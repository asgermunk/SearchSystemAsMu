using System;
using System.Collections.Generic;
using System.IO;
using Shared;

namespace Indexer
{
    public class App
    {
        public void Run(string[] args)
        {
            if (args.Length > 2)
            {
                Console.WriteLine("Usage: indexer [folder-to-index] [sqlite-database-path]");
                return;
            }

            var folder = args.Length > 0 ? args[0] : Config.FOLDER;
            var sqliteDatabasePath = args.Length > 1 ? args[1] : Paths.SQLITE_DATABASE;

            IDatabase db;
            try
            {
                db = GetDatabase(sqliteDatabasePath);
            }
            catch (InvalidOperationException exception)
            {
                Console.WriteLine(exception.Message);
                return;
            }
            Crawler crawler = new Crawler(db);

            var root = new DirectoryInfo(folder);
            if (!root.Exists)
            {
                Console.WriteLine($"Folder does not exist: {root.FullName}");
                return;
            }

            DateTime start = DateTime.Now;

            crawler.IndexFilesIn(root, new List<string> { ".txt"});        

            TimeSpan used = DateTime.Now - start;
            Console.WriteLine("DONE! used " + used.TotalMilliseconds);

            var all = db.GetAllWords();

            Console.WriteLine($"Indexed {db.DocumentCounts} documents");
            Console.WriteLine($"Number of different words: {all.Count}");
            int count = 10;
            Console.WriteLine($"The first {count} is:");
            foreach (var p in all) {
                Console.WriteLine("<" + p.Key + ", " + p.Value + ">");
                count--;
                if (count == 0) break;
            }
        }

        private IDatabase GetDatabase(string sqliteDatabasePath)
        {
            Console.Write("Use SQLite (1) or Postgres (2) database?");
            string input = Console.ReadLine();
            if (input.Equals("1"))
                return new DatabaseSqlite(sqliteDatabasePath);
            else if (input.Equals("2"))
                return new DatabasePostgres();
            Console.WriteLine("Wrong input - try again...");
            return GetDatabase(sqliteDatabasePath);
        }
    }
}
