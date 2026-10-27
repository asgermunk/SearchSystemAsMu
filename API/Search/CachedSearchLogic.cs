using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace API.Search
{
    /* Wraps the real ISearchLogic with a Redis cache of whole search results. The
     * key is the query words, maxAmount and caseSensitive; the shard's own prefix
     * (the InstanceName given to AddStackExchangeRedisCache) keeps two shards
     * sharing one Redis apart. Every lookup counts as a hit or a miss in
     * search.cache.hits / search.cache.misses and becomes a span in the trace.
     *
     * If Redis cannot be reached the search runs uncached instead of failing.
     * SearchLogic and the controller know nothing about it.
     */
    public class CachedSearchLogic : ISearchLogic
    {
        public const string TelemetryName = "API.Search.Cache";

        private static readonly ActivitySource Source = new(TelemetryName);
        private static readonly Meter Meter = new(TelemetryName);
        private static readonly Counter<long> Hits =
            Meter.CreateCounter<long>("search.cache.hits", description: "Searches answered from the cache.");
        private static readonly Counter<long> Misses =
            Meter.CreateCounter<long>("search.cache.misses", description: "Searches not in the cache, run against the database.");
        private static readonly Counter<long> Errors =
            Meter.CreateCounter<long>("search.cache.errors", description: "Cache calls that failed, so the search ran uncached.");

        // BEDocument has public fields, not properties
        private static readonly JsonSerializerOptions Json = new() { IncludeFields = true };

        private readonly ISearchLogic mInner;
        private readonly IDistributedCache mCache;
        private readonly DistributedCacheEntryOptions mEntryOptions;
        private readonly ILogger<CachedSearchLogic> mLogger;

        public CachedSearchLogic(ISearchLogic inner, IDistributedCache cache, TimeSpan expiry, ILogger<CachedSearchLogic> logger)
        {
            mInner = inner;
            mCache = cache;
            mEntryOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry };
            mLogger = logger;
        }

        public SearchResult Search(String[] query, int maxAmount, bool caseSensitive)
        {
            var start = DateTime.Now;
            var key = $"search:{(caseSensitive ? "cs" : "ci")}:{maxAmount}:{string.Join(' ', query)}";

            using var activity = Source.StartActivity("cache lookup", ActivityKind.Client);
            activity?.SetTag("db.system", "redis");
            activity?.SetTag("cache.key", key);

            var cached = Get(key);
            if (cached != null)
            {
                Hits.Add(1);
                activity?.SetTag("cache.hit", true);
                // report the time this lookup took, not the time of the original search
                return new SearchResult(cached.Query, cached.Hits, cached.DocumentHits, cached.Ignored, DateTime.Now - start);
            }

            Misses.Add(1);
            activity?.SetTag("cache.hit", false);
            var result = mInner.Search(query, maxAmount, caseSensitive);
            Set(key, result);
            return result;
        }

        public List<string> MissingWords(int docId, Dictionary<string, List<int>> wordIds, List<int> allWordIds) =>
            mInner.MissingWords(docId, wordIds, allWordIds);

        private SearchResult? Get(string key)
        {
            try
            {
                var json = mCache.GetString(key);
                return json == null ? null : JsonSerializer.Deserialize<SearchResult>(json, Json);
            }
            catch (Exception e)
            {
                Failed(e, "read", key);
                return null;
            }
        }

        private void Set(string key, SearchResult result)
        {
            try
            {
                mCache.SetString(key, JsonSerializer.Serialize(result, Json), mEntryOptions);
            }
            catch (Exception e)
            {
                Failed(e, "write", key);
            }
        }

        private void Failed(Exception e, string operation, string key)
        {
            Errors.Add(1, new KeyValuePair<string, object?>("operation", operation));
            Activity.Current?.SetStatus(ActivityStatusCode.Error, e.Message);
            mLogger.LogWarning(e, "Cache {Operation} failed for {Key}, searching without the cache", operation, key);
        }
    }
}
