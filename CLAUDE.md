# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A proof-of-concept search engine (course code base for "Arkitektur principper"). **The API is the search service** — it owns the search logic and is the only process that touches the search database. Everything else is either the indexer or a client of that service.

- `indexer/` — crawls a folder recursively, extracts words from `.txt` files, writes a reverse index to a database. Stands alone; shares nothing with the search side but the database file and `Shared`.
- `API/` — the search service. `API/Search/` holds the logic (`ISearchLogic`/`SearchLogic`), the read side `IDatabase` and its `DatabaseSqlite` implementation; `API/Controllers/SearchController` is the HTTP face. Swagger UI at `/swagger` in Development.
- `LoadBalancer/` — round robins across several API instances. Exposes the same route as the API (`api/search/instances`) so a client only changes its base URL to go through it.
- `ConsoleSearch/` — the REPL, an HTTP client of the API. No search logic and no database reference.
- `SearchFrontend/` — Blazor WebAssembly client, a Google styled search page ("Søgle"). Also just an HTTP client.
- `Shared/` — `Paths` (connection strings), `BEDocument` (the indexer's document entity) and `Contracts/SearchResponse` (the API's wire contract, referenced by the service and both clients so the shape is defined once).

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

Run the search REPL — **the API must be running first**, since the REPL is a client of it:

```bash
dotnet run --project ConsoleSearch
```

Point it somewhere else by passing a base URL: `dotnet run --project ConsoleSearch -- http://otherhost:5223/`.

The indexer prompts `Use SQLite (1) or Postgres (2) database?` on stdin before doing anything; ConsoleSearch does not prompt at all and goes straight to the query loop. To drive them non-interactively, pipe the answers — e.g. a SQLite indexer run, or a single search for `hello world` then quit:

```bash
echo 1 | dotnet run --project indexer
```

```bash
printf 'hello world\nq\n' | dotnet run --project ConsoleSearch
```

Run the service and the web client — the API first, then the frontend, in two terminals:

```bash
dotnet run --project API --launch-profile http
```

```bash
dotnet run --project SearchFrontend --launch-profile http
```

The API listens on `http://localhost:5223` (Swagger at `/swagger`), the load balancer on `http://localhost:5222`, the frontend on `http://localhost:5136`. The frontend points at the balancer through `ApiBaseUrl` in `SearchFrontend/wwwroot/appsettings.json`; `ConsoleSearch` still goes straight to the API on 5223 unless given another base URL. Both `LoadBalancer/Program.cs` and `API/Program.cs` have a `frontend` CORS policy set to `AllowAnyOrigin()`.

There is no test project, linter, or CI configuration in the repo.

## Configuration that must be edited before anything runs

