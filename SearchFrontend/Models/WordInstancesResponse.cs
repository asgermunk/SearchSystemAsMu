namespace SearchFrontend.Models;

/* Client side view of what GET /api/search/instances returns.
 */
public class WordInstancesResponse
{
    public string Word { get; set; } = string.Empty;

    public bool InIndex { get; set; }

    public int Hits { get; set; }

    public double TimeUsedMs { get; set; }

    public List<DocumentInstance> Documents { get; set; } = new();
}

public class DocumentInstance
{
    // the index holds windows paths, but be friendly to both kinds
    private static readonly char[] Separators = { '/', '\\' };

    public int DocumentId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string IndexTime { get; set; } = string.Empty;

    /// <summary>The file name on its own, used as the title of a result.</summary>
    public string FileName => Split().name;

    /// <summary>The folder the document lives in, shown above the title.</summary>
    public string Folder => Split().folder;

    private (string folder, string name) Split()
    {
        var cut = Url.LastIndexOfAny(Separators);
        return cut < 0 ? (string.Empty, Url) : (Url.Substring(0, cut), Url.Substring(cut + 1));
    }
}
