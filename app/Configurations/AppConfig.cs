namespace PE.Mk2.Integrations.NetDocuments.Configurations
{
    public class AppConfig
    {
        /// <summary>
        /// Required X-API-KEY request header to interact with the /console endpoints
        /// </summary>
        public string ConsoleApiKey { get; set; }
        /// <summary>
        /// Elasticsearch logging config
        /// </summary>
        public ElasticsearchLogConfig Logging { get; set; }

        /// <summary>
        /// Evolve Prime base configuration
        /// </summary>
        public PrimeConfig Prime { get; set; }

        /// <summary>
        /// If running integration for multiple tenants, the tenant-specific keys
        /// </summary>
        public IList<TenantConfig> Tenants { get; set; }
    }

    public class PrimeConfig
    {
        /// <summary>
        /// Fixed unique ID for this integration to identify it in the PE application directory
        /// </summary>
        public static readonly Guid IntegrationId = new("42d88299-71c9-4230-a995-5a78c1bd9146");

        /// <summary>
        /// Evolve Prime's API endpoint, including trailing slash. Any {tenant} tag in the URL is replaced with the tenant property.
        /// </summary>
        public string ApiUrl { get; set; }
        /// <summary>
        /// Used to request oauth tokens
        /// </summary>
        public string TokenEndpoint { get; set; }
        /// <summary>
        /// OAuth application identifier
        /// </summary>
        public string ClientId { get; set; }
        /// <summary>
        /// OAuth application secret
        /// </summary>
        public string ClientSecret { get; set; }
        /// <summary>
        /// Scope to request, as known by Prime's identity provider and the Prime API
        /// </summary>
        public string Scope { get; set; }
        /// <summary>
        /// HMAC signing key that Prime should use to sign the message to ensure it is not tampered with
        /// </summary>
        public Guid? SigningKey { get; set; }
        /// <summary>
        /// Publicly addressable URL to reach the Prime webhook receivers
        /// </summary>
        public Uri ReceiverUrl { get; set; }
    }

    public class TenantConfig
    {
        /// <summary>
        /// Evolve Prime tenant configuration
        /// </summary>
        public PrimeTenantConfig Prime { get; set; }

        /// <summary>
        /// NetDocs configuration
        /// </summary>
        public NetDocsConfig NetDocs { get; set; }

    }

    public class PrimeTenantConfig
    {
        /// <summary>
        /// Tenant alias used to identify which tenant to run the integration against
        /// </summary>
        public string Tenant { get; set; }

        /// <summary>
        /// Evolve Prime's API endpoint, including trailing slash. Any {tenant} tag in the URL is replaced with the tenant property.
        /// </summary>
        /// <remarks>Overrides ApiUrl set in base config - leave null to use base config value</remarks>
        public string? ApiUrl { get; set; }
    }


    public class NetDocsConfig
    {
        public string OAuthTokenUrl { get; set; } = "https://api.au.netdocuments.com/v1/OAuth";
        public string ApiUrl { get; set; } = "https://api.au.netdocuments.com/";
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }

        public string RepositoryId { get; set; }
        public string CabinetId { get; set; }
        public string ClientAttributeId { get; set; } = "1";
        public string MatterAttributeId { get; set; } = "2";
    }


    public class ElasticsearchLogConfig
    {
        /// <summary>
        /// Elasticsearch URI. If not specified, Elasticsearch log export is disabled
        /// </summary>
        public string Uri { get; set; }
        /// <summary>
        /// Base64 encoded API key to access Elasticsearch
        /// </summary>
        public string ApiKey { get; set; }
        /// <summary>
        /// Data stream type. Should remain as the default 'logs'
        /// </summary>
        public string DataStreamType { get; set; } = "logs";
        /// <summary>
        /// Optional. Dataset name of the data stream.  Defaults to 'generic'
        /// </summary>
        public string DataStreamDataSet { get; set; } = "generic";
        /// <summary>
        /// Optional. Namespace of the data stream.  Defaults to 'default'
        /// </summary>
        public string DataStreamNamespace { get; set; } = "default";
    }
}
