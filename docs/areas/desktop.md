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
| `Game/` | `GameSession` (commands, snapshots, unsaved progress) and the in-game shell: menu bar, sections and page tabs (`ShellPage`, `NavigationItemViewModel`), the team banner (`TeamBannerViewModel`), and the status bar |
| `Home/`, `Inbox/`, `Roster/`, `Lines/`, `TeamStatistics/`, `Injuries/`, `Teams/`, `Standings/`, `Playoffs/`, `Schedule/` | The shell's pages |
| `Players/` | Player profile, regular-season or playoff totals as displayed (`PlayerSeasonTotals`, chosen by `StatisticsSet`), and shared player and injury formatting (`PlayerDisplay`, `InjuryDisplay`) |
| `Playoffs/PlayoffDisplay.cs` | Shared round, seed, and series-status formatting, and finding a team's playoff game by date |
| `TeamStatistics/TeamStatisticsDisplay.cs`, `Injuries/InjuryReportRowViewModel.cs` | Team statistic and injury report formatting |
| `Schedule/MatchDetail*`, `MatchDisplay.cs` | The box score with its summaries, and shared match and statistic formatting |
| `Confirmation/` | The confirmation shown over every screen |
| `Theme/` | Palette, control styles (including the menu bar, banner, tabs, and status bar), menu-bar icons, the bundled PT Sans fonts and their licence (`Fonts/`), and `TeamPalette`, which colours the window with the managed team |

## Tests

`tests/HockeySim.Desktop.Tests/` tests view models against real Management
games, plus a headless Avalonia walkthrough.

| File | Covers |
| --- | --- |
| `MainWindowViewTests.cs` | Headless walkthrough of the menu bar and every page, team colours, saving, loading, closing |
| `NewGameViewModelTests.cs`, `GameShellViewModelTests.cs`, `TeamBrowsingTests.cs` | Setup; sections, tabs, planned pages, the banner, and the status bar; browsing teams |
| `LinesPageViewModelTests.cs` | Lineup and unit editing, any-role choices, out-of-position warnings |
| `SeasonAdvancementTests.cs` | Continue, refresh after a day, failures, box scores, the preseason labels, the schedule's phases and preseason results |
| `PlayoffDisplayTests.cs` | A shared season played through the shell: the bracket before and at each playoff stage, playoff schedule rows and box scores, switching statistic sets, the completed season and champion |
| `StandingsAndStatisticsTests.cs` | Standings scopes including the wild card, playoff markers, season totals |
| `AdvancedStatisticsDisplayTests.cs` | Statistic formatting, box-score summaries and new columns, basic and advanced roster views, the profile line, team statistics |
| `SaveAndLoadTests.cs` | Save and load screens, unsaved progress, confirmations |
| `PlayerBiographyDisplayTests.cs`, `AppVersionTests.cs` | Formatting, version display |
| `TeamPaletteTests.cs` | Readable text on team colours |
| `InjuryDisplayTests.cs` | Continue waiting for replacements, the status bar's lineup state, Lines-page injury warnings, roster markers, the injury report, the profile's health |
| `GameTestData.cs`, `TestMatchSimulators.cs`, `TemporarySaveDirectory.cs`, `InjuredPlayerReplacement.cs`, `PreseasonPlay.cs` | Builders (sessions start on opening day unless a test needs the preseason), test engines, and replacing injured players between days |

## Behaviour

Desktop wires a Management game manager at startup. After the new-game screen
starts a game, a `GameSession` forwards commands (lineup changes, reading
messages, advancing a league day) to Management and publishes each resulting
snapshot to the in-game shell and its pages.

