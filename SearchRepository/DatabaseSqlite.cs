using System;
using System.Collections.Generic;
using Shared.Model;
using Microsoft.Data.Sqlite;
using SearchCore;

namespace SearchRepository
{
    public class DatabaseSqlite : IDatabase
    {
        private SqliteConnection _connection;

        private Dictionary<string, int> mWords = null;

        // lower cased word -> the ids of all indexed words with that spelling,
        // regardless of casing. Used for case insensitive search.
        private Dictionary<string, List<int>> mWordsIgnoringCase = null;

        /* The path to the database file is injected - this class knows nothing about
         * where the hosting process gets it from.
         */
        public DatabaseSqlite(string databasePath)
        {
            var connectionStringBuilder = new SqliteConnectionStringBuilder();

            connectionStringBuilder.DataSource = databasePath;

            _connection = new SqliteConnection(connectionStringBuilder.ConnectionString);

            _connection.Open();
        }


        // key is the id of the document, the value is number of search words in the document
        public List<KeyValuePair<int, int>> GetDocuments(List<List<int>> wordIdGroups)
        {
            var res = new List<KeyValuePair<int, int>>();

            /* Example sql statement looking for doc id's that contain the words
               with id 2 or 3, where 2 and 3 are two different casings of the same
               query word, and the word with id 7

               SELECT docId, MAX(CASE WHEN wordId in (2,3) THEN 1 ELSE 0 END)
                           + MAX(CASE WHEN wordId in (7) THEN 1 ELSE 0 END) as count
                 FROM Occ
                WHERE wordId in (2,3,7)
             GROUP BY docId
             ORDER BY count DESC
             */

            var allWordIds = new List<int>();
            var counters = new List<string>();
            foreach (var group in wordIdGroups)
            {
                allWordIds.AddRange(group);
                counters.Add($"MAX(CASE WHEN wordId in {AsString(group)} THEN 1 ELSE 0 END)");
            }

            var sql = "SELECT docId, " + string.Join(" + ", counters) + " as count FROM Occ where ";
            sql += "wordId in " + AsString(allWordIds) + " GROUP BY docId ";
            sql += "ORDER BY count DESC;";

            var selectCmd = _connection.CreateCommand();
            selectCmd.CommandText = sql;

            using (var reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var docId = reader.GetInt32(0);
                    var count = reader.GetInt32(1);

                    res.Add(new KeyValuePair<int, int>(docId, count));
                }
            }

            return res;
        }

        private string AsString(List<int> x) => $"({string.Join(',', x)})";



       

        private Dictionary<string, int> GetAllWords()
        {
            Dictionary<string, int> res = new Dictionary<string, int>();

            var selectCmd = _connection.CreateCommand();
            selectCmd.CommandText = "SELECT * FROM word";

            using (var reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var id = reader.GetInt32(0);
                    var w = reader.GetString(1);

                    res.Add(w, id);
                }
            }
            return res;
        }
        
        public BEDocument GetDocDetails(int docId)
        {
            var selectCmd = _connection.CreateCommand();
            selectCmd.CommandText = $"SELECT * FROM document where id = {docId}";

            using (var reader = selectCmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    var id = reader.GetInt32(0);
                    var url = reader.GetString(1);
                    var idxTime = reader.GetString(2);
                    var creationTime = reader.GetString(3);

                    return new BEDocument { mId = id, mUrl = url, mIdxTime = idxTime, mCreationTime = creationTime };
                }
            }
            return null;
        }

        /* Return a list of id's for words; all them among wordIds, but not present in the document
         */
        public List<int> getMissing(int docId, List<int> wordIds)
        {
            var sql = "SELECT wordId FROM Occ where ";
            sql += "wordId in " + AsString(wordIds) + " AND docId = " + docId;
            sql += " ORDER BY wordId;";

            var selectCmd = _connection.CreateCommand();
            selectCmd.CommandText = sql;

            List<int> present = new List<int>();

            using (var reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var wordId = reader.GetInt32(0);
                    present.Add(wordId);
                }
            }
            var result = new List<int>(wordIds);
            foreach (var w in present)
                result.Remove(w);


            return result;
        }

        public List<string> WordsFromIds(List<int> wordIds)
        {
            var sql = "SELECT name FROM Word where ";
            sql += "id in " + AsString(wordIds);

            var selectCmd = _connection.CreateCommand();
            selectCmd.CommandText = sql;

            List<string> result = new List<string>();

            using (var reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var wordId = reader.GetString(0);
                    result.Add(wordId);
                }
            }
            return result;
        }

        /* Group the indexed words by their lower cased spelling, so a case insensitive
         * lookup can find every casing of a word in one go.
         */
        private Dictionary<string, List<int>> GroupWordsIgnoringCase(Dictionary<string, int> words)
        {
            var res = new Dictionary<string, List<int>>();
            foreach (var p in words)
            {
                var key = p.Key.ToLower();
                if (!res.ContainsKey(key))
                    res.Add(key, new List<int>());
                res[key].Add(p.Value);
            }
            return res;
        }

        public Dictionary<string, List<int>> GetWordIds(string[] query, bool caseSensitive, out List<string> outIgnored)
        {
            if (mWords == null)
            {
                mWords = GetAllWords();
                mWordsIgnoringCase = GroupWordsIgnoringCase(mWords);
            }
            var res = new Dictionary<string, List<int>>();
            var ignored = new List<string>();
            var seen = new HashSet<string>();

            foreach (var aWord in query)
            {
                // the same word twice in the query counts once - and "The" and "the"
                // are the same word when the search is case insensitive
                var key = caseSensitive ? aWord : aWord.ToLower();
                if (!seen.Add(key))
                    continue;

                if (caseSensitive)
                {
                    if (mWords.ContainsKey(aWord))
                        res.Add(aWord, new List<int> { mWords[aWord] });
                    else
                        ignored.Add(aWord);
                }
                else
                {
                    if (mWordsIgnoringCase.ContainsKey(key))
                        res.Add(aWord, new List<int>(mWordsIgnoringCase[key]));
                    else
                        ignored.Add(aWord);
                }
            }
            outIgnored = ignored;
            return res;
        }
    }
}
