using API.Search;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts;

namespace API.Controllers;

/* The web face of the search service. It holds no search logic - it translates
 * HTTP into a call on ISearchLogic and the result into the shared wire contract.
 */
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SearchController : ControllerBase
{
    private readonly ISearchLogic mSearchLogic;

    public SearchController(ISearchLogic searchLogic)
    {
        mSearchLogic = searchLogic;
    }

    /// <summary>
    /// Get the documents the query words occur in, ranked by how many distinct
    /// query words each document contains.
    /// </summary>
    /// <param name="query">The search terms, separated by spaces.</param>
    /// <param name="maxAmount">How many documents to return details for.</param>
    /// <param name="caseSensitive">When false, "the" also matches "The" and "THE".</param>
    [HttpGet("instances")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<SearchResponse> GetInstances(
        [FromQuery] string query,
        [FromQuery] int maxAmount = 10,
        [FromQuery] bool caseSensitive = false)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("A query must be given.");
        if (maxAmount < 1)
            return BadRequest("maxAmount must be at least 1.");

        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var result = mSearchLogic.Search(words, maxAmount, caseSensitive);

        var response = new SearchResponse
        {
            Query = words.ToList(),
            Hits = result.Hits,
            TimeUsedMs = result.TimeUsed.TotalMilliseconds,
            Ignored = result.Ignored
        };

        foreach (var hit in result.DocumentHits)
        {
            response.Documents.Add(new DocumentHitDto
            {
                DocumentId = hit.Document.mId,
                Url = hit.Document.mUrl,
                IndexTime = hit.Document.mIdxTime,
                NoOfHits = hit.NoOfHits,
                Missing = hit.Missing
            });
        }

        return Ok(response);
    }
}
