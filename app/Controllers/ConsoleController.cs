using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PE.Mk2.Integrations.NetDocuments.Configurations;
using PE.Mk2.Integrations.NetDocuments.Services;

namespace PE.Mk2.Integrations.NetDocuments.Controllers
{
    [ApiController]
    [Route("console")]
    [Authorize(Policy = "ApiKeyPolicy")]
    public class ConsoleController(ConfigService configService): ControllerBase
    {
        /// <summary>
        /// Get config for a tenant alias
        /// </summary>
        /// <param name="tenantAlias"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        [HttpGet]
        [Route("config")]
        public async Task<ActionResult<TenantConfig>> GetConfig(string tenantAlias)
        {
            if (string.IsNullOrWhiteSpace(tenantAlias)) throw new ArgumentException($"Argument tenantAlias must be specified");

            var config = await configService.GetConfig(tenantAlias);

            if (config == null) return NotFound($"No configuration found for tenant {tenantAlias}");
            
            var redacted = "<redacted>";
            config.NetDocs.ClientSecret = redacted;
            return config;
        }

        /// <summary>
        /// Save tenant config
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        [HttpPut]
        [Route("config")]
        public async Task PutConfig(TenantConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.Prime.Tenant)) throw new ArgumentException($"Property config.Prime.Tenant must be specified");
            await configService.SetConfig(config);
        }

        /// <summary>
        /// Delete tenant config
        /// </summary>
        /// <param name="tenantAlias"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        [HttpDelete]
        [Route("config")]
        public async Task DeleteConfig(string tenantAlias)
        {
            if (string.IsNullOrWhiteSpace(tenantAlias)) throw new ArgumentException($"Argument tenantAlias must be specified");
            await configService.DeleteConfig(tenantAlias);
        }
    }
}
