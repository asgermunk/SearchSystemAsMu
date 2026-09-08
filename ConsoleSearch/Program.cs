using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleSearch
{
    /* Composition root. The REPL is a client of the search service now, so the only
     * thing it has to be told is where that service lives.
     */
    class Program
    {
        private const string DefaultApiBaseUrl = "http://localhost:5223/";

        static async Task Main(string[] args)
        {
            // "dotnet run --project ConsoleSearch -- http://otherhost:5223/" also works
            var apiBaseUrl = args.Length > 0 ? args[0] : DefaultApiBaseUrl;

            var services = new ServiceCollection();

            services.AddSingleton(new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
            services.AddSingleton<SearchApiClient>();
            services.AddSingleton<App>();

            using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<App>().Run();
        }
    }
}
