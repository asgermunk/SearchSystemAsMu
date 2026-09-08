using Microsoft.Extensions.DependencyInjection;
using SearchCore;

namespace SearchRepository
{
    /* Registers the storage half of the search system. A host picks its repository
     * here; swapping SQLite for another store means calling a different extension
     * and touching nothing in SearchCore.
     */
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSqliteSearchRepository(this IServiceCollection services, SearchRepositoryOptions options)
        {
            services.AddSingleton(options);

            /* Singleton: DatabaseSqlite holds one open connection and caches the whole
             * word table for the lifetime of the process - see GetWordIds.
             */
            services.AddSingleton<IDatabase>(sp => new DatabaseSqlite(options.SqliteDatabasePath));
            return services;
        }
    }
}
