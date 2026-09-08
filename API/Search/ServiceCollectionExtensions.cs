using Microsoft.Extensions.DependencyInjection;

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
            services.AddSingleton<IDatabase>(sp => new DatabaseSqlite(options.SqliteDatabasePath));
            services.AddSingleton<ISearchLogic, SearchLogic>();
            return services;
        }
    }
}
