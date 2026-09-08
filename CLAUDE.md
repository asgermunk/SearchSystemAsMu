# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A proof-of-concept search engine (course code base for "Arkitektur principper"), consisting of two .NET 10 console programs plus two class libraries:

- `indexer/` — crawls a folder recursively, extracts words from `.txt` files, writes a reverse index to a database.
- `SearchCore/` — the search component: `ISearchLogic`/`SearchLogic`, the result types, and the read side `IDatabase` **contract**. No storage code, no dependency on a database package.
- `SearchRepository/` — the storage half: `DatabaseSqlite` (the `IDatabase` implementation) and `SearchRepositoryOptions`. Depends on `SearchCore`, never the other way round.
- `ConsoleSearch/` — a thin REPL client over `SearchCore`; it is the composition root and nothing more.
- `API/` — ASP.NET Core host for `SearchCore` (MVC template plus `SearchController`). Swagger UI at `/swagger` in Development.
- `SearchFrontend/` — Blazor WebAssembly client, a Google styled search page ("Søgle") that calls the API over HTTP. Knows nothing about `SearchCore`; it only has the API's URL.
- `Shared/` — `Paths` (connection strings) and `BEDocument` (business entity for a document). Nothing else is shared.

The indexer picks its database engine at startup (SQLite or Postgres) via a console prompt. The search side is SQLite only.

## Commands

Build everything:

```bash
dotnet build SearchSystem.sln
```

Run the indexer (crawls `Config.FOLDER`, then prints stats):

```bash
dotnet run --project indexer
```

Run the search REPL:

```bash
dotnet run --project ConsoleSearch
```

The indexer prompts `Use SQLite (1) or Postgres (2) database?` on stdin before doing anything; ConsoleSearch does not prompt at all and goes straight to the query loop. To drive them non-interactively, pipe the answers — e.g. a SQLite indexer run, or a single search for `hello world` then quit:

```bash
echo 1 | dotnet run --project indexer
```

```bash
printf 'hello world\nq\n' | dotnet run --project ConsoleSearch
```

Run the web stack — the API first, then the frontend, in two terminals:

```bash
dotnet run --project API --launch-profile http
```

```bash
dotnet run --project SearchFrontend --launch-profile http
```

The API listens on `http://localhost:5223` (Swagger at `/swagger`), the frontend on `http://localhost:5136`. The frontend reads the API's address from `SearchFrontend/wwwroot/appsettings.json` (`ApiBaseUrl`), and the API allows that origin through the `frontend` CORS policy in `API/Program.cs` — change one and change the other.

There is no test project, linter, or CI configuration in the repo.

## Configuration that must be edited before anything runs