These are hardcoded source constants (checked in with one developer's macOS paths), not settings files:

- `Shared/Paths.cs` — `SQLITE_DATABASE` (absolute path to the `.db` file) and `POSTGRES_DATABASE` (connection string, password is a literal `XXXX` placeholder).
- `indexer/Config.cs` — `FOLDER`, the root directory to crawl.

The indexer and the API read the same `Paths` constants: the indexer opens the database read-write-create, the API read-only. `API/Search` never touches `Paths` itself — `API/Program.cs` passes the path in as `SearchOptions.SqliteDatabasePath`, preferring `Configuration["Search:SqliteDatabasePath"]` when it is set. The clients have no database configuration at all: `ConsoleSearch` only knows the API base URL (a constant in `Program.cs`, overridable by the first command line argument) and the frontend reads `ApiBaseUrl` from `wwwroot/appsettings.json`.

## Architecture

### Schema (created by the indexer, never by ConsoleSearch)

```
document(id INTEGER PRIMARY KEY, url TEXT, idxTime TEXT, creationTime TEXT)
word(id INTEGER PRIMARY KEY, name)
Occ(wordId INTEGER, docId INTEGER)   -- the reverse index; INDEX word_index ON Occ(wordId)
```

**Destructive constructor:** `Indexer.DatabaseSqlite` / `Indexer.DatabasePostgres` run `DROP TABLE` + `CREATE TABLE` in their constructors. Merely instantiating an indexer database object wipes the existing index — there is no incremental indexing, and no code path that only reads from the indexer side.

### Two separate `IDatabase` interfaces

`Indexer.IDatabase` and `API.Search.IDatabase` are distinct types in distinct namespaces. The indexer side is write-oriented (`InsertDocument`, `InsertAllWords`, `InsertAllOcc`, `GetAllWords`, `DocumentCounts`) and has both a SQLite and a Postgres implementation; the search side is read-oriented (`GetWordIds`, `GetDocuments`, `GetDocDetails`, `getMissing`, `WordsFromIds`) and has SQLite only. A change to the storage contract usually means touching both interfaces.

### The service and its clients

`API/Search/ServiceCollectionExtensions.AddSearch(options)` is the service's whole wiring: `SearchOptions`, `IDatabase` -> `new DatabaseSqlite(options.SqliteDatabasePath)`, and `ISearchLogic` -> `SearchLogic`. All three are **singletons** — `DatabaseSqlite` holds one open connection and caches the entire `word` table for the lifetime of the process, so it must not be transient. That cache is also why a running API never sees words added by a later indexer run: restart the API after re-indexing.

`SearchController` takes `ISearchLogic` in its constructor and holds no logic of its own — it splits the query on spaces, calls `Search`, and maps `SearchResult`/`DocumentHit` onto `Shared.Contracts.SearchResponse`:

`GET /api/search/instances?query=hello+world&maxAmount=10&caseSensitive=false`

The wire contract is deliberately a different type from the internal `SearchResult`, but it lives in `Shared/Contracts` so the service and both clients compile against one definition instead of three copies. It uses **properties**, unlike `BEDocument`, whose public fields `System.Text.Json` would ignore — that is why the entity is not reused as the response.

`ConsoleSearch/Program.cs` registers an `HttpClient`, `SearchApiClient` and `App`. `ConsoleSearch/SearchApiClient` is the only class in the REPL that knows the service exists, and `App` prints exactly what it printed when the logic ran in-process. Since the search now lives in another process it can be unreachable, so `App` catches `HttpRequestException` and says so rather than ending the session.

### Load balancer

`LoadBalancer/Controllers/SearchController` round robins over a static `Backends` array with an `Interlocked` counter and redirects the caller to the chosen instance. It exposes the same route as the API (`api/search/instances`), so a client only changes its base URL to go through it.

Because it redirects, the browser ends up talking to the backend directly, so **both** the balancer and the API need CORS. Both use `AllowAnyOrigin()`.

Two mistakes here show up as a plain 404, not as a routing error:

- A controller must be a **public top level class**. Controller discovery skips nested types.
- Attribute routed controllers need `app.MapControllers()`; `MapControllerRoute` only adds the conventional `{controller}/{action}` route.

Run it alongside two API instances - the `http` profile is 5223 and `http2` is 5224, matching `Backends`:

```bash
dotnet run --project LoadBalancer --launch-profile http
```

### Frontend

`SearchFrontend` is a standalone Blazor WebAssembly app; it runs in the browser and reaches the API over HTTP, which is why the API needs the CORS policy. `Services/SearchApiClient` is the only class that knows the API exists, `Models/DocumentDisplay` holds presentation helpers over the shared contract (splitting a path into folder and file name), and `Pages/Home.razor` holds the whole UI — landing view, results view, the `Components/Logo` wordmark, and the case sensitivity toggle that mirrors the REPL's `/ChangeCaseSensitive`. The search field is a `<form>` with a submit button (the magnifier), so Enter searches.

### Indexing flow

`indexer/App.Run` → `Crawler.IndexFilesIn(root, [".txt"])`, recursing into subdirectories. Per file: increment `documentCounter` (that counter *is* the document id), insert the document, split the file on `Crawler.separators` into a `HashSet<string>` of distinct words, then insert only the words not seen before and one `Occ` row per distinct word in the file.

Word ids are allocated **in memory** by `Crawler` (`words.Count + 1`) and inserted as explicit ids — the database never generates them. The crawler's `words` dictionary is therefore the source of truth for the whole run, and only the delta (`newWords`) is written per file. Word extraction is presence-only: `Occ` has no term frequency, and no case folding or stemming happens — the index is case preserving, so case insensitive search is done on the search side by folding the cached `word` table.

### Search flow

A client sends the raw query string to `GET /api/search/instances`; the controller splits it on spaces and calls `SearchLogic.Search(query, maxAmount: 10, caseSensitive)`. `caseSensitive` is client state — REPL state on `App`, toggled with `/ChangeCaseSensitive [on|off]`, and the `Aa` chip in the frontend — defaulting to **off** in both. Inside the service:

1. `GetWordIds(query, caseSensitive, out ignored)` — on first call the implementation loads the *entire* `word` table into `mWords`, plus `mWordsIgnoringCase` (lower cased word → the ids of every casing of it), and caches both for the process lifetime; query words absent from the relevant lookup go into `Ignored`. Because the cache is loaded once, a search process will not see words added by a later indexer run. The return value maps each query word to the ids matching it — one id when case sensitive, potentially several (`the`, `The`, `THE`) when not. Query words that collapse to the same key are searched once.
2. `GetDocuments(wordIdGroups)` — one group of ids per query word. The core query counts each query word once, no matter how many of its casings the document holds: `SELECT docId, MAX(CASE WHEN wordId IN (<group 1>) THEN 1 ELSE 0 END) + ... AS count FROM Occ WHERE wordId IN (<all ids>) GROUP BY docId ORDER BY count DESC`. This is an OR search ranked by how many distinct query words a document contains.
3. For the top `maxAmount` doc ids: `GetDocDetails` plus `SearchLogic.MissingWords`, which asks `getMissing` for the ids absent from the document and reports a *query* word as missing when none of its ids are present — so a case insensitive search does not list each casing separately. Reported to the user alongside `Ignored`.

Read-side SQL is assembled by string concatenation of integer id lists (`AsString`) rather than parameters; write-side inserts use parameters and wrap each batch in a transaction.

## Conventions in this code base

- Fields on entity classes use the `m` prefix (`mId`, `mUrl`, `mIdxTime`); locals and privates often use `_`/`m` inconsistently — match the surrounding file rather than normalizing.
- `indexer/Renamer.cs` is an unused utility (appends `.txt` to extensionless files); it is invoked only from a commented-out line in `indexer/Program.cs`.
