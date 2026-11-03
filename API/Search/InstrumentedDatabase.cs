using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Shared.Model;

namespace API.Search
{
    /* Wraps the real IDatabase and times every call, so a slow search can be split
     * into time spent in the database and time spent in the API around it. Each
     * call becomes a span in the request's trace and a sample in the
     * search.database.duration histogram, tagged with the operation name.
     * SearchLogic and DatabaseSqlite know nothing about it.
     */
    public class InstrumentedDatabase : IDatabase
    {
        public const string TelemetryName = "API.Search";

        private static readonly ActivitySource Source = new(TelemetryName);
        private static readonly Meter Meter = new(TelemetryName);
        private static readonly Histogram<double> Duration = Meter.CreateHistogram(
            "search.database.duration", unit: "s",
            description: "Time spent in one call to the search database.",
            tags: null,
            // the same buckets as http.server.request.duration, so the two compare
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10]
            });

        private readonly IDatabase mInner;

        public InstrumentedDatabase(IDatabase inner)
        {
            mInner = inner;
        }

        public Dictionary<string, List<int>> GetWordIds(string[] query, bool caseSensitive, out List<string> outIgnored)
        {
            List<string> ignored = null!;
            var result = Measure(nameof(GetWordIds), () => mInner.GetWordIds(query, caseSensitive, out ignored));
            outIgnored = ignored;
            return result;
        }

        public BEDocument GetDocDetails(int docId) =>
            Measure(nameof(GetDocDetails), () => mInner.GetDocDetails(docId));

        public List<KeyValuePair<int, int>> GetDocuments(List<List<int>> wordIdGroups) =>
            Measure(nameof(GetDocuments), () => mInner.GetDocuments(wordIdGroups));

        public List<int> getMissing(int docId, List<int> wordIds) =>
            Measure(nameof(getMissing), () => mInner.getMissing(docId, wordIds));

        public List<string> WordsFromIds(List<int> wordIds) =>
            Measure(nameof(WordsFromIds), () => mInner.WordsFromIds(wordIds));

        private static T Measure<T>(string operation, Func<T> call)
        {
            using var activity = Source.StartActivity("db " + operation, ActivityKind.Client);
            activity?.SetTag("db.system", "sqlite");
            activity?.SetTag("db.operation.name", operation);

            var start = Stopwatch.GetTimestamp();
            try
            {
                return call();
            }
            catch (Exception e)
            {
                activity?.SetStatus(ActivityStatusCode.Error, e.Message);
                throw;
            }
            finally
            {
                Duration.Record(Stopwatch.GetElapsedTime(start).TotalSeconds,
                    new KeyValuePair<string, object?>("operation", operation));
            }
        }
    }
}
