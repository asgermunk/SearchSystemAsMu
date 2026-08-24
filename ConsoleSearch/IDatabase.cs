using System.Collections.Generic;
using Shared.Model;

namespace ConsoleSearch
{
    public interface IDatabase
    {
        /// <summary>
        /// Get id's for words in [query]. The key is the word from the query and the
        /// value is the id's of the indexed words matching it. When [caseSensitive] is
        /// false a query word matches every indexed word that differs from it only in
        /// casing, so one query word can have several id's. [outIgnored] contains those
        /// word from query that is not present in any document.
        /// </summary>
        Dictionary<string, List<int>> GetWordIds(string[] query, bool caseSensitive, out List<string> outIgnored);

        /// <summary>
        /// Get document by its id
        /// </summary>
        BEDocument GetDocDetails(int docId);

        /// <summary>
        /// Perform the essential search for documents. Each element of [wordIdGroups]
        /// holds the id's matching one word of the query. It will return
        /// a list of KeyValuePairs - the key is the id of the
        /// document, and value is the number of words from the query
        /// contained in the document. A query word counts once, no matter how many of
        /// its casings the document contains. The list is ordrered for descending value.
        /// </summary>
        List<KeyValuePair<int, int>> GetDocuments(List<List<int>> wordIdGroups);

        /// <summary>
        /// Return all ids of words, contained in [wordIds], but not
        /// present in the document with id [docId]
        /// </summary>
        List<int> getMissing(int docId, List<int> wordIds);

        /// <summary>
        /// Convert a list of word id's to a list of the value of the
        /// words
        /// </summary>
        List<string> WordsFromIds(List<int> wordIds);
    }
}
