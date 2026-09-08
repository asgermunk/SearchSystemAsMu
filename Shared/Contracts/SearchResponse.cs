using System.Collections.Generic;

namespace Shared.Contracts
{
    /* The wire contract of GET /api/search/instances. It lives in Shared so the API
     * and every client - the REPL and the Blazor front end - agree on one definition
     * instead of each keeping a copy.
     *
     * These are properties, not fields like BEDocument, because System.Text.Json
     * ignores public fields by default.
     */
    public class SearchResponse
    {
        /// <summary>The words the query was split into.</summary>
        public List<string> Query { get; set; } = new();

        /// <summary>Number of documents containing at least one query word.</summary>
        public int Hits { get; set; }

        /// <summary>Time the search itself took, in milliseconds.</summary>
        public double TimeUsedMs { get; set; }

        /// <summary>Query words that are not in the index at all.</summary>
        public List<string> Ignored { get; set; } = new();

        /// <summary>The best matching documents, at most the requested amount.</summary>
        public List<DocumentHitDto> Documents { get; set; } = new();
    }

    public class DocumentHitDto
    {
        public int DocumentId { get; set; }

        public string Url { get; set; } = string.Empty;

        public string IndexTime { get; set; } = string.Empty;

        /// <summary>How many distinct query words this document contains.</summary>
        public int NoOfHits { get; set; }

        /// <summary>Query words not found in this document.</summary>
        public List<string> Missing { get; set; } = new();
    }
}
