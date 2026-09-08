using System.Net.Http.Json;
using SearchFrontend.Models;

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

    public async Task<WordInstancesResponse?> GetInstancesAsync(string word, int maxAmount = 10, bool caseSensitive = false)
    {
        var url = $"api/search/instances?word={Uri.EscapeDataString(word)}&maxAmount={maxAmount}&caseSensitive={caseSensitive}";
        return await mHttp.GetFromJsonAsync<WordInstancesResponse>(url);
    }
}