The shell fills the window. The menu bar holds back, home, and forward buttons
(back and forward stay disabled until [navigation history](https://github.com/tylerforgione/hockeysim/issues/62)),
then text menus: Game (save, load, quit to menu), Team (roster, lines, schedule,
team statistics, injuries), League (standings, teams, league leaders, playoff
picture, schedule), Stats (player statistics, team statistics, league leaders),
Club (staff, transactions), and Inbox with its unread count. Pages that do not
exist yet are listed but disabled. Every menu and item has an access key, and
save and load show their shortcuts (Ctrl+S and Ctrl+O; Cmd on macOS). Load
opens the saved games, whose back button returns to the game. On the right
are the date, the days to the managed team's next match in any phase, then the
days to opening day in the preseason, to the end of the regular season, or the
playoff round under way (once the champion is crowned, the champion instead of
both), and Continue, a split button whose arrow stays disabled until
[simulating ahead](https://github.com/tylerforgione/hockeysim/issues/85). Under
the menu bar is the managed team's banner in its colours: crest, name, record,
points, division and conference rank, and the next match, last result, and
streak. A page may show its own banner instead; none does yet. Below the banner,
the current section's pages are tabs, with the page's subtitle on the right.
Home, Team, League, and Inbox are sections; the team and league schedules are
one page, and opening it from Team shows the managed team. The status bar shows
the game's name and save state, whether any dressed player is out injured
(opening Lines), the newest unread message with how many more are unread
(opening it), and the version.

Only the managed team's lineup is editable, and
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
replaced player's unit slots. Unsaved edits survive browsing other teams. Continue plays the
current league day off the UI thread; the session rejects a second request while
one runs and then publishes Management's latest snapshot, since a command
issued meanwhile waits on Management's lock and may have produced newer state.
While Management's `PlayersToReplace` is not empty, Continue is disabled and a
banner under the page tabs names the injured players to replace, with a button
to the Lines page; it clears once a saved lineup leaves them out.
A failed day is shown as an error banner; Management applied nothing, so the
pages still show the unplayed day. Continue's tooltip counts the day's
preseason, league, or playoff matches. The home page starts
with a season tile: preseason or regular-season matches played (the preseason
counting toward nothing), the managed team's playoff series and its status,
how far it went once eliminated, or that it did not qualify. Its next match is
marked as a preseason match or by playoff round, game number, and series status,
and its latest results list the last played day in any phase, each opening the
box score. Once the regular season ends the standings page shows the final
standings. Once the champion is crowned the season is complete: the menu bar
names the champion, Continue is disabled, and every page remains browsable. The
schedule page lists one team's matches in every phase in date order (its seven
preseason matches, its 84 regular-season matches, and its playoff games as
they are scheduled), each labelled PRE, REG, or by playoff round and game
("R1 G3"), with results; its subtitle counts regular-season matches played. It
opens a completed match's score, decision, and box score; these are
single-match figures, kept apart from season totals. The playoff picture page shows the
bracket: a column per round with each series in bracket order, each team's seed
(division initial and finish, such as N1, or WC1 and WC2), its wins, the
series status, and the winner in bold and the loser dimmed; a round not yet
formed is shown as to be decided. Selecting a series lists its games, played and
scheduled, each opening on the schedule page; the managed team's latest series
is selected at first. Before the playoffs the page explains when they begin and
who qualifies, and once they end a banner names the champion. The box
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
The standings page presents Management's division, wild-card, conference, or
league tables as ranked, without re-sorting them. The wild-card view lists each
division's top three, then each conference's wild-card race with a line under
the second wild card. Every table marks each team's playoff status with the
NHL's letter (x, y, z, p, or e) before its name, with the meaning in a tooltip
and a legend beneath the tables. The team statistics page lists the managed
team's regular-season power-play, penalty-kill, and faceoff percentages and its
five-on-five Corsi, Fenwick, shot, and xG shares. The injuries page is the managed team's
injury report: each injury that has not healed, players who cannot play first,
with its status and expected return. Other teams' figures wait for
[team pages](https://github.com/tylerforgione/hockeysim/issues/63). Roster tables mark an injured player OUT or INJ beside
their name, with the details in a tooltip, and the player profile shows the
player's health: each injury, its expected return, and either that the player
cannot play or the rating points it costs while playing through. The exact
recovery time, wear, and durability are never shown. Once the playoffs start,
each roster switches between regular-season and playoff statistics from the
skaters panel's header; the choice changes both tables and the profile together, holds across
teams and days like the column choice, and shows zeros and dashes for a team or
player without playoff games. Roster tables for every team
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
Game menu's Save game item saves under a typed or chosen name, and the Load
Game screen, from the startup menu or the Game menu, lists the saves. A `GameSession` tracks whether the game
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
