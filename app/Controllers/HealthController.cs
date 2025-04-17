using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PE.Mk2.Integrations.NetDocuments.Services;


namespace PE.Mk2.Integrations.NetDocuments.Controllers
{
    [ApiController]
    [Route("healthz")]
    public class HealthController(NetDocsServiceFactory netDocsServiceFactory, PrimeServiceFactory primeServiceFactory) : ControllerBase
    {
        /// <summary>
        /// Liveness check (returns 200 OK if the app is running)
        /// </summary>
        /// <returns></returns>
        [HttpGet("live")]
        public IActionResult LivenessCheck()
        {
            return Ok("Application is alive");
        }

        /// <summary>
        /// Readiness check (returns 200 OK if the app is ready to receive traffic)
        /// </summary>
        [HttpGet("ready")]
        public IActionResult ReadinessCheck()
        {
            return Ok("Application is ready");
        }
    }
}

