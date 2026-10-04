# Future features

Features and game-design decisions intentionally left out of v1. Each entry
describes current behaviour where relevant, the intended behaviour, and the
direction to take; it does not replace a scoped GitHub issue when implementation
begins.

## Configurable match tuning

Match simulation is driven by tuning values that are currently fixed constants
in `HockeySim.Simulation`: base shot and goal chances, how strongly rating
differences shift them, the overtime shot rate, shootout scoring, probability
bounds, and the forward-line and defence-pair usage shares.

Two goals build on making these values configurable:

- **Calibration to real NHL data.** Tune the defaults so aggregate outcomes
  (goals and shots per game, regulation/overtime/shootout split, scoring by line,
  save percentage) approximate recent NHL seasons. The statistical-band tests in
  `HockeySim.Simulation.Tests` should then assert against the calibrated targets
  with wide tolerances rather than hand-picked ranges.
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

## Allow skaters in any skater lineup role

The current lineup model treats a player's `Position` as both their player
position and the only lineup role they may occupy. Centres can only fill centre
slots, wings can only fill wing slots, and defence players can only fill
defence-pair slots.

A future lineup model should allow any skater to fill any skater role. Skaters
must remain ineligible for starting or backup goalie roles, and goalies must
remain ineligible for skater roles.

Before implementing this, decide whether playing outside a player's usual
position affects simulation performance and whether `Position` represents a
usual position, a preferred position, or a broader player category. Update the
Domain lineup invariants, Management commands and snapshots, simulation inputs,
and tests together once those rules are resolved.

Roster membership remains a separate invariant: every assigned player must be
the actual player instance from that team's roster, regardless of which lineup
roles are allowed.

## Self-contained desktop builds

HockeySim currently runs only from source (`dotnet run`); there is no published
build, and players would need the .NET 10 SDK or runtime installed.

Players should be able to download and run the game on Windows, macOS, and Linux
without installing .NET. Self-contained deployment bundles the runtime with the
app, which also keeps a machine-wide .NET update from changing behaviour within
an engine version ([reproducible saves](adr/0002-reproducible-saves.md)).

Direction:

- Record the deployment decision in an ADR: self-contained versus
  framework-dependent, single-file, ReadyToRun, trimming, and Native AOT, with
  reasons for what is rejected or deferred. This resolves the native-packaging
  row in [technology stack](tech-stack.md)'s deferred decisions.
- Choose the supported runtime identifiers (likely `win-x64`, `osx-arm64`,
  `osx-x64`, `linux-x64`) and minimum OS versions, and configure
  `HockeySim.Desktop` so one publish command per identifier produces a
  self-contained build.
- Only enable trimming if publishing produces no trim warnings and the trimmed
  build passes a native smoke test. Compiled bindings are already the default;
  `BuiltInComInteropSupport` and any reflection-dependent bindings need explicit
  attention.
- Publish each identifier in a CI job separate from the existing build-and-test
  checks, and smoke-test the output on each OS, including a machine with no .NET
  installed.
- Platform-native packaging (macOS `.app` bundle with signing and notarization,
  a Windows installer or MSIX, Linux AppImage or Flatpak), auto-update, and
  release versioning are separate follow-up work that builds on the publish
  output.

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

## Career history and advanced statistics

Desktop shows each player's current-season totals only: skater games, goals,
assists, and points, and goalie games, shots against, saves, goals against, and
save percentage. Totals are not kept once a later season begins, and nothing
beyond those counts is recorded.

Keep per-season totals as history when subsequent seasons exist, and show career
lines on the player profile. Shutouts can be derived from existing box scores;
most other advanced statistics need the match engine to record more first, such
as plus/minus, penalty minutes, time on ice (and with it goals-against average),
power-play and shorthanded production, and shots by skater. Add sortable
league-wide leader tables alongside them.
