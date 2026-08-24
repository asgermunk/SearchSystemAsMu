# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A proof-of-concept search engine (course code base for "Arkitektur principper"), consisting of two .NET 10 console programs plus a shared class library:

- `indexer/` — crawls a folder recursively, extracts words from `.txt` files, writes a reverse index to a database.
- `ConsoleSearch/` — interactive REPL that queries that reverse index.
- `Shared/` — `Paths` (connection strings) and `BEDocument` (business entity for a document). Nothing else is shared.

Each program picks its database engine at startup (SQLite or Postgres) via a console prompt.

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

Both prompt `Use SQLite (1) or Postgres (2) database?` on stdin before doing anything. To drive them non-interactively, pipe the answers — e.g. SQLite indexer run, or a single SQLite search for `hello world` then quit:

```bash
echo 1 | dotnet run --project indexer
```

```bash
printf '1\nhello world\nq\n' | dotnet run --project ConsoleSearch
```

There is no test project, linter, or CI configuration in the repo.

## Configuration that must be edited before anything runs

These are hardcoded source constants (checked in with one developer's macOS paths), not settings files:

- `Shared/Paths.cs` — `SQLITE_DATABASE` (absolute path to the `.db` file) and `POSTGRES_DATABASE` (connection string, password is a literal `XXXX` placeholder).
- `indexer/Config.cs` — `FOLDER`, the root directory to crawl.

Both programs read the same `Paths` constants: the indexer opens the database read-write-create, ConsoleSearch read-only.

## Architecture

### Schema (created by the indexer, never by ConsoleSearch)

```
document(id INTEGER PRIMARY KEY, url TEXT, idxTime TEXT, creationTime TEXT)
word(id INTEGER PRIMARY KEY, name)
Occ(wordId INTEGER, docId INTEGER)   -- the reverse index; INDEX word_index ON Occ(wordId)
```

**Destructive constructor:** `Indexer.DatabaseSqlite` / `Indexer.DatabasePostgres` run `DROP TABLE` + `CREATE TABLE` in their constructors. Merely instantiating an indexer database object wipes the existing index — there is no incremental indexing, and no code path that only reads from the indexer side.

### Two separate `IDatabase` interfaces

`Indexer.IDatabase` and `ConsoleSearch.IDatabase` are distinct types in distinct namespaces, each with a SQLite and a Postgres implementation (four files total). The indexer side is write-oriented (`InsertDocument`, `InsertAllWords`, `InsertAllOcc`, `GetAllWords`, `DocumentCounts`); the search side is read-oriented (`GetWordIds`, `GetDocuments`, `GetDocDetails`, `getMissing`, `WordsFromIds`). A change to the storage contract usually means touching both interfaces and all four implementations, which are near-duplicates of each other per engine.

### Indexing flow

`indexer/App.Run` → `Crawler.IndexFilesIn(root, [".txt"])`, recursing into subdirectories. Per file: increment `documentCounter` (that counter *is* the document id), insert the document, split the file on `Crawler.separators` into a `HashSet<string>` of distinct words, then insert only the words not seen before and one `Occ` row per distinct word in the file.

Word ids are allocated **in memory** by `Crawler` (`words.Count + 1`) and inserted as explicit ids — the database never generates them. The crawler's `words` dictionary is therefore the source of truth for the whole run, and only the delta (`newWords`) is written per file. Word extraction is presence-only: `Occ` has no term frequency, and no case folding or stemming happens.

### Search flow

`ConsoleSearch/App` reads a line, splits on spaces, calls `SearchLogic.Search(query, maxAmount: 10)`:

1. `GetWordIds` — on first call the implementation loads the *entire* `word` table into `mWords` and caches it for the process lifetime; query words absent from that table go into `Ignored`. Because the cache is loaded once, a search process will not see words added by a later indexer run.
2. `GetDocuments(wordIds)` — the core query: `SELECT docId, COUNT(wordId) FROM Occ WHERE wordId IN (...) GROUP BY docId ORDER BY count DESC`. This is an OR search ranked by how many distinct query words a document contains.
3. For the top `maxAmount` doc ids: `GetDocDetails` plus `getMissing` (query words with no `Occ` row for that document), reported to the user alongside `Ignored`.

Read-side SQL is assembled by string concatenation of integer id lists (`AsString`) rather than parameters; write-side inserts use parameters and wrap each batch in a transaction.

## Conventions in this code base

- Fields on entity classes use the `m` prefix (`mId`, `mUrl`, `mIdxTime`); locals and privates often use `_`/`m` inconsistently — match the surrounding file rather than normalizing.
- `indexer/Renamer.cs` is an unused utility (appends `.txt` to extensionless files); it is invoked only from a commented-out line in `indexer/Program.cs`.
