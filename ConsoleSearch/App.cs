using System;
using System.Net.Http;
using System.Threading.Tasks;
using Shared.Contracts;

namespace ConsoleSearch
{
    /* The user interface of the search system - it knows nothing but the API client.
     */
    public class App
    {
        private readonly SearchApiClient mClient;

        // false: "hello" also matches "Hello" and "HELLO" in the index
        private bool mCaseSensitive = false;

        public App(SearchApiClient client)
        {
            mClient = client;
        }

        public async Task Run()
        {
            Console.WriteLine("Console Search");
            Console.WriteLine($"Searching through {mClient.BaseAddress}");
            Console.WriteLine("/ChangeCaseSensitive [on|off] - turn case sensitive search on or off");

            while (true)
            {
                Console.WriteLine($"enter search terms - q for quit (case sensitive: {OnOff(mCaseSensitive)})");
                string input = Console.ReadLine();
                if (input == null || input.Equals("q")) break;

                if (input.StartsWith("/ChangeCaseSensitive", StringComparison.OrdinalIgnoreCase))
                {
                    ChangeCaseSensitive(input);
                    continue;
                }

                var query = input.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (query.Length == 0) continue;

                var result = await Search(query);
                if (result == null) continue;

                if (result.Ignored.Count > 0) {
                    Console.WriteLine($"Ignored: {string.Join(',', result.Ignored)}");
                }

                int idx = 1;
                foreach (var doc in result.Documents) {
                    Console.WriteLine($"{idx} : {doc.Url} -- contains {doc.NoOfHits} search terms");
                    Console.WriteLine("Index time: " + doc.IndexTime);
                    Console.WriteLine($"Missing: {ArrayAsString(doc.Missing.ToArray())}");
                    idx++;
                }
                Console.WriteLine("Documents: " + result.Hits + ". Time: " + result.TimeUsedMs);
            }
        }

        /* The search runs in another process now, so it can simply be unreachable.
         * Say so rather than letting the exception end the session.
         */
        private async Task<SearchResponse> Search(string[] query)
        {
            try
            {
                return await mClient.SearchAsync(query, 10, mCaseSensitive);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Could not reach the search API on {mClient.BaseAddress} - {e.Message}");
                Console.WriteLine("Start it with: dotnet run --project API --launch-profile http");
                return null;
            }
        }

        /* Handle the /ChangeCaseSensitive command. The setting can be given on the
         * command line - otherwise it is asked for.
         */
        private void ChangeCaseSensitive(string command)
        {
            var parts = command.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            string setting = parts.Length > 1 ? parts[1] : null;

            while (setting == null || !(setting.Equals("on", StringComparison.OrdinalIgnoreCase)
                                     || setting.Equals("off", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("Type on or off");
                setting = Console.ReadLine();
                if (setting == null) return;
            }

            mCaseSensitive = setting.Equals("on", StringComparison.OrdinalIgnoreCase);
            Console.WriteLine($"Case sensitive search is {OnOff(mCaseSensitive)}");
        }

        string OnOff(bool b) => b ? "on" : "off";

        string ArrayAsString(string[] s) => s.Length == 0?"[]":$"[{String.Join(',', s)}]";
    }
}
