using System;
using System.Collections.Generic;

namespace SearchCore;

public interface ISearchLogic
{
    SearchResult Search(String[] query, int maxAmount, bool caseSensitive);
    List<string> MissingWords(int docId, Dictionary<string, List<int>> wordIds, List<int> allWordIds);
}