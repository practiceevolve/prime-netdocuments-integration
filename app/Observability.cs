using System.Diagnostics.Metrics;
using System.Diagnostics;
using System.Reflection;

namespace PE.Mk2.Integrations.NetDocuments
{
    public class Observability
    {
        public static Observability Instance { get; } = new();

        public string MeterName { get; }
        public Counter<int> WebhooksReceived { get; }
        public ActivitySource NetDocsPutLookup { get; }

        private Observability()
        {
            var assemblyName = Assembly.GetExecutingAssembly().GetName();
            MeterName = assemblyName.FullName.ToLowerInvariant();
            var applicationMeter = new Meter(MeterName, assemblyName.Version.ToString());

            // Custom metrics for the application
            WebhooksReceived = applicationMeter.CreateCounter<int>("webhooks_received_total", description: "Total number of webhook events received from Prime");

            // Custom ActivitySource for the application
            NetDocsPutLookup = new ActivitySource($"{assemblyName.FullName}.PutLookup");
        }
    }
}
