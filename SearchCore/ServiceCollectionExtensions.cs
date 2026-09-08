using Microsoft.Extensions.DependencyInjection;

namespace SearchCore
{
    /* Registers the search logic. It says nothing about storage - the host adds an
     * IDatabase separately, e.g. with SearchRepository.AddSqliteSearchRepository.
     */
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSearchCore(this IServiceCollection services)
        {
            services.AddSingleton<ISearchLogic, SearchLogic>();
            return services;
        }
    }
}
