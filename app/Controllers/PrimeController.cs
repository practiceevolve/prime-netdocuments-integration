using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PE.Mk2.Integrations.NetDocuments.Configurations;
using PE.Mk2.Integrations.NetDocuments.Services;
using System.Security.Claims;
using System.Text.Json;

namespace PE.Mk2.Integrations.NetDocuments.Controllers
{
    [ApiController]
    [Route("prime")]
    [Authorize(Policy = "WebhookPolicy")]
    public class PrimeController(ILogger<PrimeController> logger,
        NetDocsServiceFactory getNetDocsSvc,
        PrimeServiceFactory getPrimeSvc) : ControllerBase
    {
        [HttpPost("client")]
        public async Task<ActionResult> ClientUpdated([FromBody] JsonElement body)
        {
            try
            {
                logger.LogInformation("ClientUpdated in Prime: " + body);

                var clientUpdatedEvent = body.GetProperty("data");
                var client = clientUpdatedEvent.GetProperty("data");
                await EnsureClient(client);

                logger.LogInformation("Client updated in NetDocs: " + body);

                return Ok();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ClientUpdated handler failed");
                return BadRequest("ClientUpdated handler failed");
            }
        }

        /// <summary>
        /// MatterCreated, MatterUpdated
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        [HttpPost("matter")]
        public async Task<ActionResult> MatterUpdated([FromBody] JsonElement body)
        {
            try
            {
                logger.LogInformation("MatterUpdated: " + body);

                var matterUpdated = body.GetProperty("data");
                var matter = matterUpdated.GetProperty("data");
                await EnsureMatter(matter);

                return Ok();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MatterUpdated handler failed");
                return BadRequest("MatterUpdated handler failed");
            }
        }

        /// <summary>
        /// DocumentCreated, DocumentCheckedIn
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        [HttpPost("document")]
        public async Task<ActionResult> DocumentUpdated([FromBody] JsonElement body)
        {
            try
            {


                logger.LogInformation("DocumentUpdated: " + body);
                // Upload a document to NetDocs
                var updated = body.GetProperty("data");
                var documentId = updated.GetProperty("id").GetString();
                var document = await PrimeSvc.GetDocument(documentId);

                var collectionId = document.GetProperty("documentCollectionId").GetString();
                var fileName = document.GetProperty("fileName").GetString();
                var collection = await PrimeSvc.GetCollection(collectionId);

                var collectionName = collection.GetProperty("name").GetString();
                var subjectId = collection.GetProperty("subjectId").GetString();

                string? clientNumber = null, matterNumber = null;
                if (subjectId.StartsWith("client"))
                {
                    logger.LogInformation($"Client {collectionName}: {subjectId}");
                    clientNumber = await EnsureClient(await PrimeSvc.GetClient(subjectId));
                }
                else if (subjectId.StartsWith("matter"))
                {
                    logger.LogInformation($"Matter {collectionName}: {subjectId}");
                    (matterNumber, clientNumber) = await EnsureMatter(await PrimeSvc.GetMatter(subjectId));
                }
                else
                {
                    // Receipts and other firm-level collections
                    logger.LogInformation($"General {collectionName} {subjectId}: {documentId}");
                }

                await NetDocsSvc.UploadDocument(documentId, clientNumber, matterNumber,
                    fileName, await PrimeSvc.DownloadDocument(documentId));

                return Ok();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DocumentUpdated handler failed");
                return BadRequest("DocumentUpdated handler failed");
            }
        }

        [HttpPost("settings")]
        public async Task<ActionResult<JsonElement>> SettingValidationRequested([FromBody] JsonElement body)
        {
            var success = new { Success = true };

            if (!body.TryGetProperty("data", out var payload) ||
                !payload.TryGetProperty("configData", out var configData) ||
                string.IsNullOrWhiteSpace(configData.GetString()))
            {
                return Ok(success);
            }

            try
            {
                var configJson = JsonDocument.Parse(configData.GetString());
                var netDocsConfig = configJson.RootElement.Deserialize<NetDocsConfig>();

                var problems = await NetDocsSvc.Validate(netDocsConfig);
                if (problems == null)
                    return Ok(success);
                else
                    return BadRequest(new { Success = false, Reason = string.Join(Environment.NewLine, problems) });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Reason = ex.Message });
            }
        }


        private async Task<string> EnsureClient(JsonElement client)
        {
            var clientNumber = client.GetProperty("clientNumber").GetString();
            await NetDocsSvc.EnsureClient(
                clientNumber,
                client.GetProperty("sortName").GetString());
            return clientNumber;
        }

        private async Task<(string matterNumber, string clientNumber)> EnsureMatter(JsonElement matter)
        {
            var clientId = matter.GetProperty("clientId").GetString();
            var matterNumber = matter.GetProperty("matterNumber").GetString();
            var matterTitle = matter.GetProperty("sortTitle").GetString();

            var client = await PrimeSvc.GetClient(clientId);
            var clientNumber = client.GetProperty("clientNumber").GetString() ?? clientId;
            await EnsureClient(client);

            await NetDocsSvc.EnsureMatter(clientNumber, matterNumber, matterTitle);
            return (matterNumber, clientNumber);
        }




        private PrimeService PrimeSvc
        {
            get
            {
                var tenantAlias = User.FindFirstValue(ClaimNames.Tenant);
                return getPrimeSvc.Get(tenantAlias);
            }
        }

        private NetDocsService NetDocsSvc
        {
            get
            {
                var tenantAlias = User.FindFirstValue(ClaimNames.Tenant);
                return getNetDocsSvc.Get(tenantAlias);
            }
        }
    }
}