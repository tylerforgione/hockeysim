# Future features

Features and game-design decisions intentionally left out of the current
milestone. Each entry describes current behaviour where relevant, the intended
behaviour, and the direction to take; it does not replace a scoped GitHub issue
when implementation begins.

v0.2.0 (the `v0.2.0` GitHub milestone) still plays one season: the event-based
match engine with special teams and advanced statistics, player identities and
flexible lineup roles, in-match injuries, a preseason and playoffs, and Desktop
navigation and home-page customization. Season rollover is planned as v0.3.0.
Entries marked **Needs season rollover** only pay off once several seasons can
be played.

## Configurable match tuning

Match simulation is driven by tuning values that are currently fixed constants
in `HockeySim.Simulation`, kept together in `Play/MatchTuning.cs` and the
expected-goal model: possession outcome weights, how strongly rating differences
shift them, shot danger, blocking, and finishing chances, fatigue and shift
lengths, open-ice overtime effects, shootout scoring, probability bounds, and
the forward-line, defence-pair, and three-on-three usage shares.

Two goals build on making these values configurable:

- **Calibration to real NHL data.** Planned for v0.2.0 alongside the event-based
  engine: tune the defaults so aggregate outcomes approximate recent NHL seasons,
  and assert against the calibrated targets with wide tolerances.
- **User-facing league settings.** Let the user adjust a small set of
  understandable options, such as "Offensive output", when creating a game or in
  league settings. Each option maps to one or more underlying tuning values (for
  example, offensive output scales the base shot and goal chances); raw tuning
  values stay internal.

Direction:

- Extract the constants into an immutable, validated tuning type owned by
  Simulation and pass it to the simulator with each match. The current values
  become the defaults.
- Management owns league settings and translates user options into tuning.
  Desktop only edits settings through Management commands.
- Save the settings with the game. The same save, settings, and random state must
  produce the same results ([reproducible saves](adr/0002-reproducible-saves.md)).
- Decide whether settings can change mid-season or only when a game is created,
  and bound each option so extreme values cannot make matches degenerate (for
  example, a shootout that almost never produces a winner).

## Side-specific positions

v0.2.0 lets any skater fill any skater lineup role, keeps `Position` as a
player's natural position (centre, wing, defence, or goalie), and applies
out-of-position and off-hand-side effects in simulation.

Later, consider splitting positions by side (left wing, right wing, left
defence, right defence) if handedness alone does not express a player's
natural side well enough. Decide how it interacts with handedness, lineup
validation, generation, and the out-of-position effects before changing it.

## Native packaging and updates

Releases publish self-contained, single-file builds as archives for `win-x64`,
`osx-arm64`, `osx-x64`, and `linux-x64`
([ADR 0005](adr/0005-self-contained-desktop-builds.md)). They are not signed
for distribution, so macOS Gatekeeper and Windows SmartScreen warn on first
launch, and players unpack and run the executable themselves.

Later:

- A macOS `.app` bundle with an icon, Developer ID code signing, and
  notarization.
- Windows code signing and an installer or MSIX package.
- Linux AppImage or Flatpak packages.
- Auto-update, and checking for a newer release from the app.
- Smaller or faster builds through trimming or ReadyToRun, deferred in
  ADR 0005. Trimming needs `BuiltInComInteropSupport` removed after a native
  Windows check.
- Arm builds for Windows and Linux.
- Migrating saves between releases rather than rejecting another save format
  version ([ADR 0004](adr/0004-local-save-format.md)).

## Desktop end-to-end tests

