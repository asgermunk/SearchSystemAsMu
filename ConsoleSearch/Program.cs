using Microsoft.Extensions.DependencyInjection;
using SearchCore;
using SearchRepository;
using Shared;

namespace ConsoleSearch
{
    /* Composition root. This is the only file that decides which database the search
     * component runs on - a service host would replace exactly this file.
     */
    class Program
    {
        static void Main(string[] args)
        {
            var services = new ServiceCollection();

            services.AddSearchCore();
            services.AddSqliteSearchRepository(new SearchRepositoryOptions
            {
                SqliteDatabasePath = Paths.SQLITE_DATABASE
            });
            services.AddSingleton<App>();

            using var provider = services.BuildServiceProvider();

            provider.GetRequiredService<App>().Run();
        }
    }
}
