using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Shared.Contracts;

namespace ConsoleSearch
{
    /* The only class in the REPL that knows the search service exists. It speaks
     * HTTP; the search itself happens in the API process.
     */
    public class SearchApiClient
    {
        private readonly HttpClient mHttp;

        public SearchApiClient(HttpClient http)
        {
            mHttp = http;
        }

        public string BaseAddress => mHttp.BaseAddress?.ToString() ?? "";

        public async Task<SearchResponse> SearchAsync(string[] query, int maxAmount, bool caseSensitive)
        {
            var url = $"api/search/instances?query={Uri.EscapeDataString(string.Join(' ', query))}"
                    + $"&maxAmount={maxAmount}&caseSensitive={caseSensitive}";

            return await mHttp.GetFromJsonAsync<SearchResponse>(url);
        }
    }
}
