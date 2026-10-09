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
| `Players/` | Player profile, season totals as displayed (`PlayerSeasonTotals`), and shared player and injury formatting (`PlayerDisplay`, `InjuryDisplay`) |
| `Roster/TeamStatisticsDisplay.cs`, `InjuryReportRowViewModel.cs` | The team statistics strip and injury report above each roster |
| `Schedule/MatchDetail*`, `MatchDisplay.cs` | The box score with its summaries, and shared match and statistic formatting |
| `Confirmation/` | The confirmation shown over every screen |
| `Theme/` | Palette, control styles, icons, the bundled PT Sans fonts and their licence (`Fonts/`), and `TeamPalette`, which colours the window with the managed team |

## Tests

`tests/HockeySim.Desktop.Tests/` tests view models against real Management
games, plus a headless Avalonia walkthrough.

| File | Covers |
| --- | --- |
| `MainWindowViewTests.cs` | Headless walkthrough of every page, team colours, saving, loading, closing |
| `NewGameViewModelTests.cs`, `GameShellViewModelTests.cs`, `TeamBrowsingTests.cs` | Setup, navigation, browsing teams |
| `LinesPageViewModelTests.cs` | Lineup and unit editing, any-role choices, out-of-position warnings |
| `SeasonAdvancementTests.cs` | Continue, refresh after a day, failures, box scores, a full season |
| `StandingsAndStatisticsTests.cs` | Standings scopes, season totals |
| `AdvancedStatisticsDisplayTests.cs` | Statistic formatting, box-score summaries and new columns, basic and advanced roster views, the profile line, the team strip |
| `SaveAndLoadTests.cs` | Save and load screens, unsaved progress, confirmations |
| `PlayerBiographyDisplayTests.cs`, `AppVersionTests.cs` | Formatting, version display |
| `TeamPaletteTests.cs` | Readable text on team colours |
| `InjuryDisplayTests.cs` | Continue waiting for replacements, Lines-page injury warnings, roster markers, the injury report, the profile's health |
| `GameTestData.cs`, `TestMatchSimulators.cs`, `TemporarySaveDirectory.cs`, `InjuredPlayerReplacement.cs` | Builders, test engines, and replacing injured players between days |

## Behaviour

Desktop wires a Management game manager at startup. After the new-game screen
starts a game, a `GameSession` forwards commands (lineup changes, reading
messages, advancing a league day) to Management and publishes each resulting
snapshot to the in-game shell's feature pages: home, inbox, roster, lines,
league teams, standings, and schedule. Only the managed team's lineup is editable, and
Management validates every change. The lines page shows any team's lineup, read-only
for other clubs, on tabs for even strength, power play, penalty kill, and the
other situations (4-on-4, 3-on-3, extra attacker); each unit shows its forwards
in front of its defence. Every skater slot offers every skater, and goalie slots
only goalies; each choice shows the player's natural position.
A slot warns beneath its choice when the player is injured (in red when they
cannot play, in amber when they are playing through it) and when they are out of
position (a defenceman at forward or a forward on defence is named as such;
extra-attacker slots never are), and
each choice marks an injured player OUT or INJ. Handedness is not shown or
warned about there; the roster and player profile show it. AI teams' lineups are
shown as they dress today, with injured players already replaced.
Choosing a player already in the same line set, unit, or
extra-attacker pair swaps the two; dressing a scratched player hands them the
replaced player's unit slots. Unsaved edits survive browsing other teams. The title bar's Continue button plays the
current league day off the UI thread; the session rejects a second request while
one runs and then publishes Management's latest snapshot, since a command
issued meanwhile waits on Management's lock and may have produced newer state.
While Management's `PlayersToReplace` is not empty, Continue is disabled and a
banner under the title bar names the injured players to replace, with a button
to the Lines page; it clears once a saved lineup leaves them out.
A failed day is shown as an error banner; Management applied nothing, so the
pages still show the unplayed day. Once the season is complete, Continue is
disabled and every page remains browsable. The schedule page lists one team's
84 matches with results and opens a completed match's score, decision, and box
score; these are single-match figures, kept apart from season totals. The box
score starts with the scoring summary (each goal's period and time, team,
scorer, assists, PP/SH/PS/EN marker, and the running score, away first) and the
penalty summary (period and time, team, player, infraction, and length). It then
lists each team's skaters (goals, assists, points, plus/minus, time on ice,
shots, shot attempts, xG, hits, blocks, faceoffs won and lost, takeaways,
giveaways, penalty minutes, power-play and shorthanded goals, and five-on-five
on-ice Corsi and xG percentages) and starting goalie (shots and goals against,
saves, save percentage, xG against, goals saved above expected, time on ice), and
each team's power play (goals of opportunities) and penalty minutes under its
shots; the two teams are stacked, away first, because each table needs the full
width.
The standings page presents Management's division, conference, or league tables
as ranked, without re-sorting them. Each roster starts with a strip of the team's
season statistics: power-play, penalty-kill, and faceoff percentages and its
five-on-five Corsi, Fenwick, shot, and xG shares, then the team's injury report:
each injury that has not healed, players who cannot play first, with its status
and expected return. Roster tables mark an injured player OUT or INJ beside
their name, with the details in a tooltip, and the player profile shows the
player's health: each injury, its expected return, and either that the player
cannot play or the rating points it costs while playing through. The exact
recovery time, wear, and durability are never shown. Roster tables for every team
switch between ratings, basic season totals (skater GP, G, A, P, +/-, PIM, PPP,
SHP, shots, TOI per game, FO%; goalie GP, SA, SV, GA, SV%, GAA, shutouts), and
advanced figures (skater five-on-five on-ice Corsi and Fenwick for, against, and
percentage, xG for, against, and percentage, and individual xG; goalie time in
net, xG against, goals against, GSAx, and time per start). The player profile
always shows the full statistic line in groups, scrolling with the ratings. A
player who has not appeared shows zeros, and every percentage, average, and rate
is a dash until it is defined. Advanced figures are five-on-five only; views by
situation are a [future feature](../future-features.md#situational-statistics-views).
Pages rebuild from whichever snapshot the session last published.
The look is a dense "front office": square panels with 1 px dark borders and
6 px gutters on a slate palette, PT Sans for body text (its digits are
fixed-width, so numbers line up) and PT Sans Narrow for headings, panel titles,
and large figures. Both fonts are bundled under the SIL Open Font License, whose
text is copied beside the executable. Panel headers are upper case on the team's
primary colour with a 2 px underline in its secondary colour. Segmented button
groups switch views; the selected button and primary actions are the secondary
colour with primary-coloured text. Tables are zebra-striped, with the managed
team's or selected player's row highlighted. Copy is labels and data only. The
palette and control styles live in `Theme/`. The team colours are dynamic
resources: while a game is open, `TeamPalette` sets them on the main window from
the managed team's colours, with light or dark text on the primary, whichever
contrasts more; before the first game they are the league's black and silver.

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
