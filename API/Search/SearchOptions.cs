namespace API.Search
{
    /* Everything the search side needs to know about its surroundings; the host
     * supplies it from its own configuration.
     */
    public class SearchOptions
    {
        public string SqliteDatabasePath { get; set; }
    }
}
