using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PE.Mk2.Integrations.NetDocuments.Configurations;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PE.Mk2.Integrations.NetDocuments.Filters
{
    public class WebhookAuthorizationRequirement : IAuthorizationRequirement
    {
        public WebhookAuthorizationRequirement() { }
    }


    public class WebhookAuthorizationHandler : AuthorizationHandler<WebhookAuthorizationRequirement>
    {
        private readonly Guid? _signingKey;
        private readonly ILogger<WebhookAuthorizationHandler> _logger;

        public WebhookAuthorizationHandler(ILogger<WebhookAuthorizationHandler> logger, IOptions<AppConfig> config)
        {
            _logger = logger;
            if (config.Value.Prime.SigningKey == null)
            {
                _logger.LogWarning("Webhook SigningKey not specified, no validation will be done. This is strongly discouraged webhook receivers become a public unprotected endpoint.");
            }
            _signingKey = config.Value.Prime.SigningKey;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, WebhookAuthorizationRequirement requirement)
        {
            var httpContext = context.Resource as HttpContext;
            if (httpContext != null && httpContext.Request.Method == HttpMethod.Post.Method)
            {
                if (!IsValidSignature(httpContext.Request))
                {
                    context.Fail(new(this, "Invalid signature"));
                    return;
                }

                var timestamp = httpContext.Request.Headers["X-PE2-TIMESTAMP"];
                if (!IsValidTimestamp(timestamp))
                {
                    context.Fail(new(this, "Invalid timestamp"));
                    return;
                }

                // Tenant (e.g. law firm identifier) name to identify which tenant this call came from
                var tenantAlias = httpContext.Request.Headers["X-PE2-TENANT-ALIAS"];
                if (!string.IsNullOrWhiteSpace(tenantAlias))
                {
                    ((ClaimsIdentity)context.User.Identity).AddClaim(new Claim(ClaimNames.Tenant, tenantAlias));
                }

                // Request for identifying duplicate requests
                var requestId = httpContext.Request.Headers["X-PE2-REQUEST-ID"];

                context.Succeed(requirement);
            }
        }

        private bool IsValidSignature(HttpRequest request)
        {
            if (_signingKey == null) return true;
            
            var requestId = request.Headers["X-PE2-REQUEST-ID"];
            var timestamp = request.Headers["X-PE2-TIMESTAMP"];
            var receivedSignature = request.Headers["X-PE2-WEBHOOK-SIGNATURE"];

            using var hmac = new HMACSHA256(_signingKey.Value.ToByteArray());
            var computedHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{requestId}|{timestamp}")));
            return string.Equals(receivedSignature, $"HMACSHA256:{computedHash}", StringComparison.InvariantCulture);
        }

        private static bool IsValidTimestamp(string timestamp)
        {
            var requestTime = DateTime.Parse(timestamp);
            var currentTime = DateTime.Now;

            // Allow a small window of 5 minutes to account for network delays
            return Math.Abs((currentTime - requestTime).TotalMinutes) <= 5;
        }
    }
}
