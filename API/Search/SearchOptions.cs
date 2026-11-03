namespace API.Search
{
    /* Everything the search side needs to know about its surroundings; the host
     * supplies it from its own configuration.
     */
    public class SearchOptions
    {
        public string SqliteDatabasePath { get; set; }

        /* host:port of the Redis that caches search results. Unset means no cache,
         * which is what a plain "dotnet run" gets.
         */
        public string? RedisConnectionString { get; set; }

        public string? RedisPassword { get; set; }

        public TimeSpan CacheExpiry { get; set; } = TimeSpan.FromMinutes(5);
    }
}
