# Simulate matches as possession steps and derive statistics from the play-by-play

The first match engine divided play into 30-second ticks in which each team
sent out a weighted-random unit and got one chance to shoot. It produced
plausible scores but no events to build shifts, special teams, injuries, or
advanced statistics on, and line usage was a fixed weight rather than ice time.

The engine now plays a match as **possession steps** on a game clock in whole
seconds. In each step the team with the puck tries to move it up the ice and
create a shot, and the defending team tries to win it back; steps record
faceoffs, shot attempts, goals, hits, takeaways, and giveaways with the players
on the ice. Shifts and fatigue decide who is on the ice. A finer model (puck
position on a rink, individual player movement) was rejected for now: the
management game needs believable statistics and consequences of lineup
choices, not a physical simulation, and possession steps keep a 1,344-match
season to about a second. A coarser one (keeping ticks and adding event types)
would not give shifts, zone starts, or rebounds a place to live. The step model
can grow strength states, penalties, and pulled goalies (#51, #52) by changing
who is on the ice and how often each outcome happens.

**Every statistic is derived from the play-by-play**, except time on ice, which
comes from the shifts. Reconciliation (shots against equal to the opponent's
shots, faceoff wins equal to the opponent's losses, plus/minus following the
players on the ice at each goal) therefore holds by construction instead of
being kept in step by hand. Domain still checks it, because completed matches
also come from saves.

**The play-by-play stays in Simulation's `MatchResult`.** Management converts a
result into a Domain completed match that keeps only the box score, and saves
store only that. A season's play-by-play would add roughly 330,000 events with
their on-ice players to every save and every load replay, for a feature
(viewing a match's events) that does not exist yet. A future match viewer or
play-by-play page can decide whether to keep events for some matches, and
whether to save them, when it is designed.

**Expected goals come from the shot's context alone** (danger level, rebound,
rush), and the engine splits that chance into reaching the net and beating the
goalie, each shifted by the shooter's and goalie's ratings from a reference
rating. A reference shooter against a reference goalie therefore scores exactly
at the xG rate, so the gap between goals and xG measures finishing and
goaltending, which is what goals saved above expected (#54) reports. The model
and its values are documented in [architecture](../architecture.md#expected-goals).

Overtime is an input (`OvertimeFormat`) rather than a rule inside the engine, so
playoff overtime (unlimited twenty-minute five-on-five sudden-death periods) is
available before playoffs are modelled. Regulation consumes the random stream
identically under either format.

The tuning values are provisional and kept together in `MatchTuning` so that
calibration (#53) and [configurable match tuning](../future-features.md#configurable-match-tuning)
can adjust them in one place. Changing any of them changes seeded results, so it
is a change of engine version for [reproducible saves](0002-reproducible-saves.md).
