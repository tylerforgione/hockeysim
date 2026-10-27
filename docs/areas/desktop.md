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
| `Startup/`, `NewGame/`, `Saves/` | The startup menu (`StartupViewModel`: Continue, the recent games, quit), new-game setup, save and load screens. `Saves/` holds `SavedGameLoader` (loading with the discard confirmation and failure wording, shared by the load screen and the menu), `SavedGameRowViewModel` (one save's details formatting), and the shared table pieces `SavedGameRowView`, `SavedGameHeaderView`, `SavedGameColumns`. `NewGame/NewGameViewModel.cs` holds the steps (`NewGameStep`, `NewGameStepViewModel`), the team list (`ConferenceOptionViewModel`, `DivisionOptionViewModel`, `TeamOptionViewModel`), and the team preview (`TeamPreviewViewModel`, `KeyPlayerViewModel`, `RatingBarViewModel`) |
| `Game/` | `GameSession` (commands, snapshots, unsaved progress, runs of days) and the in-game shell: menu bar, Continue and its menu (`SimulateAheadOptionViewModel`), sections and page tabs (`ShellPage`, `NavigationItemViewModel`, `IPageTab`), the team banner (`TeamBannerViewModel`), and the status bar |
| `Home/`, `Inbox/`, `Roster/`, `Lines/`, `TeamStatistics/`, `Injuries/`, `Teams/`, `Standings/`, `Playoffs/`, `Schedule/` | The shell's pages. `Teams/TeamsPageViewModel.cs` also holds `TeamEntryViewModel.ForLeague`, the team picker list the Teams, Lines, and Schedule pages share |
| `Home/` | The home page: `HomePageViewModel` builds five self-contained panels, each a view and view model that takes the `GameSession` and only the shortcuts it needs and refreshes itself: `UpcomingGamesPanel…`, `StandingsPanel…` (scope held across refreshes), `TaleOfTheTapePanel…` (the managed team against its next opponent), `RecentResultsPanel…`, `TeamLeadersPanel…` (with the minimum-games choice). `HomeStatistics` picks the statistics set the tape and leaders share and finds its figures |
| `Roster/` | The roster shared by the Roster and Teams pages: `TeamRosterView`/`TeamRosterViewModel` (player groups, column views, selection, the links to a player's profile), `SkaterTableView` and `GoalieTableView` (each table's four column views, double-clicking a row opening the profile through `RosterRowActivation`), `PlayerStripView`/`PlayerStripViewModel` (the selected player's strip), `PlayerRowViewModel` (one row) |
| `Lines/` | The Lines page: `LinesPageViewModel` (team, tabs, save and revert, opening a profile), `LineupEditorViewModel` (one team's slots, swap rules, scratches, and the drag: `BeginDrag`, `CanDrop`, `Drop`, `EndDrag`), `LineupSlotViewModel` (one slot's choice and warnings), `PlayerOptionViewModel`, and `LineupDragDrop`, the in-page drag: a press on a grip, the ghost that follows the pointer, and the hit test that drops on a slot. The view's code-behind opens profiles from the name links |
| `Players/` | The player profile and what it shows: `PlayerProfilePageView`/`PlayerProfilePageViewModel` (the shell page: tabs, statistic set, biography, usage, ratings, health), `PlayerProfileTab`, `PlayerBannerViewModel` (the banner, painted in the player's team colours), `PlayerGameLog` (the game log's rows), `RatingViewModels.cs`, `PlayerInjuryViewModel`; regular-season or playoff totals as displayed (`PlayerSeasonTotals`, chosen by `StatisticsSet`, which falls back to the regular season before the playoffs); and shared player and injury formatting (`PlayerDisplay`, `InjuryDisplay`) |
| `Standings/StandingsTableBuilder.cs` | Builds the standings tables per scope from Management's rankings, for the Standings page and the home page's standings panel |
| `Standings/TeamForm.cs`, `Standings/PlayoffStatusDisplay.cs` | A team's last ten games (and the points they earned) and streak (also on the banner), and the playoff-status letters and legend |
| `Playoffs/PlayoffDisplay.cs` | Shared round, seed, and series-status formatting, and finding a team's playoff game by date |
| `TeamStatistics/TeamStatisticsDisplay.cs`, `Injuries/InjuryReportRowViewModel.cs` | Team statistic and injury report formatting |
| `Schedule/MatchDetail*`, `MatchSummaryTables.cs`, `MatchDisplay.cs` | The box score: its banner, Summary and Players tabs, the summary's tables, and shared match and statistic formatting (including ordinals and the PRE, REG, and playoff game labels) |
| `Schedule/TeamResults.cs` | Picking a team's results: whether a result involves a team, and each team's results oldest first |
| `Tables/` | Sortable tables: `TableSort` (column key and direction), `TableColumn` (a column's raw sort key and first direction), `TableSorting` (click and ordering, ties keep default order, dashes last), and the `SortHeader` header button. Per-table column sets: `Roster/RosterTableColumns.cs` (with `RosterSorts`, a roster's remembered skater and goalie sorts), `Schedule/ScheduleTableColumns.cs`, `Schedule/BoxScoreTableColumns.cs` |
| `Confirmation/` | The confirmation shown over every screen |
| `Theme/` | Palette, control styles (including the menu bar, banners, tabs, and status bar), menu-bar icons, the bundled PT Sans fonts and their licence (`Fonts/`), `TeamPalette`, which colours the window with the managed team, `ColourBrushConverter`, which paints a team's colour, and `ReadableTextBrushConverter`, which picks text that reads on it |

## Tests

`tests/HockeySim.Desktop.Tests/` tests view models against real Management
games, plus a headless Avalonia walkthrough.

| File | Covers |
| --- | --- |
| `MainWindowViewTests.cs` | Headless walkthrough of the menu bar, Continue's menu, and every page, team colours, the profile page and its tabs, saving, loading, closing |
| `NewGameViewModelTests.cs`, `GameShellViewModelTests.cs`, `TeamBrowsingTests.cs` | Setup: steps, the world seed and its preview, the team preview, the manager and save names, starting the previewed world; sections, tabs, planned pages, the banner, and the status bar; browsing teams |
| `LinesPageViewModelTests.cs` | Lineup and unit editing, any-role choices, out-of-position warnings, dragging and dropping players, opening profiles |
| `SeasonAdvancementTests.cs` | Continue, refresh after a day, failures, box scores, the preseason labels, the schedule's phases and preseason results |
| `MatchDetailViewModelTests.cs` | The box score for regulation, overtime, and shootout matches: periods, totals, records, the comparison, stars, goalies, and tabs |
| `SimulateAheadTests.cs` | Continue's menu by phase, one refresh per run, Stop, a failure part-way, and the run summary for every stop reason |
| `PlayoffDisplayTests.cs` | A shared season played through the shell: the bracket before and at each playoff stage, playoff schedule rows and box scores, switching statistic sets, the completed season and champion |
| `HomePageTests.cs` | The home page's panels: the tale of the tape (opening day, mid-season, playoffs, season complete), team leaders and the minimum-games choice, the standings panel's scopes, upcoming games and recent results and what they open; also defines the collection that shares `PlayoffDisplayTests`' played season |
| `StandingsAndStatisticsTests.cs` | Standings scopes including the wild card, conference columns, recent form, team links, playoff markers, season totals |
| `AdvancedStatisticsDisplayTests.cs` | Statistic formatting, box-score summaries and new columns, basic and advanced roster views, the profile line, team statistics |
| `TableSortingTests.cs` | Sorting roster, schedule, and box-score tables: first-click direction, reversal, dashes last, ties, selection, and the sort held across teams, refreshes, groups, and column views |
| `RosterViewsTests.cs` | Roster player groups and the forward filter, each view's columns, the sort across groups and views, selection when filtered out, the player strip, xSV%, the Lines link |
| `PlayerProfileTests.cs` | The profile page against real games: skaters and goalies; healthy, playing hurt (current ratings, marks, losses), and out; the injured-until line; a player who has not played; the game log's phases, labels, order, and box scores; usage and special-teams units; the playoff switch; the profile's banner, tabs, and section in the shell; a refresh; and the entry points (roster strip, home) |
| `SaveAndLoadTests.cs` | Save and load screens, unsaved progress, confirmations |
| `StartupMenuTests.cs` | The startup menu: Continue (open game or newest readable save), the recent games (newest first, at most eight, unreadable saves), choosing a row, refresh after saving, the load screen's row details, and standing text |
| `PlayerBiographyDisplayTests.cs`, `AppVersionTests.cs` | Formatting, version display |
| `TeamPaletteTests.cs` | Readable text on team colours |
| `InjuryDisplayTests.cs` | Continue waiting for replacements, the status bar's lineup state, Lines-page injury warnings, roster markers, the injury report, the profile's health |
| `GameTestData.cs`, `TestMatchSimulators.cs`, `TemporarySaveDirectory.cs`, `InjuredPlayerReplacement.cs`, `PreseasonPlay.cs` | Builders (sessions start on opening day unless a test needs the preseason), test engines, and replacing injured players between days |

## Behaviour

New-game setup has three steps across the top, League, Team, and Manager, with
Cancel, Back, Next, and on the last step Start game below; a step header opens
any step already reached. League offers the fictional league (real players are
listed but disabled), names the first season, and takes an optional world seed.
Leaving it generates the league through Management's `PreviewNewGame`, from the
typed seed or a fresh random one; an unchanged seed keeps the league already
generated, and a changed one regenerates it, keeping the chosen team. Team lists
both conferences by division, each team with its colours, team overall, and
outlook; the selected team's preview, in its colours, shows its crest, name,
division, team overall, outlook, average age, key players, and forwards,
defence, and goalie ratings. Manager takes the manager's name and the save name,
which follows the team until edited, and sums up the league, seed, team, and
manager. Start game, in the team's colours, starts the previewed world from the
same seed. A rules step waits for
[configurable match tuning](../future-features.md#configurable-match-tuning).

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
both), and Continue, a split button: Continue plays one day, and its arrow opens
a menu of runs for the current phase (see below). Under
the menu bar is the managed team's banner in its colours: crest, name, record,
points, division and conference rank, and the next match and last result,
which cover the preseason, regular season, and playoffs, and the streak, which
counts regular-season results only. A page may show its own banner instead, and
its own tabs: the player profile does both. Below the banner, the current
section's pages are tabs, with the page's subtitle on the right.
Home, Team, League, and Inbox are sections; the team and league schedules are
one page, and opening it from Team shows the managed team. The player profile is
a page in no section: while it is shown the menu bar keeps the section it was
opened from. The status bar shows
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
Every player's name, in a slot or among the scratches, opens their profile; only
a slot's arrow opens its list, and other clubs' slots have no list. On the
managed team's lineup, each slot and scratch also has a grip: dragging it carries
the player's number and name with the pointer, and releasing over a slot is the
same as choosing that player there (goalie slots take only goalies, skater slots
only skaters). Releasing elsewhere or pressing Escape cancels. The drag is drawn
by the page rather than the operating system, whose drag shows a file icon that
Avalonia cannot replace.
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
preseason, league, or playoff matches. Continue's menu simulates ahead through
Management's `SimulateAhead` (see [season](season.md#simulating-ahead)): next day
and to the next match in every phase, then one week, one month, and to the
playoffs in the regular season, to the end of the preseason, or to the end of
the round and of the playoffs. A run plays off the UI thread like a single day;
Continue then reads "Simulating…" with the date reached, the arrow gives way to
Stop, which ends the run after the day being played, and saving, loading, and
leaving for the menu wait as they do for a day. The session publishes one
snapshot when the run ends, so pages refresh once, and a banner says how many
days were played and why the run stopped. A failure part-way keeps the days
before it: the error banner says how many were simulated and names the day that
could not be played. A run that played any day is unsaved progress. The home page is built
around the managed team's next match, in five panels, each its own view model
and view so it can become a widget on a [home canvas](https://github.com/tylerforgione/hockeysim/issues/65):
upcoming games and the standings over recent results and team leaders, beside
the tale of the tape. Upcoming games lists the next seven matches in any phase,
each with the opponent's record (or the playoff round and game) and opening on
the schedule. The standings panel shows the managed team's division, wild-card
view, conference, or the league, as Management ranks them, with the choice held
for the session. The tale of the tape compares the two teams for the next match,
the managed team on the left: crest, record, and standing, then points
percentage, goals for and against per game, power play, penalty kill,
five-on-five xGF% and CF%, team save percentage, and the last ten games, each
with a bar in the teams' colours and the better side emphasised (fewer goals
against is better; an undefined figure is a dash and favours neither). It names
each team's top scorer and starting goalie, and links to the Lines page, the
opponent on the Teams page, and the season series on the schedule. Once the
managed team has a playoff series, even after it is out, it compares playoff
figures instead: the
playoff record and seed, without points percentage or the last ten, and the
series status linking to the playoff picture. With no next match it says there
is none, or that the season is complete. Recent results lists the managed team's last six results in
any phase, each opening the box score. Team leaders shows points, goals,
assists, five-on-five xGF%, save percentage, and goals saved above expected in
the same regular-season or playoff figures; the two rates count only players
who have played a minimum share of the team's games, chosen on the panel (half
at first) and held for the session. The inbox is not on the home page; the menu
bar and status bar carry it. Once the regular season ends the standings page shows the final
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
score opens with a banner split diagonally between the two teams' primary
colours, away on the left: each team's crest, name, and record once the match
was played (wins, losses, and overtime losses, or its series record in the
playoffs), and between them the final score and decision. Under it are the
Summary and Players tabs, with the date and the home team's arena on the right.
The Summary shows goals (with a shootout column after a shootout), shots, xG, and
the power play (goals of opportunities) by period and in total; the scoring
summary (each goal's period and time, team, scorer, assists, PP/SH/PS/EN marker,
and the running score, away first); the penalty summary (period and time, team,
player, infraction, and length); the teams compared, each figure with a bar in
the two teams' colours (shots, shot attempts, xG, five-on-five xG share,
faceoffs won, hits, blocked shots, giveaways, and penalty minutes); the three
stars, each with its line and game score; and both goalies. Players lists each
team's skaters (goals, assists, points, plus/minus, time on ice, shots, shot
attempts, xG, hits, blocks, faceoffs won and lost, takeaways, giveaways, penalty
minutes, power-play and shorthanded goals, and five-on-five on-ice Corsi and xG
percentages) and starting goalie (shots and goals against, saves, save
percentage, xG against, goals saved above expected, time on ice), stacked away
first because each table needs the full width. A shot chart and play-by-play
wait for [play-by-play retention](../future-features.md#play-by-play-retention).
The standings page presents Management's division, wild-card, conference, or
league tables as ranked, without re-sorting them, chosen with a segmented
control. The division, wild-card, and conference views put the two conferences
side by side, a column each; the league view is one table. The wild-card view
lists each division's top three, then each conference's wild-card race with a
rule in the managed team's secondary colour under the second wild card. Each row
shows rank, the team's colour swatch and name, GP, W, L, OTL, PTS, P%, ROW, DIFF
(goals for and against in its tooltip), the last ten games (wins-losses-overtime
losses), and the current streak, both from regular-season results. The managed
team's row is highlighted. A team's name opens it on the Teams page until
[team pages](https://github.com/tylerforgione/hockeysim/issues/63) exist. Every
table marks each team's playoff status with the NHL's letter (x, y, z, p, or e)
before its name, with the meaning in a tooltip and a one-line legend beneath the
tables. The team statistics page lists the managed
team's power-play, penalty-kill, and faceoff percentages and its five-on-five
Corsi, Fenwick, shot, and xG shares; once the playoffs start it switches between
the regular season's and the playoffs', showing dashes if the team missed them. The injuries page is the managed team's
injury report: each injury that has not healed, players who cannot play first,
with its status and expected return. Other teams' figures wait for
[team pages](https://github.com/tylerforgione/hockeysim/issues/63). Roster tables mark an injured player OUT or INJ beside
their name, with the details in a tooltip, and the player profile shows the
player's health (see below). The exact recovery time, wear, and durability are
never shown. Once the playoffs start, each roster switches between regular-season
and playoff statistics from the skaters panel's header; the choice changes both
tables, is the set a profile opened from the roster starts on, holds across teams
and days like the column choice, and shows zeros and dashes for a team or player
without playoff games. Every team's roster (the Roster page and the
Teams page) has a toolbar of player groups (All players, Skaters, Forwards,
Defence, Goalies, with a filter for all forwards, centres, or wingers under
Forwards) and column views (Basic, Advanced, Ratings, Bio; Ratings at first).
All players shows a skater table above a goalie table; the other groups show
one. Every view shows the number, name, position (skaters), and lineup role.
Basic adds age, overall, and season totals (skater GP, G, A, P, +/-, PIM, PPP,
SHP, shots, TOI per game, FO%; goalie GP, SA, SV, GA, SV%, GAA, shutouts);
Advanced adds skater five-on-five on-ice Corsi and Fenwick for, against, and
percentage, xG for, against, and percentage, and individual xG, or goalie GP,
time in net, SA, GA, xG against, GSAx, SV%, and expected SV% (one minus xG
against over shots against); Ratings adds the ratings; Bio adds age, shoots or
catches, height, weight, birth date, birthplace, and nationality. In Advanced a
situation selector is fixed at 5 on 5. The group, filter, and view hold across
teams and days like the statistic set. When a group hides the selected player
the first listed player is selected; opening a player the group hides switches
to All players. Under the tables a strip shows the selected player: number,
name, position, injury marker and summary, handedness, age, height, weight,
nationality, overall, and the player's ratings, with Profile and Game log links,
which open the player's profile on its Overview or Game log tab with the roster's
statistic set, and Lines, which opens the Lines page on that team. Double-clicking
a row opens the profile too, and the tables take the full width. A player who has
not appeared shows zeros, and every percentage, average, and rate is a dash until
it is defined. Advanced figures are five-on-five only; views by
situation are a [future feature](../future-features.md#situational-statistics-views).
The player profile is a shell page opened from the roster strip's links, a roster
row's double-click, or the home page's team leaders and tale of the tape (the
latter two on the statistic set the home page shows for the player's team: the
playoffs once the team has a series). It shows any team's player, and brings its
own banner and tabs: Overview, Ratings, Statistics, Game log, Health, and
Career, which is disabled until [career history](../future-features.md#career-history-and-league-leaders).
Its subtitle is the player's team. The banner is painted in the player's team
colours rather than the managed team's: the jersey number, name, position,
handedness, and age, a link to the team (opening it on the Teams page), an
injury badge when injured (OUT or INJ, the injury, and whether the player is out
or playing through), and figure tiles: a skater's overall, points, and
five-on-five xGF%, or a goalie's overall, save percentage, and goals saved above
expected, from the page's statistic set. Once the playoffs start, a Regular
season and Playoffs switch in the statistics panels' headers drives the banner,
Overview, and Statistics tab together; a refresh after a day rebuilds the same
player, keeping the tab and the set. The Overview has four panels. Biography
lists the birth date, birthplace, nationality, height, weight, handedness,
position, and number. Statistics shows a skater's scoring (GP, G, A, P, +/-, PIM),
shooting and usage (SOG, SH%, ixG, TOI/GP, PPP, SHP), five-on-five on-ice
figures (CF, CA, CF%, xGF, xGA, xGF%), and physical play (HIT, BLK, TK, GV, FO%),
or a goalie's goaltending and expected-goal figures, then the newest five rows of
the game log. Ratings shows the position's ratings as bars, each at the rating
the player plays at today; while playing hurt a reduced rating's bar carries a
mark at the healthy value and the points lost, and one line at the bottom says
when the injuries heal ("Injured until Sun 23 Nov", with the earliest and latest
dates when the staff cannot say which day), following the injury that heals
last. Usage lists a skater's even-strength (five-on-five and other), power-play,
and penalty-kill time on ice per game, the latter two with each unit the player
is on: PP or PK for the five-on-four power play or four-on-five penalty kill,
then the five-on-three and four-on-three, or three-on-five and three-on-four,
units ("2:58 · PP1 · 5v3 U1 · 4v3 U2"); and the lineup role; a goalie's starts, time in net per start, and role. Most common
linemates wait for [combination statistics](../future-features.md#line-and-unit-combination-statistics).
The Ratings tab shows every rating in its group (offence, defence, and game for
a skater; goaltending and game for a goalie) with the overall rating, in the same
bars. The Statistics tab shows the full statistic line in groups. The Game log
lists every completed match of the season the player appeared in, in any phase,
newest first, each labelled as the schedule labels it (PRE, REG, or "R1 G3"),
with the date, the opponent, the result for the player's team, and a skater's
G, A, P, +/-, PIM, SOG, ixG, HIT, BLK, and TOI or a goalie's SA, GA, SV, SV%,
xGA, GSAx, and TOI (a goalie's starts, until
[backup goalies play](../future-features.md#backup-goalies-in-matches)); choosing
a row opens its box score in the player's team's schedule. It does not sort. The Health tab lists each injury that has not
healed: its name, body part, status (out or playing through), the date it was
suffered, the expected return, and its effect (the rating points lost, or that
the player cannot play), or "Healthy". The exact recovery time, wear, and
durability are never shown.
The roster tables, the schedule, and the box score's skater tables sort by any
column: clicking a header sorts by it, an arrow marks the sorted column and
direction, and clicking again reverses it. Figures start with the highest; names
(by last name), position, nationality, birthplace, handedness, lineup (in lineup order, scratches last), venue, and
opponent start at the top, and birth dates with the oldest. Values shown as a dash, and unplayed matches, sort
last either way, and ties keep the table's default order (position then name,
date, or box-score order). The schedule's phase and date sort by date and its
result by goal differential; the box score's faceoffs sort by faceoffs won.
Each roster keeps one sort for skaters (every skater group) and one for
goalies, across groups, column views, teams, days, and statistic sets; in a
view without the sorted column the table shows its default order, and the sort
returns in a view that has it; the schedule keeps its sort across teams and
days; one box-score sort applies to both teams and carries to the next box score
opened. Sorts are not saved, so a started or loaded game opens unsorted. The
selected player or match stays selected. Standings, the team statistics page,
and the box score's goalies do not sort.
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
Game screen, from the startup menu or the Game menu, lists every save with its
details (team and colours, in-game date, record, standing, last played). Delete
removes the selected save, readable or not, after asking. Deleting the save the
open game was last saved or loaded as leaves that game open with unsaved
progress.

The startup menu wears the league's neutral colours even while a game is open:
its view merges the palette again, over the window's team brushes. Its left
column holds the wordmark, Continue (with the game name and in-game date it
continues), New game, Load game, Quit, and the version. Continue returns to the
open game after quitting to the menu; with none open it loads the newest save
whose details can be read, and it is disabled when there is neither. Beside the
column, over a quiet rink and inset within its end boards (the view's
code-behind follows the rink as it scales), a table lists the eight most recent saves with the
same details as the load screen; clicking a row loads it. Standing reads
"Preseason", the division rank ("3rd Atlantic", kept after missing the playoffs),
the playoff round, "Eliminated", or "Champions". A save whose details cannot be
read is listed by name and time as "Can't be read", and choosing it shows why it
cannot be loaded. The list and Continue's target are read again each time the
menu is shown. The menu and the load screen load through one `SavedGameLoader`,
so both confirm discarding unsaved progress and describe failures the same way.

A `GameSession` tracks whether the game
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
season is complete.

Releases publish `HockeySim.Desktop` as a self-contained, single-file build
named `HockeySim` for each supported runtime identifier; ordinary builds stay
framework-dependent. See
[the deployment decision](../adr/0005-self-contained-desktop-builds.md).
