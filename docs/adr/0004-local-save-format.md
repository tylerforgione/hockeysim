# Save games as one compressed JSON document, rebuilt by replaying history

A save must let a game resume exactly, including the hidden random state
([reproducible saves](0002-reproducible-saves.md)), and must never load part of
a world. The game reads and writes the whole world at once; nothing queries saved
history without loading the game, so a queryable store such as SQLite would add
a dependency and a schema to keep in step with the domain without serving an
access pattern. A save is therefore one file per game, holding one JSON document
serialized with System.Text.Json and compressed with Brotli, both part of .NET.

A complete 32-team season (1,344 completed matches with box scores) measured
4.6 MB as JSON, 1.1 MB with gzip, and 158 KB with Brotli at its default
`Optimal` level, which compressed in about 5 ms. Brotli's larger window finds
the player identities that repeat across many days of box scores, which gzip's
32 KB window cannot. The `SmallestSize` level saved a further third but took
several seconds. Saving a completed season took about 60 ms, and loading it
about 100 ms. Saves are not meant to be edited by hand, and decompressing a
file gives readable JSON for investigation.

The document starts with a format name and an integer format version ahead of
the game, so a save from another version is reported as unsupported rather
than as damaged. Pre-release builds read only their own version and do not
migrate older saves; the version must change whenever the saved shape or its
serialization changes. A stable-release compatibility policy is still to be
decided.

Management owns the save model (`GameSave`) and the store contract
(`IGameSaveStore`); Infrastructure serializes that model directly rather than
mapping it to separate file types. This keeps one description of the saved
shape, at the cost that renaming a save-model property changes the file
format. Reading enforces nullable annotations and required values, and enums
are written by name.

The save holds the world, lineups, schedule, current date, completed matches,
inbox, and random state, but not team records, season statistics, or
standings. Loading rebuilds the world through Domain constructors and replays
each saved league day through the season's normal day completion. That
revalidates every result and derives the totals from the history, so they
cannot disagree with it, at the cost of slower loading as history grows. The
active game is replaced only after the whole save has been rebuilt.

A save is written to a temporary file in the same directory, flushed to disk,
and then moved over the earlier save, so a failed or interrupted save leaves
the earlier one intact.

This format suits a single season. Revisit it when multi-season history
arrives: rewriting, parsing, and holding every past season in memory on each
save and load will not scale to decades of history across several leagues. See
[league database for multi-season history](../future-features.md#league-database-for-multi-season-history).
