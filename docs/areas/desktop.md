# Desktop

`HockeySim.Desktop`: the Avalonia app. It composes Management with
Infrastructure and Simulation at startup, sends commands through a
`GameSession`, and shows Management's snapshots. It never calls the engine,
storage, or Domain mutation directly. See
[ADR 0003](../adr/0003-avalonia-desktop.md) and, for release builds,
[ADR 0005](../adr/0005-self-contained-desktop-builds.md).

## Code map

Paths are under `src/HockeySim.Desktop/`. Each page folder holds its view and
view model together.

| Folder | Holds |
| --- | --- |
| `Program.cs`, `App.axaml*`, `AppVersion.cs` | Startup and composition |
| `Main/` | The window and top-level screen choice, with discard confirmation |
| `Startup/`, `NewGame/`, `Saves/` | The startup menu, new-game setup, save and load screens |
| `Game/` | `GameSession` (commands, snapshots, unsaved progress) and the in-game shell |
| `Home/`, `Inbox/`, `Roster/`, `Lines/`, `Teams/`, `Standings/`, `Schedule/` | The shell's pages |
| `Players/` | Player profile and shared player formatting (`PlayerDisplay`) |
| `Schedule/MatchDetail*`, `MatchDisplay.cs` | The box score and shared match formatting |
| `Confirmation/` | The confirmation shown over every screen |
| `Theme/` | Colours, control styles, icons |

## Tests

`tests/HockeySim.Desktop.Tests/` tests view models against real Management
games, plus a headless Avalonia walkthrough.

| File | Covers |
| --- | --- |
| `MainWindowViewTests.cs` | Headless walkthrough of every page, saving, loading, closing |
| `NewGameViewModelTests.cs`, `GameShellViewModelTests.cs`, `TeamBrowsingTests.cs` | Setup, navigation, browsing teams |
| `LinesPageViewModelTests.cs` | Lineup and unit editing |
| `SeasonAdvancementTests.cs` | Continue, refresh after a day, failures, box scores, a full season |
| `StandingsAndStatisticsTests.cs` | Standings scopes, season totals |
| `SaveAndLoadTests.cs` | Save and load screens, unsaved progress, confirmations |
| `PlayerBiographyDisplayTests.cs`, `AppVersionTests.cs` | Formatting, version display |
| `GameTestData.cs`, `TestMatchSimulators.cs`, `TemporarySaveDirectory.cs` | Builders and test engines |

## Behaviour

Desktop wires a Management game manager at startup. After the new-game screen
starts a game, a `GameSession` forwards commands (lineup changes, reading
messages, advancing a league day) to Management and publishes each resulting
snapshot to the in-game shell's feature pages: home, inbox, roster, lines,
league teams, standings, and schedule. Only the managed team's lineup is editable, and
Management validates every change. The lines page shows any team's lineup, read-only
for other clubs, on tabs for even strength, power play, penalty kill, and the
other situations (4-on-4, 3-on-3, extra attacker); each unit shows its forwards
in front of its defence. Choosing a player already in the same line set, unit, or
extra-attacker pair swaps the two; dressing a scratched player hands them the
replaced player's unit slots. Unsaved edits survive browsing other teams. The title bar's Continue button plays the
current league day off the UI thread; the session rejects a second request while
one runs and then publishes Management's latest snapshot, since a command
issued meanwhile waits on Management's lock and may have produced newer state.
A failed day is shown as an error banner; Management applied nothing, so the
pages still show the unplayed day. Once the season is complete, Continue is
disabled and every page remains browsable. The schedule page lists one team's
84 matches with results and opens a completed match's score, decision, and box
score; these are single-match figures, kept apart from season totals. The box
score lists each team's skaters (goals, assists, points, plus/minus, time on ice,
shots, shot attempts, xG, hits, blocks, faceoffs won and lost, takeaways,
giveaways, penalty minutes, power-play and shorthanded goals) and starting goalie
(shots and goals against, saves, save percentage, xG against, time on ice), and
each team's power play (goals of opportunities) and penalty minutes under its
shots; the two teams are stacked, away first, because each table needs the full
width.
The standings page presents Management's division, conference, or league tables
as ranked, without re-sorting them. Roster tables for every team switch between
ratings and current-season totals (skater GP/G/A/P, goalie GP/SA/SV/GA/SV%), and
the player profile always shows the totals. A player who has not appeared shows
zeros, and a save percentage or points percentage is a dash until it is defined.
Pages rebuild from whichever snapshot the session last published.
Colours and control styles live in `HockeySim.Desktop/Theme/`; team identity
colours are dynamic resources so a chosen team's colours can replace the
league defaults later.

Desktop composes `GameSaveDirectory` with the game manager at startup. The
title bar's Save Game button saves under a typed or chosen name, and the startup
menu's Load Game screen lists the saves. A `GameSession` tracks whether the game
has changed since it was last saved or loaded; overwriting an existing save, and
anything that would discard unsaved progress (loading, starting a new game,
exiting, or closing the window), asks first through a confirmation shown over
every screen. Saving and leaving for the menu wait while a day is being played,
so a day cannot complete into a game that has since been replaced. Save and load
failures are shown where they happened; a failed load leaves the game in
progress as it was. A successful load builds a new session and shell from the
loaded snapshot rather than refreshing the old pages, because a loaded game may
be a different league with different teams and players, and no selection or
unapplied lineup edit should carry over. Saving stays available once the
season is complete. The startup menu shows the running version.

Releases publish `HockeySim.Desktop` as a self-contained, single-file build
named `HockeySim` for each supported runtime identifier; ordinary builds stay
framework-dependent. See
[the deployment decision](../adr/0005-self-contained-desktop-builds.md).
