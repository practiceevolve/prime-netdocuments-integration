using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using PE.Mk2.Integrations.NetDocuments;
using PE.Mk2.Integrations.NetDocuments.Configurations;
using Serilog;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

public static class AspNetExtensions
{
    /// <summary>
    /// Add Open API documentation
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddAppOpenApi(this IServiceCollection services)
    {
        return services.AddOpenApi(options =>
         {
             var apiKeySchemeId = "ApiKey";

             // Define the API key security scheme
             var apiKeyScheme = new OpenApiSecurityScheme
             {
                 Type = SecuritySchemeType.ApiKey,
                 In = ParameterLocation.Header, // API key in the request header
                 Name = "X-API-KEY", // The header name for the API key
                 Description = "API key needed to access the endpoints"
             };
             options.AddDocumentTransformer((document, context, cancellationToken) =>
             {
                 // Add the API key security definition to the document
                 document.Components ??= new OpenApiComponents();
                 document.Components.SecuritySchemes.Add(apiKeySchemeId, apiKeyScheme);
                 return Task.CompletedTask;
             });

             options.AddOperationTransformer((operation, context, cancellationToken) =>
             {
                 // Add a security requirement for some operations
                 if (context.Description.RelativePath.StartsWith("console/"))
                 {
                     operation.Security.Add(new OpenApiSecurityRequirement
                     {
                         [new OpenApiSecurityScheme { Reference = new OpenApiReference { Id = apiKeySchemeId, Type = ReferenceType.SecurityScheme } }] = Array.Empty<string>()
                     });
                 }
                 return Task.CompletedTask;
             });
         });
    }

    /// <summary>
    /// Add Serilog and OpenTelemetry
    /// </summary>
    /// <param name="services"></param>
    /// <param name="appConfig"></param>
    /// <returns></returns>
    public static IServiceCollection AddObservability(this IServiceCollection services, AppConfig? appConfig)
    {
        // Setup Serilog with OpenTelemetry and Elasticsearch
        var loggerConfig = new LoggerConfiguration()
            .WriteTo.Console();

        if (!string.IsNullOrWhiteSpace(appConfig.Logging?.Uri))
        {
            loggerConfig = loggerConfig
                .WriteTo.Elasticsearch([new Uri(appConfig.Logging.Uri)],
                opts =>
                {
                    opts.DataStream = new DataStreamName(appConfig.Logging.DataStreamType, appConfig.Logging.DataStreamDataSet, appConfig.Logging.DataStreamNamespace);
                    opts.BootstrapMethod = BootstrapMethod.Silent;
                },
                transport =>
                {
                    // transport.Authentication(new BasicAuthentication(username, password)); // Basic Auth
                    if (!string.IsNullOrWhiteSpace(appConfig.Logging.ApiKey))
                    {
                        transport.Authentication(new ApiKey(appConfig.Logging.ApiKey)); // Key must be base65 encoded
                    }
                });
        }

        Log.Logger = loggerConfig.CreateLogger();
        services.AddSerilog();

        // OpenTelemetry
        var otel = services.AddOpenTelemetry();

        // Configure OpenTelemetry Resources with the application name
        otel.ConfigureResource(resource => resource
            .AddService(serviceName: Assembly.GetExecutingAssembly().FullName));

        // Add Metrics for ASP.NET Core and our custom metrics and export to Prometheus
        otel.WithMetrics(metrics => metrics
            // Inbound HTTP metrics
            .AddMeter("Microsoft.AspNetCore.Hosting")
            // Outbound HTTP metrics
            .AddMeter("System.Net.Http")
            // Application metrics
            .AddMeter(Observability.Instance.MeterName)
            // Serve a prometheus endpoint
            .AddPrometheusExporter());

        return services;
    }
}