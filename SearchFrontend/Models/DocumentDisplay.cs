using Shared.Contracts;

namespace SearchFrontend.Models;

/* Presentation helpers for the shared contract - splitting a path for display is
 * the front end's business, not the API's.
 */
public static class DocumentDisplay
{
    private static readonly char[] Separators = { '/', (char)92 };

    public static string FileName(this DocumentHitDto doc)
    {
        var cut = doc.Url.LastIndexOfAny(Separators);
        return cut < 0 ? doc.Url : doc.Url.Substring(cut + 1);
    }

    public static string Folder(this DocumentHitDto doc)
    {
        var cut = doc.Url.LastIndexOfAny(Separators);
        return cut < 0 ? string.Empty : doc.Url.Substring(0, cut);
    }
}