These are hardcoded source constants (checked in with one developer's macOS paths), not settings files:

- `Shared/Paths.cs` — `SQLITE_DATABASE` (absolute path to the `.db` file) and `POSTGRES_DATABASE` (connection string, password is a literal `XXXX` placeholder).
- `indexer/Config.cs` — `FOLDER`, the root directory to crawl.

Both programs read the same `Paths` constants: the indexer opens the database read-write-create, the search side read-only. `SearchCore` itself never touches `Paths` — `ConsoleSearch/Program.cs` passes the path in as `SearchCoreOptions.SqliteDatabasePath`.

## Architecture

### Schema (created by the indexer, never by ConsoleSearch)

```
document(id INTEGER PRIMARY KEY, url TEXT, idxTime TEXT, creationTime TEXT)
word(id INTEGER PRIMARY KEY, name)
Occ(wordId INTEGER, docId INTEGER)   -- the reverse index; INDEX word_index ON Occ(wordId)
```

**Destructive constructor:** `Indexer.DatabaseSqlite` / `Indexer.DatabasePostgres` run `DROP TABLE` + `CREATE TABLE` in their constructors. Merely instantiating an indexer database object wipes the existing index — there is no incremental indexing, and no code path that only reads from the indexer side.

### Two separate `IDatabase` interfaces

`Indexer.IDatabase` and `SearchCore.IDatabase` are distinct types in distinct namespaces. The indexer side is write-oriented (`InsertDocument`, `InsertAllWords`, `InsertAllOcc`, `GetAllWords`, `DocumentCounts`) and has both a SQLite and a Postgres implementation; the search side is read-oriented (`GetWordIds`, `GetDocuments`, `GetDocDetails`, `getMissing`, `WordsFromIds`) and has SQLite only. A change to the storage contract usually means touching both interfaces.

### Dependency injection on the search side

Wiring is split in two, matching the project split. `SearchCore.AddSearchCore()` registers `ISearchLogic` -> `SearchLogic` and says nothing about storage. `SearchRepository.AddSqliteSearchRepository(options)` registers `SearchRepositoryOptions` and `IDatabase` -> `new DatabaseSqlite(options.SqliteDatabasePath)`. A host calls both:

```csharp
services.AddSearchCore();
services.AddSqliteSearchRepository(new SearchRepositoryOptions { SqliteDatabasePath = ... });
```

Everything is registered as a **singleton** — `DatabaseSqlite` holds one open connection and caches the entire `word` table for the lifetime of the process, so it must not be transient.

The dependency arrow is the point of the split: `IDatabase` lives with its consumer in `SearchCore`, and `SearchRepository` references `SearchCore` to implement it. Swapping in another store means a new project with its own `Add...Repository` extension and one changed line in each host; `SearchCore` does not move.

`ConsoleSearch/Program.cs` is the composition root and the only file in that program that decides anything about storage: it builds a `ServiceCollection`, calls the two extensions with `Paths.SQLITE_DATABASE`, registers `App`, and resolves `App` from the provider. `App` takes `ISearchLogic` in its constructor and knows nothing else — no database type appears anywhere in the `ConsoleSearch` project. Hosting the search behind a web API therefore means writing a new composition root that calls the same `AddSearchCore`; no logic moves. `API/Program.cs` is exactly that second host: it calls both extensions, taking the path from `Configuration["SearchRepository:SqliteDatabasePath"] ?? Paths.SQLITE_DATABASE`, and `API/Controllers/SearchController` takes `ISearchLogic` in its constructor. `GET /api/search/instances?word=x&maxAmount=10&caseSensitive=false` is a one word `Search` call mapped onto the `WordInstancesResponse` DTO in `API/Models` — the API contract is deliberately its own type, not `SearchResult`.

### Frontend

`SearchFrontend` is a standalone Blazor WebAssembly app; it runs in the browser and reaches the API over HTTP, which is why the API needs the CORS policy. `Services/SearchApiClient` is the only class that knows the API exists, `Models/WordInstancesResponse` is the client side copy of the API contract, and `Pages/Home.razor` holds the whole UI — landing view, results view, the `Components/Logo` wordmark, and the case sensitivity toggle that mirrors the REPL's `/ChangeCaseSensitive`. Because the endpoint takes a single word, the page turns away a query containing spaces with a hint instead of searching for something the index cannot contain. The search field is a `<form>` with a submit button (the magnifier), so Enter searches.

When that happens, the singleton connection plus word cache is the thing to revisit — the cache is also why a running search process never sees words added by a later indexer run.

### Indexing flow

`indexer/App.Run` → `Crawler.IndexFilesIn(root, [".txt"])`, recursing into subdirectories. Per file: increment `documentCounter` (that counter *is* the document id), insert the document, split the file on `Crawler.separators` into a `HashSet<string>` of distinct words, then insert only the words not seen before and one `Occ` row per distinct word in the file.

Word ids are allocated **in memory** by `Crawler` (`words.Count + 1`) and inserted as explicit ids — the database never generates them. The crawler's `words` dictionary is therefore the source of truth for the whole run, and only the delta (`newWords`) is written per file. Word extraction is presence-only: `Occ` has no term frequency, and no case folding or stemming happens — the index is case preserving, so case insensitive search is done on the search side by folding the cached `word` table.

### Search flow

`ConsoleSearch/App` reads a line, splits on spaces, calls `SearchLogic.Search(query, maxAmount: 10, caseSensitive)`. `caseSensitive` is REPL state on `App`, defaulting to **off** and toggled with `/ChangeCaseSensitive [on|off]`:

1. `GetWordIds(query, caseSensitive, out ignored)` — on first call the implementation loads the *entire* `word` table into `mWords`, plus `mWordsIgnoringCase` (lower cased word → the ids of every casing of it), and caches both for the process lifetime; query words absent from the relevant lookup go into `Ignored`. Because the cache is loaded once, a search process will not see words added by a later indexer run. The return value maps each query word to the ids matching it — one id when case sensitive, potentially several (`the`, `The`, `THE`) when not. Query words that collapse to the same key are searched once.
2. `GetDocuments(wordIdGroups)` — one group of ids per query word. The core query counts each query word once, no matter how many of its casings the document holds: `SELECT docId, MAX(CASE WHEN wordId IN (<group 1>) THEN 1 ELSE 0 END) + ... AS count FROM Occ WHERE wordId IN (<all ids>) GROUP BY docId ORDER BY count DESC`. This is an OR search ranked by how many distinct query words a document contains.
3. For the top `maxAmount` doc ids: `GetDocDetails` plus `SearchLogic.MissingWords`, which asks `getMissing` for the ids absent from the document and reports a *query* word as missing when none of its ids are present — so a case insensitive search does not list each casing separately. Reported to the user alongside `Ignored`.

Read-side SQL is assembled by string concatenation of integer id lists (`AsString`) rather than parameters; write-side inserts use parameters and wrap each batch in a transaction.

## Conventions in this code base

- Fields on entity classes use the `m` prefix (`mId`, `mUrl`, `mIdxTime`); locals and privates often use `_`/`m` inconsistently — match the surrounding file rather than normalizing.
- `indexer/Renamer.cs` is an unused utility (appends `.txt` to extensionless files); it is invoked only from a commented-out line in `indexer/Program.cs`.
