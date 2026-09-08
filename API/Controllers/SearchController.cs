using API.Models;
using Microsoft.AspNetCore.Mvc;
using SearchCore;

namespace API.Controllers;

/* The web front end of the search component. It holds no search logic - it only
 * translates HTTP into a call on the injected ISearchLogic and back into JSON.
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
    /// Get the documents a single word occurs in, most relevant first.
    /// </summary>
    /// <param name="word">The word to look up.</param>
    /// <param name="maxAmount">How many documents to return details for.</param>
    /// <param name="caseSensitive">When false, "the" also matches "The" and "THE".</param>
    [HttpGet("instances")]
    [ProducesResponseType(typeof(WordInstancesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<WordInstancesResponse> GetInstances(
        [FromQuery] string word,
        [FromQuery] int maxAmount = 10,
        [FromQuery] bool caseSensitive = false)
    {
        if (string.IsNullOrWhiteSpace(word))
            return BadRequest("A word must be given.");
        if (maxAmount < 1)
            return BadRequest("maxAmount must be at least 1.");

        var result = mSearchLogic.Search(new[] { word.Trim() }, maxAmount, caseSensitive);

        var response = new WordInstancesResponse
        {
            Word = word.Trim(),
            // a word not present in any document is reported as ignored by the search
            InIndex = result.Ignored.Count == 0,
            Hits = result.Hits,
            TimeUsedMs = result.TimeUsed.TotalMilliseconds
        };

        foreach (var hit in result.DocumentHits)
        {
            response.Documents.Add(new DocumentInstance
            {
                DocumentId = hit.Document.mId,
                Url = hit.Document.mUrl,
                IndexTime = hit.Document.mIdxTime
            });
        }

        return Ok(response);
    }
}
