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