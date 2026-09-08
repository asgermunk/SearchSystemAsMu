namespace SearchRepository
{
    /* Everything the repository needs to know about its surroundings. The hosting
     * process supplies it - a console program from a constant, a web service from
     * its own configuration.
     */
    public class SearchRepositoryOptions
    {
        public string SqliteDatabasePath { get; set; }
    }
}
