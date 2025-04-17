using Microsoft.AspNetCore.Authorization;

namespace PE.Mk2.Integrations.NetDocuments.Filters
{
    public class ApiKeyAuthorizationHandler(ILogger<ApiKeyAuthorizationHandler> logger) : AuthorizationHandler<ApiKeyRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ApiKeyRequirement requirement)
        {
            var httpContext = context.Resource as HttpContext;
            var apiKeyHeader = httpContext.Request.Headers["X-API-KEY"].FirstOrDefault();

            if (apiKeyHeader != null && apiKeyHeader == requirement.ApiKey)
            {
                context.Succeed(requirement);  // Success if the API key matches
            }
            else
            {
                logger.LogWarning("Invalid API key provided.");
                context.Fail();  // Fail if the API key doesn't match
            }

            return Task.CompletedTask;
        }
    }

    public class ApiKeyRequirement(string apiKey) : IAuthorizationRequirement
    {
        public string ApiKey { get; } = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
    }

}
