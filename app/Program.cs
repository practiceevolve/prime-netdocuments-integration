using Microsoft.AspNetCore.Authorization;
using PE.Mk2.Integrations.NetDocuments.Configurations;
using PE.Mk2.Integrations.NetDocuments.Filters;
using PE.Mk2.Integrations.NetDocuments.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(ConfigService.TenantConfigFile, optional: true);
builder.Services.Configure<AppConfig>(builder.Configuration);
var appConfig = builder.Configuration.Get<AppConfig>();

// Add logging and metrics
builder.Services.AddObservability(appConfig);

// Add openapi documentation
builder.Services.AddAppOpenApi();

// Controllers
builder.Services.AddControllers();

// Register the handler and authorization policy
builder.Services.AddSingleton<IAuthorizationHandler, WebhookAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, ApiKeyAuthorizationHandler>();

// Security
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("ApiKeyPolicy", policy => policy.Requirements.Add(new ApiKeyRequirement(appConfig.ConsoleApiKey)))
    .AddPolicy("WebhookPolicy", policy => policy.Requirements.Add(new WebhookAuthorizationRequirement()));

// Application services
builder.Services.AddSingleton<ConfigService>();
builder.Services.AddSingleton<NetDocsServiceFactory>();
builder.Services.AddSingleton<PrimeServiceFactory>();


var app = builder.Build();

///////////////////////////////////////////

app.UseHttpsRedirection();

// Serve prometheus metrics at /metrics
app.MapPrometheusScrapingEndpoint();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

app.MapControllers();

// Subscribe to webhooks
await RunOnStartup(
    app.Services.GetService<ConfigService>(),
    app.Services.GetService<ILogger<Program>>());

app.Run();

///////////////////////////////////////////

/// <summary>
/// Register webhooks and startup
/// </summary>
async Task RunOnStartup(ConfigService configService, Microsoft.Extensions.Logging.ILogger logger)
{
    var services = new List<IAsyncInit>();
    int retryIntervalInSecs = 5;

    var primeServiceFactory = app.Services.GetService<PrimeServiceFactory>();
    var netDocsServiceFactory = app.Services.GetService<NetDocsServiceFactory>();

    var config = await configService.GetConfig();

    foreach (var tenantConfig in config.Tenants ?? [])
    {
        var primeService = primeServiceFactory.Create(config.Prime, tenantConfig.Prime);
        var netDocsService = netDocsServiceFactory.Create(tenantConfig.Prime.Tenant, tenantConfig.NetDocs);

        _ = queueInit(primeService);
        _ = queueInit(netDocsService);
    }

    async Task queueInit(IAsyncInit service)
    {
        try
        {
            await service.InitAsync();
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Service {service.GetType().Name} failed to initialise ({ex.Message}), trying in {retryIntervalInSecs:n0}s");
            await Task.Delay(TimeSpan.FromSeconds(retryIntervalInSecs));
            _ = queueInit(service);
        }
    }
}

