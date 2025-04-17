using Microsoft.Extensions.Diagnostics.HealthChecks;
using PE.Mk2.Integrations.NetDocuments.Configurations;
using PE.Mk2.Integrations.NetDocuments.Helpers;
using System.Text.Json;

namespace PE.Mk2.Integrations.NetDocuments.Services
{
    public class PrimeService(ILogger<PrimeService> logger, PrimeConfig config, PrimeTenantConfig tenantConfig) : IAsyncInit
    {
        private DateTime? _accessTokenExpiresOn;
        private HttpClient? _httpClient;
        private HealthCheckResult _healthStatus = HealthCheckResult.Degraded("Starting up...");

        #region Get tenant settings

        public async Task<TSetting?> GetSettings<TSetting>()
        {
            try
            {
                var http = await GetHttpClient();
                var response = await http.SendJsonAsync<JsonElement>($"v1/integrations/tenantSettings", HttpMethod.Get);
                var settings = response.GetProperty("data");
                return settings.Deserialize<TSetting>()!;
            }
            catch (Exception)
            {
                return default;
            }
        }

        public async Task<JsonElement> PutSettings(JsonElement elem)
        {
            var http = await GetHttpClient();
            var response = await http.SendJsonAsync<JsonElement>($"v1/integrations/tenantSettings", HttpMethod.Put, elem);
            var settings = response.GetProperty("data");
            return settings;
        }

        #endregion

        public HealthCheckResult CheckHealth()
        {
            return _healthStatus;
        }

        public async Task InitAsync()
        {
            try
            {
                await RegisterWebhookAsync(
                    Guid.Parse("86a24d17-0000-0000-0000-851853400170"),
                    new Uri(config.ReceiverUrl, "client"),
                    ["PE.Mk2.Accounting.V1.ClientCreated", "PE.Mk2.Accounting.V1.ClientUpdated"]);

                await RegisterWebhookAsync(
                    Guid.Parse("86a24d17-0000-0000-0000-851853400171"),
                    new Uri(config.ReceiverUrl, "document"),
                    ["PE.Mk2.Documents.V1.DocumentCreated", "PE.Mk2.Documents.V1.DocumentCheckedIn"]);

                await RegisterWebhookAsync(
                    Guid.Parse("86a24d17-0000-0000-0000-851853400172"),
                    new Uri(config.ReceiverUrl, "matter"),
                    ["PE.Mk2.Accounting.V1.MatterCreated", "PE.Mk2.Accounting.V1.MatterUpdated"]);

                await RegisterWebhookAsync(
                    Guid.Parse("86a24d17-0000-0000-0000-851853400173"),
                    new Uri(config.ReceiverUrl, "settings"),
                    ["PE.Mk2.Core.V1.SettingsValidationRequested"]);

                _healthStatus = HealthCheckResult.Healthy("Webhook registered");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to register webhook");
                _healthStatus = HealthCheckResult.Unhealthy("Failed to register webhook");
                throw;
            }
        }


        public async Task<JsonElement> GetClient(string clientId)
        {
            var http = await GetHttpClient();
            var response = await http.SendJsonAsync<JsonElement>($"v1/clients/{Uri.EscapeDataString(clientId)}", HttpMethod.Get);
            var client = response.GetProperty("data");
            return client;
        }

        public async Task<JsonElement> GetMatter(string matterId)
        {
            var http = await GetHttpClient();
            var response = await http.SendJsonAsync<JsonElement>($"v1/matters/{Uri.EscapeDataString(matterId)}", HttpMethod.Get);
            var matter = response.GetProperty("data");
            return matter;
        }

        public async Task<JsonElement> GetCollection(string collectionId)
        {
            var http = await GetHttpClient();
            var response = await http.SendJsonAsync<JsonElement>($"v1/documentcollections/{Uri.EscapeDataString(collectionId)}", HttpMethod.Get);
            var collection = response.GetProperty("data");
            return collection;
        }

        public async Task<JsonElement> GetDocument(string documentId)
        {
            var http = await GetHttpClient();
            var response = await http.SendJsonAsync<JsonElement>($"v1/documents/{Uri.EscapeDataString(documentId)}", HttpMethod.Get);
            var document = response.GetProperty("data");
            return document;
        }

