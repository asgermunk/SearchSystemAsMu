using System;
using SearchCore;

namespace ConsoleSearch
{
    /* The user interface of the search system - it knows nothing but ISearchLogic.
     */
    public class App
    {
        private readonly ISearchLogic mSearchLogic;

        // false: "hello" also matches "Hello" and "HELLO" in the index
        private bool mCaseSensitive = false;

        public App(ISearchLogic searchLogic)
        {
            mSearchLogic = searchLogic;
        }

        public void Run()
        {
            Console.WriteLine("Console Search");
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

                var result = mSearchLogic.Search(query, 10, mCaseSensitive);

                if (result.Ignored.Count > 0) {
                    Console.WriteLine($"Ignored: {string.Join(',', result.Ignored)}");
                }
                
                int idx = 1;
                foreach (var doc in result.DocumentHits) {
                    Console.WriteLine($"{idx} : {doc.Document.mUrl} -- contains {doc.NoOfHits} search terms");
                    Console.WriteLine("Index time: " + doc.Document.mIdxTime);
                    Console.WriteLine($"Missing: {ArrayAsString(doc.Missing.ToArray())}");
                    idx++;
                }
                Console.WriteLine("Documents: " + result.Hits + ". Time: " + result.TimeUsed.TotalMilliseconds);
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
