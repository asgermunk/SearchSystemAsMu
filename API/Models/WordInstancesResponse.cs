namespace API.Models;

/* The response of GET /api/search/instances. A flat contract of its own, so the
 * shape of the API does not follow whatever SearchCore returns internally.
 */
public class WordInstancesResponse
{
    /// <summary>The word that was looked up.</summary>
    public string Word { get; set; } = string.Empty;

    /// <summary>False when the word is not present in the index at all.</summary>
    public bool InIndex { get; set; }

    /// <summary>Total number of documents containing the word.</summary>
    public int Hits { get; set; }

    /// <summary>Time the search itself took, in milliseconds.</summary>
    public double TimeUsedMs { get; set; }

    /// <summary>The first [maxAmount] documents containing the word.</summary>
    public List<DocumentInstance> Documents { get; set; } = new();
}

public class DocumentInstance
{
    public int DocumentId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string IndexTime { get; set; } = string.Empty;
}
