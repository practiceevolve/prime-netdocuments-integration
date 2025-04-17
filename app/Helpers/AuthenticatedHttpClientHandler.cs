using System.Net.Http.Headers;
using System.Net;

namespace PE.Mk2.Integrations.NetDocuments.Helpers
{
    public class AuthenticatedHttpClientHandler : DelegatingHandler
    {
        private readonly ITokenService _tokenService; // Service to manage token retrieval and refresh

        public AuthenticatedHttpClientHandler(ITokenService tokenService)
        {
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            InnerHandler = new HttpClientHandler();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Attach the token to the request
            string token = await _tokenService.GetAccessTokenAsync();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Send the request
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            // Check for unauthorized response (401)
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Refresh the token
                token = await _tokenService.RefreshAccessTokenAsync();
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Retry the request
                response = await base.SendAsync(request, cancellationToken);
            }

            return response;
        }
    }

    public interface ITokenService
    {
        Task<string> GetAccessTokenAsync();
        Task<string> RefreshAccessTokenAsync();
    }

}