The Desktop tests closest to end to end use Avalonia.Headless: they render the
real window in the test process and walk from the startup menu through every
in-game page, play a day, save, and load. Nothing launches the published
executable. The release workflow checks that each build publishes and contains
the executable, and a manual smoke test on each platform covers launching,
starting a game, advancing a day, saving, and loading
([testing](testing.md#release-builds)). Packaging, startup, real windowing, and
platform problems are only caught by that manual check.

Automate it once concrete failures or repetitive release checks justify the
cost ([testing](testing.md#test-organization)):

- Start with a launch smoke test in the release workflow: run each published
  build on its own OS runner and confirm it starts, shows the expected version,
  and exits cleanly. This needs a way to drive or close the app without a
  person, such as a command-line flag or a display server on Linux.
- Then consider scripted UI journeys against the real executable (new game,
  advance a day, save, load) through platform accessibility APIs or an
  automation driver. Avalonia's UI automation support and the available drivers
  differ by platform; approve any new test dependency under the
  [technology stack](tech-stack.md) policy.
- Keep the journeys few and stable, and keep detailed behaviour in the faster
  headless and view-model tests. Use fixed seeds and a temporary save folder so
  runs are reproducible and leave no files behind.

## Realistic season calendar

The regular season is generated in two stages in `HockeySim.Management/Scheduling/`.
`MeetingPlanner` decides who plays whom and who hosts, independent of dates.
`RoundCalendar` then packs the meetings into 84 rounds in which every team
plays exactly once, one round every other day from October 1. Every date has 16
matches, and there are no breaks, back-to-backs, or special events.

A realistic calendar should support:

- **Breaks and blocked dates**: the All-Star break, an Olympic or international
  tournament break, and league-wide days off such as Christmas.
- **Uneven nights**: a varying number of matches per date, with some teams idle.
- **Rest rules**: back-to-backs allowed but limited, plus caps such as matches
  in a rolling window, and home stands and road trips.
- **Pinned special matches**: a chosen pairing on a fixed date, such as an
  outdoor game or a holiday showcase, possibly at a neutral or outdoor venue.
- **Non-match events**: the All-Star Game and similar events appear on the
  calendar but are not regular-season matches between league teams.

Direction:

- Keep `MeetingPlanner` and its balance guarantees; replace `RoundCalendar`
  with a calendar builder that takes the planned meetings plus an explicit
  calendar specification (season window, blocked dates, pinned matches, rest
  rules). The round structure cannot express uneven nights, so this needs a
  different algorithm, such as greedy date assignment with repair or a local
  search over the constraints.
- Venues are fixed per meeting before dates are chosen, so the builder only
  orders them. Decide whether venue alternation through the season still
  matters once dates are uneven.
- Neutral-site or outdoor matches need a venue or event type on
  `ScheduledMatch`. Non-match events need their own Domain concept rather than
  a fake `ScheduledMatch`.
- Keep every calendar input explicit and derived from game state, never the
  wall clock, and keep generation on the controlled random stream
  ([reproducible saves](adr/0002-reproducible-saves.md)).
- Existing schedule tests (opponent matrix, home/away totals, same-day
  conflicts, reproducibility) still apply. Add checks for blocked dates, rest
  limits, and pinned matches, and replace the 16-matches-per-date check.

## Skip-to-date and bulk simulation

Desktop advances the season one league day per Continue press. Playing a full
season therefore takes 167 presses.

Add controls that play several days in one request: to the managed team's next
match, to a chosen date, or to the end of the regular season. Build them on
Management's existing day-atomic `AdvanceDay`, so each day still applies
completely or not at all and a failure stops at the last complete day. Report
progress and allow cancellation between days. Publish a snapshot when the run
ends rather than after every day. Decide whether events such as injuries or
inbox messages should interrupt a run once those features exist.

## Parallel match simulation

`LeagueDay` simulates a day's matches in schedule order on one continuous
random stream: each match starts from the state the previous one left, so the
matches cannot run concurrently without changing every outcome.

There is no need yet. A benchmark of the current engine (Release build, Apple
M4 Pro with 8 performance and 4 efficiency cores, October 2026) found:

- About 20–30 µs per match, or roughly 45 ms of engine time in a 1,344-match
  season.
- A full season through `AdvanceDay` takes about 210–330 ms, mostly outside the
  engine. Each day builds a snapshot whose cost grows with the history (about
  1.2 ms by season end).
- With per-match seeds, simulating 20 seasons' worth of matches took 560 ms
  sequentially and 145 ms with `Parallel.For`: only about 4× on 12 cores, which
  suggests allocation in the engine (LINQ, lists, result records) rather than
  arithmetic limits scaling.

Parallelism pays off once a request simulates far more matches than one league
day:
[playoff odds and projections](#playoff-odds-and-projections), a calibration
harness that runs thousands of seasons while tuning
([configurable match tuning](#configurable-match-tuning)),
[development leagues](#development-leagues-and-roster-transactions) simulated
alongside the NHL, and [bulk simulation](#skip-to-date-and-bulk-simulation)
once AI decisions add work to each day.

Direction:

- Draw one seed per match from the main stream in schedule order, then
  simulate each match on its own `ControlledRandom`. Results then do not depend
  on thread scheduling, and the main stream still advances by a fixed amount
  per day, so saves stay reproducible
  ([reproducible saves](adr/0002-reproducible-saves.md)). This changes every
  seeded outcome, which is allowed between engine versions; record the change
  to how the stream is consumed in an ADR.
- Keep the engine's inputs read-only during a day so concurrent matches can
  share teams safely. Applying results stays sequential and day-atomic through
  `Season.CompleteDay`.
- Profile before parallelising: cutting allocation in the engine, and building
  one snapshot per run instead of one per day during bulk simulation, may
  matter more than extra cores.
- GPU acceleration is not planned. Matches are branchy and small; a GPU library
  would be a new cross-platform runtime dependency; and floating-point results
  that differ between devices would break reproducibility across machines.

## Playoff odds and projections

The standings show current records only.

Estimate each team's chance of making the playoffs, winning its division, and
winning the championship, and project final points, by simulating the rest of
the season (and, once playoffs exist, the bracket) many times from the current
state. Show the odds on the standings page and the managed team's home page.

Direction:

- Projections must not affect the game. Run them on a random stream derived
  from the game state that never feeds back into the main stream, so viewing
  odds cannot change an outcome
  ([reproducible saves](adr/0002-reproducible-saves.md)), and work on copies
  rather than the live season.
- Simulating thousands of seasons is the clearest case for
  [parallel match simulation](#parallel-match-simulation). Run projections off
  the UI thread, refresh them after a day is played rather than on every
  snapshot, and decide how many runs give stable enough percentages.
- Decide whether projections use current lineups and ratings unchanged, or
  allow for future injuries and AI decisions.

## Career history and league leaders

**Needs season rollover.** Desktop shows each player's current-season totals.
v0.2.0 records advanced statistics (shot attempts, expected goals, goals saved
above expected, plus/minus, time on ice, penalties, special-teams production,
hits, blocks, faceoffs, takeaways and giveaways) and keeps playoff totals apart
from the regular season, but totals are still not kept once a later season
begins.

Keep per-season regular-season and playoff totals as history when subsequent
seasons exist, and show career lines on the player profile and team history on
the team page. Add sortable league-wide leader tables for the current season,
single seasons, and all time. History storage depends on the
[league database](#league-database-for-multi-season-history).

## League database for multi-season history

Each game is saved as one Brotli-compressed JSON document
([ADR 0004](adr/0004-local-save-format.md)). Every save rewrites the whole world,
every load parses it and replays the season's completed matches, and the entire
history is held in memory. That suits one season: a complete 32-team season is
4.6 MB of JSON, mostly box scores, and saves in about 60 ms.

It does not suit many seasons with more leagues. An AHL roughly doubles the
match history per year, and draft prospects add statistics from leagues outside
the simulated ones. Over 20–30 seasons the history is estimated at 150–300 MB of
JSON, all parsed, held in memory, and rewritten on every save, when most of it
only serves career pages and all-time records.

Store the game in a SQLite league database, one file per game, when the first
multi-season feature is built:

- **Active and archived state.** Keep the current season's live state in
  memory, and replay only the current season on load. Completed seasons become
  fixed records that are stored and queried, not recalculated.
- **Small writes.** Saving a league day adds that day's results in one
  transaction, together with the random state so reproducibility holds
  ([reproducible saves](adr/0002-reproducible-saves.md)), instead of rewriting
  the whole history.
- **Queries without loading everything.** Career lines, all-time leaders, and
  prospect statistics are read on demand, so memory stays limited to the active
  season.

Direction:

- Decide first what a completed season keeps: every box score, or only season
  totals per player and team. That choice sizes the archive and shapes the
  schema.
- Keep the save semantics players expect. Play on a working copy and copy it
  into the save slot on Save (SQLite's online backup API or `VACUUM INTO`), so
  quitting without saving still discards progress.
- `IGameSaveStore` currently takes the whole world at once. Replace it with
  contracts shaped for incremental writes and history queries, still owned by
  Management and free of storage types.
- `Microsoft.Data.Sqlite` is a new runtime dependency (with a native SQLite
  library); approve it under the [technology stack](tech-stack.md) policy.
  Record the change in a new ADR that supersedes ADR 0004, with a schema
  versioning and migration approach.

## Autosave and save management

Saving is manual: the user saves under a name and loads from the startup menu.
Later:

- Autosave at chosen intervals (every league day, week, or before the user's
  matches), into rotating slots that do not overwrite the user's named saves.
- Rename and delete saves from the load screen.
- Show each save's club, season, and date in the list. That needs the summary
  stored where listing can read it without loading the whole game, such as in
  the save header, which changes the save format version.
- Cloud or synced saves.

## Season rollover

Planned as v0.3.0. A completed season can be browsed and saved, but no next
season begins; v0.2.0 ends when the playoff champion is crowned.

Rollover moves the game from a completed season to the next one: archive the
completed season, advance the calendar, age players (ages already derive from
birthdates in v0.2.0), and generate the next schedule and preseason. Most
multi-season entries below build on it.

Before or alongside it, decide on the pieces the user's list does not yet
cover but rollover needs to feel complete:

- **Player development**: ratings that progress and decline with age, with
  hidden potential (see [scouting](#scouting-and-hidden-information)).
- **Trades** between clubs, and **AI general managers** that set other
  teams' lineups, sign, trade, and draft.
- **The archive**: what a completed season keeps; see the
  [league database](#league-database-for-multi-season-history).

## Offseason

**Needs season rollover.** The period between the playoff final and the next
preseason, run as a sequence of dated phases: the draft, the re-signing window
for expiring contracts, the start of free agency, and training camp. Each phase
is a league-day range like the season, so the same advancement, reproducibility,
and atomic-day rules apply. It ties together [contracts](#contracts-and-the-salary-cap),
[free agency](#free-agency), the [draft](#draft-and-prospect-leagues), and
[retirement](#player-retirement).

## Contracts and the salary cap

**Needs season rollover.** Players have no contracts and teams have no
payroll.

Add contracts with term, salary per season, and cap hit (average annual
value), and a league salary cap and floor that teams must respect. Support
contract modifiers and bonuses: signing and performance bonuses, no-trade and
no-movement clauses, entry-level and two-way contracts (with
[development leagues](#development-leagues-and-roster-transactions)), and
long-term injured reserve relief tied to the
[injury system](#full-injury-and-health-system). Contract negotiation needs
player demands and willingness to sign, which also drive
[free agency](#free-agency) and re-signing.

## Free agency

**Needs season rollover.** Players whose contracts expire become restricted
or unrestricted free agents according to age and service time. Teams bid,
players choose by money, term, role, and team quality, and AI general managers
sign players within the cap. Restricted free agency includes qualifying
offers and offer sheets.

## Development leagues and roster transactions

**Needs season rollover.** One league is simulated, every team has exactly 23
rostered players, and there is nowhere to send or call up players.

Add development leagues (an AHL and an ECHL) affiliated with each club, with
their own schedules and simulated matches, and the rules for moving players
between levels: sending players down and calling them up, two-way contracts,
waivers and waiver exemption, roster limits and the reserve list, and
emergency recalls. Once players can be called up, lift the v0.2.0 injury cap
that guarantees a valid lineup from the 23-man roster.

More simulated leagues multiply the stored history; see the
[league database](#league-database-for-multi-season-history).

## Draft and prospect leagues

**Needs season rollover.** Every player is generated directly onto a league
roster.

Hold an annual entry draft whose prospects come from prospect leagues: college
hockey, the three CHL leagues, and European professional and junior leagues.
These leagues exist so prospects have a background and statistics, but they do
not need every match simulated; generate plausible seasonal statistics from
ratings instead. Draft order follows the standings and a lottery. Drafted
players hold signing rights for a limited period. New players generated each
year keep the [generated faces](#generated-player-faces) of the fictional
league.

## Player retirement

**Needs season rollover.** Players retire at the end of a season according to
age, decline, contract status, and interest from teams, and some retire early
after serious or repeated injuries. Retired players keep their career history.

## Off-ice events

**Needs season rollover** for most value. Events not caused by a simulated
match: suspensions for incidents in a match (supplementary discipline after
fights or dangerous hits), injuries away from the rink or in practice, and
other news delivered to the inbox. Keep every event on the controlled random
stream and derived from game state
([reproducible saves](adr/0002-reproducible-saves.md)). Decide whether such
events interrupt [bulk simulation](#skip-to-date-and-bulk-simulation).

## Goalie, bench, and instigator penalties

The match engine (#51) penalizes only skaters, for infractions in the run of
play and after hits. Later: goalie penalties (served by a teammate on the ice),
bench minors such as too many men and unsportsmanlike conduct by the bench,
match penalties, the instigator and aggressor rules for fights (with their
extra minors and misconducts), a teammate serving an ejected player's major,
penalized players returning to the ice only at the next change rather than at
once, and awarded goals for a foul on a breakaway at an empty net (#52 calls an
ordinary penalty there instead of a penalty shot). Each changes seeded results, so it is an engine version change.

## Full injury and health system

v0.2.0 injures players only in matches (never in the preseason), keeps
body-part wear hidden, and caps injuries so every team can still dress a valid
lineup from its 23-man roster.

Later: injuries in practice and away from the rink (see
[off-ice events](#off-ice-events)), preseason injuries, removing the cap once
[call-ups](#development-leagues-and-roster-transactions) exist, injured
reserve and long-term injured reserve with [cap relief](#contracts-and-the-salary-cap),
medical staff who affect recovery (see [staff](#coaches-staff-and-facilities)),
and injury reports whose accuracy depends on [scouting](#scouting-and-hidden-information).

## Preseason roster decisions

v0.2.0 plays a short preseason of exhibition matches, one against each
divisional opponent, in which nothing counts and nobody is injured. Later,
make the preseason a training camp: invite more players than the roster
holds, cut players to development leagues, and decide how many matches and
which opponents a realistic preseason uses.

## Tactics and formations

v0.2.0 adds special-teams units but no tactical choices. Let the user set
team tactics (forecheck, defensive structure, pinching, shooting tendency,
power-play and penalty-kill formations, when to pull the goalie) and line
usage, and have the simulation respond to them. AI teams choose tactics too.

## Coaches, staff, and facilities

**Needs season rollover.** Lower priority. Add head and assistant coaches,
goalie and skills coaches, trainers and medical staff, and scouts with ratings
that affect player development, tactics, recovery from injury, and scouting
accuracy. Facilities (training, medical, arena) could add longer-term
investments. Staff need contracts and a hiring market.

## Scouting and hidden information

Desktop shows every player's exact ratings and (from v0.2.0) an overall
rating. Durability and body-part wear are already hidden in v0.2.0.

Add scouting in the style of OOTP, Football Manager, and Franchise Hockey
Manager: the user sees estimates of other players' ratings and potential,
whose accuracy depends on scouts, time spent watching a player, and
familiarity with a league. Decide which values are always exact (identity,
statistics), which are estimates, and which stay hidden; Management snapshots
would then carry the user's view of a player rather than true ratings.

## Real players

The league, teams, and players are fictional and generated.

Offer a real-player database, at least for the NHL and ideally for every
simulated and prospect league, with biographical details, positions, and
ratings derived from real statistics. Keep the fictional generator for new
players and as the default world. Decide how the data is sourced, updated
each season, and shipped (bundled or imported by the user). Using real
player names and likenesses in a distributed game is subject to the
players' association's group licensing; check before shipping a bundled
database.

## Generated player faces

Players have no portraits. Show a pixel-art face for each player, generated
for new players from their identity so it never changes after it is created
and is the same on every machine. Decide whether real players (see
[real players](#real-players)) get drawn or generated portraits.

## Desktop redesign

The maintainer may want to rework Desktop's visual design and layout.
Capture specific concerns here before starting; colours and control styles
already live in `HockeySim.Desktop/Theme/`.

## Display settings

Desktop shows height and weight in imperial units from v0.2.0. Add a setting
to show metric units instead, alongside other presentation preferences as
they arise.
