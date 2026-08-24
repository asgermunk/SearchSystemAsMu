using System;
using System.Collections.Generic;
using Shared.Model;

namespace ConsoleSearch
{
    public class SearchLogic
    {
        IDatabase mDatabase;

        public SearchLogic(IDatabase database)
        {
            mDatabase = database;
        }

        /* Perform search of documents containing words from query. The result will
         * contain details about amost maxAmount of documents. When caseSensitive is
         * false a query word also matches the other casings of that word in the index.
         */
        public SearchResult Search(String[] query, int maxAmount, bool caseSensitive)
        {
            List<string> ignored;

            DateTime start = DateTime.Now;

            // Convert words to wordids - one query word can match several ids when
            // the search is case insensitive
            var wordIds = mDatabase.GetWordIds(query, caseSensitive, out ignored);

            if (wordIds.Count == 0) // no words know in index
                 return new SearchResult(query, 0, new List<DocumentHit>(), ignored, DateTime.Now - start);

            var wordIdGroups = new List<List<int>>(wordIds.Values);
            var allWordIds = new List<int>();
            foreach (var group in wordIdGroups)
                allWordIds.AddRange(group);

            // perform the search - get all docIds
            var docIds =  mDatabase.GetDocuments(wordIdGroups);

            // get ids for the first maxAmount             
            var top = new List<int>();
            foreach (var p in docIds.GetRange(0, Math.Min(maxAmount, docIds.Count)))
                top.Add(p.Key);

            // compose the result.
            // all the documentHit
            List<DocumentHit> docresult = new List<DocumentHit>();
            int idx = 0;
            foreach (var docId in top)
            {
                BEDocument doc = mDatabase.GetDocDetails(docId);
                var missing = MissingWords(doc.mId, wordIds, allWordIds);
                missing.AddRange(ignored);
                docresult.Add(new DocumentHit(doc, docIds[idx++].Value, missing));
            }

            return new SearchResult(query, docIds.Count, docresult, ignored, DateTime.Now - start);
        }

        /* A query word is missing from the document when none of the ids matching it
         * occurs in the document. The query word is reported, not the indexed words,
         * so a case insensitive search does not report every casing separately.
         */
        private List<string> MissingWords(int docId, Dictionary<string, List<int>> wordIds, List<int> allWordIds)
        {
            var missingIds = new HashSet<int>(mDatabase.getMissing(docId, allWordIds));

            var result = new List<string>();
            foreach (var p in wordIds)
            {
                bool present = false;
                foreach (var id in p.Value)
                    if (!missingIds.Contains(id)) { present = true; break; }
                if (!present)
                    result.Add(p.Key);
            }
            return result;
        }
    }
}
