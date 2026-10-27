using System.IO;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace API.Search
{
    /* The search service's own wiring. Singletons: DatabaseSqlite holds one open
     * connection and caches the entire word table for the lifetime of the process,
     * so it must not be transient.
     */
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSearch(this IServiceCollection services, SearchOptions options)
        {
            services.AddSingleton(options);
            // InstrumentedDatabase times each call for OpenTelemetry, then delegates
            services.AddSingleton<IDatabase>(sp =>
                new InstrumentedDatabase(new DatabaseSqlite(options.SqliteDatabasePath)));

            if (string.IsNullOrWhiteSpace(options.RedisConnectionString))
            {
                services.AddSingleton<ISearchLogic, SearchLogic>();
                return services;
            }

            services.AddStackExchangeRedisCache(redis =>
            {
                redis.ConfigurationOptions = new ConfigurationOptions
                {
                    EndPoints = { options.RedisConnectionString },
                    Password = options.RedisPassword,
                    // keep retrying in the background, so the API starts before Redis does
                    AbortOnConnectFail = false,
                    ConnectTimeout = 2000,
                    SyncTimeout = 2000
                };
                // the shards share one Redis but not their documents
                redis.InstanceName = Path.GetFileNameWithoutExtension(options.SqliteDatabasePath) + ":";
            });
            // CachedSearchLogic answers from Redis when it can, otherwise asks SearchLogic
            services.AddSingleton<SearchLogic>();
            services.AddSingleton<ISearchLogic>(sp => new CachedSearchLogic(
                sp.GetRequiredService<SearchLogic>(),
                sp.GetRequiredService<IDistributedCache>(),
                options.CacheExpiry,
                sp.GetRequiredService<ILogger<CachedSearchLogic>>()));
            return services;
        }
    }
}