        public async Task<Stream> DownloadDocument(string documentId)
        {
            var http = await GetHttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"v1/documents/{Uri.EscapeDataString(documentId)}/download");
            var response = await http.SendAsync(request);
            return response.Content.ReadAsStream();
        }





        private async Task RegisterWebhookAsync(Guid id, Uri receiverUrl, IList<string> events)
        {
            try
            {
                var client = await GetHttpClient();
                var response = await client.SendJsonAsync($"v1/webhooks", HttpMethod.Put, new
                {
                    id = $"webhook_{id:n}",
                    enabled = true,
                    url = receiverUrl,
                    secret = config.SigningKey,
                    events
                });
                logger.LogInformation(response.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to register webhook", ex);
            }
        }

        private async Task UnregisterWebhookAsync(Guid id)
        {
            var client = await GetHttpClient();
            await client.SendJsonAsync($"v1/webhooks/webhook_{id:n}", HttpMethod.Delete);
        }

        private async Task<HttpClient> GetHttpClient()
        {
            _httpClient ??= new HttpClient()
            {
                BaseAddress = new Uri((!string.IsNullOrEmpty(tenantConfig.ApiUrl) ? tenantConfig.ApiUrl : config.ApiUrl).Replace("{tenant}", tenantConfig.Tenant)),
            };

            if (_accessTokenExpiresOn == null || _accessTokenExpiresOn < DateTime.UtcNow)
            {
                var (accessToken, expiresOn) = await GetOAuthToken();
                _accessTokenExpiresOn = expiresOn;
                _httpClient.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
            }

            return _httpClient;
        }

        private async Task<(string accessToken, DateTime expiresOn)> GetOAuthToken()
        {
            using var oauthHttp = new HttpClient();
            var clientCredentials = new List<KeyValuePair<string, string>>()
            {
                new("grant_type", "client_credentials"),
                new("client_id", config.ClientId),
                new("client_secret", config.ClientSecret),
                new("scope", config.Scope)
            };

            var requestBody = new FormUrlEncodedContent(clientCredentials);

            // Make the POST request
            var response = await oauthHttp.PostAsync(config.TokenEndpoint, requestBody);

            if (response.IsSuccessStatusCode)
            {
                // Parse the response to get the access token
                string responseBody = await response.Content.ReadAsStringAsync();
                var responseElem = JsonSerializer.Deserialize<JsonElement>(responseBody);
                if (!responseElem.TryGetProperty("access_token", out var tokenElem))
                    throw new Exception("Invalid token response, expecting access_token but cannot find it");
                if (!responseElem.TryGetProperty("expires_in", out var expiresInElem) || !expiresInElem.TryGetInt32(out var expiresInSecs))
                    throw new Exception("Invalid token response, expecting expires_in but cannot find it");
                return (
                    accessToken: tokenElem.GetString() ?? throw new Exception("Returned token is null"),
                    expiresOn: DateTime.UtcNow.AddSeconds(expiresInSecs));
            }
            else
            {
                string errorResponse = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to get access token {response.StatusCode}: {errorResponse}");
            }
        }

    }

    public class PrimeServiceFactory(ILogger<PrimeService> logger)
    {
        private readonly Dictionary<string, PrimeService> _tenants = [];

        public PrimeService Create(PrimeConfig config, PrimeTenantConfig tenantConfig)
        {
            if (string.IsNullOrWhiteSpace(tenantConfig.Tenant)) throw new ArgumentNullException("config.Tenant");
            if (!_tenants.TryGetValue(tenantConfig.Tenant, out var service))
            {
                _tenants[tenantConfig.Tenant] = service = new(logger, config, tenantConfig);
            }
            return service;
        }

        public PrimeService Get(string tenantAlias)
        {
            if (!_tenants.TryGetValue(tenantAlias, out var service))
            {
                throw new Exception($"Cannot find configuration for tenant {tenantAlias}");
            }
            return service;
        }
    }

}
