using System.Net.Http.Json;
using Shared.Contracts;

namespace SearchFrontend.Services;

/* The only thing in the frontend that knows the API exists.
 */
public class SearchApiClient
{
    private readonly HttpClient mHttp;

    public SearchApiClient(HttpClient http)
    {
        mHttp = http;
    }

    public async Task<SearchResponse?> SearchAsync(string query, int maxAmount = 10, bool caseSensitive = false)
    {
        var url = $"api/search/instances?query={Uri.EscapeDataString(query)}"
                + $"&maxAmount={maxAmount}&caseSensitive={caseSensitive}";

        return await mHttp.GetFromJsonAsync<SearchResponse>(url);
    }
}
