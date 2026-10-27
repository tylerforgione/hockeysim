# Store each game in a SQLite league database, queried through Dapper

**Proposed.** Takes effect when the first multi-season feature is built, and
then replaces [ADR 0004](0004-local-save-format.md). Until then saves remain
one compressed JSON document, and the dependencies below are not approved.

ADR 0004 saves the whole world as one document, and loading replays every
completed match. That suits one season: a complete 32-team season is about
70 MB of JSON (3 MB compressed) and loads in about half a second. Over 20–30
seasons, with a development league and prospects' statistics from other
leagues, the history is estimated at 150–300 MB of JSON, all parsed, held in
memory, and rewritten on every save, when most of it only serves career pages
and all-time records. See
[league database for multi-season history](../future-features.md#league-database-for-multi-season-history).

## Decision

**Each game is one SQLite file**, accessed through `Microsoft.Data.Sqlite`.

- **Active and archived state.** The current season stays in memory, owned by
  Domain as now, and is replayed on load. Completed seasons are stored as fixed
  records and queried, never recalculated.
- **Small writes.** Completing a league day appends that day's results and the
  random state in one transaction, so reproducibility holds
  ([ADR 0002](0002-reproducible-saves.md)).
- **Save semantics stay the same.** Play runs against a working copy; Save
  copies it into the named slot (`VACUUM INTO` or the online backup API), so
  quitting without saving still discards progress, and a failed copy leaves the
  earlier save intact.
- **Contracts stay in Management.** `IGameSaveStore` is replaced by contracts
  shaped for appending a day's results and for history queries (career lines,
  leaders), still free of storage types. Infrastructure implements them.

**Queries are hand-written SQL mapped with Dapper**, kept in a small set of
query classes in Infrastructure. Each query is tested against a real temporary
database file, as save files are tested today.

**The schema is versioned** with `PRAGMA user_version`. Pre-release builds keep
rejecting another version as unsupported; once a stable-release compatibility
policy exists, upgrades are ordered SQL migration scripts run on open.

## Alternatives

- **EF Core.** Gives LINQ queries checked by the compiler, change tracking, and
  generated migrations. Change tracking serves loading, editing, and saving
  individual rows, but the active season is changed only through Domain and the
  archive only grows. EF would map the save model, not Domain types, so it
  replaces little of `GameSaveCapture` and `GameSaveRestorer`, while bulk
  inserts of a day's box scores are slow without extensions, and it is a much
  larger dependency with its own migration workflow.
- **linq2db.** LINQ queries and mapping without change tracking; the fallback if
  hand-written SQL proves error-prone.
- **LiteDB.** Managed document store with no native library, but weaker
  aggregation for leader and career queries.
- **DuckDB.** Strong at analytical aggregation, but suited to bulk loads rather
  than many small transactions, with a much larger native library. Could later
  serve heavy statistics pages over the archive; not the save store.
- **Keep JSON, one file per season.** Bounds what each save rewrites but
  still needs an index and loading code for cross-season queries, which SQLite
  provides.

## Consequences

- Two new runtime dependencies, `Microsoft.Data.Sqlite` (with its native SQLite
  library through SQLitePCLRaw) and `Dapper`, approved under the
  [technology stack](../tech-stack.md) policy before implementation.
- The native library ships beside the single-file executable like the graphics
  libraries ([ADR 0005](0005-self-contained-desktop-builds.md)); the release
  workflow and the per-platform smoke test must cover all four runtime
  identifiers.
- SQL strings are not checked by the compiler; a renamed column fails in tests,
  not at build time.
- The schema and the save model become two descriptions of saved data to keep in
  step, unlike ADR 0004's single serialized model.
- Before the schema is fixed, decide what a completed season keeps: every box
  score, or only season totals per player and team. That choice sizes the
  archive.
- On acceptance, update ADR 0004 to say it is replaced, the persistence row in
  the technology stack, the
  [randomness and saves](../architecture.md#randomness-and-saves) section, and
  the saves area document.
