using Microsoft.Extensions.Diagnostics.HealthChecks;
using PE.Mk2.Integrations.NetDocuments.Configurations;
using PE.Mk2.Integrations.NetDocuments.Helpers;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PE.Mk2.Integrations.NetDocuments.Services
{
    public class NetDocsService(ILogger<NetDocsService> logger, NetDocsConfig config) : IAsyncInit
    {
        private HealthCheckResult _healthStatus;
        private HttpClient? _httpClient;

        public async Task EnsureClient(string clientNumber, string displayName)
        {
            clientNumber = Sanitise(clientNumber);

            await _httpClient.SendJsonAsync($"v1/attributes/{config.RepositoryId}/{config.ClientAttributeId}" +
                $"/{Uri.EscapeDataString(clientNumber)}",
                HttpMethod.Put, new { description = displayName });
        }

        public async Task EnsureMatter(string clientNumber, string matterNumber, string matterTitle)
        {
            clientNumber = Sanitise(clientNumber);
            matterNumber = Sanitise(matterNumber);

            await _httpClient.SendJsonAsync($"v1/attributes/{config.RepositoryId}/{config.MatterAttributeId}" +
                $"/{Uri.EscapeDataString(clientNumber)}" +
                $"/{Uri.EscapeDataString(matterNumber)}",
                HttpMethod.Put, new { description = matterTitle });
        }

        public async Task<JsonElement> UploadDocument(string documentId, string clientNumber, string matterNumber, string fileName, Stream fileContent)
        {
            clientNumber = Sanitise(clientNumber);
            matterNumber = Sanitise(matterNumber);

            var request = new HttpRequestMessage(HttpMethod.Post, "v1/document");
            var content = new MultipartFormDataContent
            {
                { new StringContent("upload"), "action" },
                { new StreamContent(fileContent), "file", fileName },
                {
                    JsonContent.Create(new[] {
                      new {
                        id = config.ClientAttributeId,
                        value = clientNumber
                      },
                      new {
                        id = config.MatterAttributeId,
                        value = matterNumber
                      }
                    }),
                    "profile"
                },
                { new StringContent(config.CabinetId), "cabinet" },
                { new StringContent("full"), "return" }
            };
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (!response.IsSuccessStatusCode) throw new Exception($"Failed to upload {documentId} to NetDocs: {result}");

            logger.LogInformation($"Uploaded {documentId} to NetDocs: {result}");
            return result;
        }

        /// <summary>
        /// Change all illegal netdocs ids
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static string Sanitise(string input) => input.Replace('/', '-');




        public async Task InitAsync()
        {
            try
            {
                _httpClient = await SetupNetDocsHttpClient(config);

                _healthStatus = HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                _healthStatus = HealthCheckResult.Unhealthy($"Failed to retrieve OAuth token [{ex.Message}]");
                throw;
            }
        }

        private static async Task<HttpClient> SetupNetDocsHttpClient(NetDocsConfig config)
        {
            var tokenService = new NetDocsTokenService(config.OAuthTokenUrl, config.ClientId, config.RepositoryId, config.ClientSecret);
            var httpClient = new HttpClient(new AuthenticatedHttpClientHandler(tokenService))
            {
                BaseAddress = new Uri(config.ApiUrl),
                DefaultRequestHeaders =
                {
                    Accept = { new MediaTypeWithQualityHeaderValue("application/json") }
                }
            };

            return httpClient;
        }

        /// <summary>
        /// Validate NetDocuments config
        /// </summary>
        /// <param name="netDocsConfig"></param>
        /// <returns></returns>
        public async Task<IList<string>?> Validate(NetDocsConfig netDocsConfig)
        {
            try
            {
                await SetupNetDocsHttpClient(netDocsConfig);
                return null;
            }
            catch (Exception ex)
            {
                return [ex.Message];
            }
        }

        public HealthCheckResult CheckHealth()
        {
            return _healthStatus;
        }

    }

    public class NetDocsTokenService : ITokenService
    {
        private string _accessToken = "";
        private DateTime _expires;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private readonly string _authTokenUrl;
        private readonly string _clientId;
        private readonly string _repositoryId;
        private readonly string _clientSecret;

        public NetDocsTokenService(string authTokenUrl, string clientId, string repositoryId, string clientSecret)
        {
            _authTokenUrl = authTokenUrl;
            _clientId = clientId;
            _repositoryId = repositoryId;
            _clientSecret = clientSecret;
        }
        public async Task<string> GetAccessTokenAsync()
        {
            // Check if the token is available and valid
            if (string.IsNullOrEmpty(_accessToken) || TokenExpired(_accessToken))
            {
                await RefreshAccessTokenAsync();
            }
            return _accessToken;
        }

        public async Task<string> RefreshAccessTokenAsync()
        {
            await _semaphore.WaitAsync(); // Acquire the lock asynchronously
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, _authTokenUrl);
                request.Headers.Add("User-Agent", "PEPrime-NetDocs");
                request.Headers.Add("Accept", "application/json");
                var clientRepoSecret = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}|{_repositoryId}:{_clientSecret}"));
                request.Headers.Add("Authorization", $"Basic {clientRepoSecret}");
                var collection = new List<KeyValuePair<string, string>>
                {
                    new("grant_type", "client_credentials"),
                    new("scope", "full")
                };
                var content = new FormUrlEncodedContent(collection);
                request.Content = content;
                var tokenClient = new HttpClient();
                var response = await tokenClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = await JsonDocument.ParseAsync(response.Content.ReadAsStream());

                    var tkn = tokenResponse.RootElement.GetProperty("access_token").GetString();
                    if (string.IsNullOrEmpty(tkn))
                        throw new Exception("Unable to obtain access_token");

                    var expires_seconds = tokenResponse.RootElement.GetProperty("expires_in").GetString();
                    if (!int.TryParse(expires_seconds, out var expires))
                        throw new Exception("Unable to obtain expires_in");

                    _accessToken = tkn;
                    _expires = DateTime.Now.AddSeconds(expires - 5);
                }
            }
            finally
            {
                _semaphore.Release(); // Always release the lock
            }

            return _accessToken;
        }

        private bool TokenExpired(string token)
        {
            return DateTime.Now > _expires;
        }
    }
    public class NetDocsServiceFactory(ILogger<NetDocsService> logger)
    {
        private readonly Dictionary<string, NetDocsService> _tenants = [];

        public NetDocsService Create(string tenantAlias, NetDocsConfig config)
        {
            if (string.IsNullOrWhiteSpace(tenantAlias)) throw new ArgumentNullException(nameof(tenantAlias));
            if (!_tenants.TryGetValue(tenantAlias, out var service))
            {
                _tenants[tenantAlias] = service = new(logger, config);
            }
            return service;
        }

        public NetDocsService Get(string tenantAlias)
        {
            if (!_tenants.TryGetValue(tenantAlias, out var service))
            {
                throw new Exception($"Cannot find configuration for tenant {tenantAlias}");
            }
            return service;
        }
    }

}
