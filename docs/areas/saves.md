# Saves

Saving and loading games. Management owns the save model and the persistence
contracts; Infrastructure implements them for local files. Design decisions:
[ADR 0002](../adr/0002-reproducible-saves.md) (reproducible saves) and
[ADR 0004](../adr/0004-local-save-format.md) (local save format). The
cross-cutting rules are in
[architecture](../architecture.md#randomness-and-saves).

## Code map

| File | Holds |
| --- | --- |
| `Management/Saves/GameSave.cs` | The detached save model |
| `Management/Saves/GameSaveCapture.cs` | Copies the game world into a save |
| `Management/Saves/GameSaveRestorer.cs` | Rebuilds and validates a game from a save |
| `Management/Saves/IGameSaveStore.cs`, `ISavedGameLibrary.cs` | The persistence contracts |
| `Management/Saves/SaveName.cs`, `GameSaveException.cs` | Save names and save errors |
| `Infrastructure/Saves/GameSaveFile.cs` | One save file: header, format version, atomic write |
| `Infrastructure/Saves/GameSaveDirectory.cs` | Named saves in the application-data folder |
| `Infrastructure/Saves/GameSaveJsonContext.cs`, `IdentityJsonConverters.cs` | Source-generated JSON |

Management paths are under `src/HockeySim.Management/`, Infrastructure paths
under `src/HockeySim.Infrastructure/`. A change to what a save holds usually
touches `GameSave`, `GameSaveCapture`, `GameSaveRestorer`, and the format
version in `GameSaveFile`.

## Tests

| File | Covers |
| --- | --- |
| `Management.Tests/SaveAndLoadTests.cs` | Round trips, continued play matching uninterrupted play, every kind of invalid save |
| `Management.Tests/SaveNameTests.cs` | Accepted names, case-insensitivity |
| `Infrastructure.Tests/GameSaveFileTests.cs` | Real files: header, versions, damaged files, failed writes |
| `Infrastructure.Tests/GameSaveDirectoryTests.cs` | Listing and finding named saves |
| `Management.Tests/SaveTestData.cs`, `Infrastructure.Tests/SaveTestGames.cs` | Builders |

## Behaviour

Management saves and loads games through its own contracts in `Saves/`: the
`GameSave` model and the `IGameSaveStore` interface. `SaveGame` copies the
world, lineups (with their units and extra attackers), schedule, current date, completed matches (with
their full box scores, on-ice and team shot totals by situation, scoring and
penalty summaries, injuries, and hidden wear, times in whole seconds), inbox, and random state into a
detached save, then hands it to the store. The rest of the play-by-play is not
saved. Save format version 4 added the event engine's box-score statistics,
version 5 penalty minutes, power-play and shorthanded goals and assists, and
power-play opportunities, version 6 empty-net goals, version 7 the shot
totals and summaries, and version 8 injuries and wear. The shot totals roughly triple a save's size: a complete
season is about 3 MB compressed (70 MB of JSON) rather than 1 MB, and loads in
about half a second. `LoadGame` rebuilds the
league through Domain constructors and replays each saved league day through
`Season.CompleteDay`, so a loaded game is held to the same invariants as a
played one, and team records, season statistics, and standings are derived
from the history rather than read from the file, as is each player's health. The active game is replaced
only after the whole save has been rebuilt; an unreadable, unsupported, or
invalid save raises a `GameSaveException` and leaves the active game
unchanged. Continuing a loaded game with the same commands gives the same
results as uninterrupted play within one engine version.
Infrastructure's `GameSaveFile` implements the store for one local file. It
writes the format name and version ahead of the game, serializes the save model
with source-generated System.Text.Json, and writes through a temporary file
that replaces the earlier save only once complete. Management's
`ISavedGameLibrary` lists named saves and opens the store for a `SaveName`;
Infrastructure's `GameSaveDirectory` implements it as one file per save in the
user's application-data folder, named after the save. Save names are limited to
characters that are portable in file names and ignore letter case, so the same
saves appear on every file system. Storage failures (a missing file, a locked
folder, a full disk) surface as `GameSaveStorageException`, another
`GameSaveException`, so callers see only Management's types.
