using Microsoft.Extensions.Options;
using PE.Mk2.Integrations.NetDocuments.Configurations;
using System.Text.Json;

namespace PE.Mk2.Integrations.NetDocuments.Services
{
    /// <summary>
    /// Overlays tenant config with base config in the appsettings config files
    /// </summary>
    public class ConfigService(ILoggerFactory loggerFactory, IOptions<AppConfig> appConfig)
    {
        private readonly AppConfig _config = appConfig.Value;
        private readonly ReaderWriterLockSlim _lock = new();

        /// <summary>
        /// Name of the file storing tenant app settings
        /// </summary>
        public const string TenantConfigFile = "appsettings.tenants.json";

        /// <summary>
        /// Get the list of all configured tenants, or if single-tenanted, just the base config
        /// </summary>
        /// <returns></returns>
        public async Task<AppConfig> GetConfig()
        {
            try
            {
                _lock.EnterReadLock();
                return _config;
            }
            finally
            {
    _lock.ExitReadLock();
}
        }

        /// <summary>
        /// Get config for a tenant
        /// </summary>
        /// <param name="tenantAlias"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task<TenantConfig> GetConfig(string tenantAlias)
        {
            try
            {
                _lock.EnterReadLock();
                var tenants = _config.Tenants ??= [];
                if (string.IsNullOrWhiteSpace(tenantAlias)) throw new ArgumentNullException(nameof(tenantAlias));
                var existing = tenants.FirstOrDefault(t => t.Prime?.Tenant == tenantAlias.ToLowerInvariant());
                return existing;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Add or replace an existing tenant config
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task SetConfig(TenantConfig config) {
            try
            {
                _lock.EnterWriteLock();
                var tenants = _config.Tenants ??= new List<TenantConfig>();
                if (string.IsNullOrWhiteSpace(config.Prime.Tenant)) throw new ArgumentNullException(nameof(config.Prime.Tenant));
                config.Prime.Tenant = config.Prime.Tenant.ToLowerInvariant();

                var existing = tenants.FirstOrDefault(t => t.Prime?.Tenant == config.Prime.Tenant);
                if (existing != null) tenants.Remove(existing);
                tenants.Add(config);
                
                WriteConfig(tenants);
            }
            finally { 
                _lock.ExitWriteLock(); 
            }
        }


        /// <summary>
        /// Get config for a tenant
        /// </summary>
        /// <param name="tenantAlias"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task DeleteConfig(string tenantAlias)
        {
            try
            {
                _lock.EnterWriteLock();
                var tenants = _config.Tenants ??= [];
                if (string.IsNullOrWhiteSpace(tenantAlias)) throw new ArgumentNullException(nameof(tenantAlias));
                var existing = tenants.FirstOrDefault(t => t.Prime?.Tenant == tenantAlias.ToLowerInvariant());
                if (existing != null) tenants.Remove(existing);

                WriteConfig(tenants);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }





        private void WriteConfig(IList<TenantConfig> tenants)
        {
            // we are writing to a file that will be read as an AppConfig object
            // which will override any base configuration
            // therefore we only want to write the Tenants property

            var tenantConfigs = new
            {
                Tenants = tenants
            };

            var tempFile = Path.GetTempFileName();
            using (var fs = new FileStream(tempFile, FileMode.Truncate))
            {
                using var jw = new Utf8JsonWriter(fs, new JsonWriterOptions() { Indented = true });
                JsonSerializer.Serialize(jw, tenantConfigs);
            }

            var tenantConfigFile = new FileInfo(TenantConfigFile);
            File.Move(tempFile, tenantConfigFile.FullName, true);
        }

       }
}