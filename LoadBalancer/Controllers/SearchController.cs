using Microsoft.AspNetCore.Mvc;
using Shared.Contracts;
using System.Diagnostics;
using System.Net.Http.Json;

namespace LoadBalancer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    // Each backend owns a different shard of the indexed document collection.
    private static readonly string[] Backends =
    {
        "http://localhost:5223",
        "http://localhost:5224"
    };

    private readonly HttpClient mHttp;

    public SearchController(IHttpClientFactory httpClientFactory)
    {
        mHttp = httpClientFactory.CreateClient("search-shards");
    }

    [HttpGet("instances")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<SearchResponse>> GetInstances(
        [FromQuery] string query,
        [FromQuery] int maxAmount = 10,
        [FromQuery] bool caseSensitive = false)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("A query must be given.");
        if (maxAmount < 1)
            return BadRequest("maxAmount must be at least 1.");

        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Task.WhenAll starts both HTTP requests before awaiting either one.
            var shardResponses = await Task.WhenAll(
                Backends.Select(backend => SearchShardAsync(backend, query, maxAmount, caseSensitive)));

            var response = new SearchResponse
            {
                Query = words.ToList(),
                Hits = shardResponses.Sum(shard => shard.Hits),
                TimeUsedMs = stopwatch.Elapsed.TotalMilliseconds,
                // A word is globally ignored only when no shard has indexed it.
                Ignored = words
                    .Where(word => shardResponses.All(shard => shard.Ignored.Contains(word)))
                    .Distinct()
                    .ToList(),
                Documents = shardResponses
                    .SelectMany(shard => shard.Documents)
                    .OrderByDescending(document => document.NoOfHits)
                    .ThenBy(document => document.Url, StringComparer.Ordinal)
                    .Take(maxAmount)
                    .ToList()
            };

            return Ok(response);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                "One or more search shards could not be reached.");
        }
    }

    private async Task<SearchResponse> SearchShardAsync(
        string backend,
        string query,
        int maxAmount,
        bool caseSensitive)
    {
        var url = $"{backend}/api/search/instances?query={Uri.EscapeDataString(query)}"
                + $"&maxAmount={maxAmount}&caseSensitive={caseSensitive}";

        var response = await mHttp.GetFromJsonAsync<SearchResponse>(url);
        return response ?? throw new HttpRequestException($"Search shard '{backend}' returned no response body.");
    }
}
