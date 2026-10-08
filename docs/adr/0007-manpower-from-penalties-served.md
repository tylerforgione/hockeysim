# Derive manpower from the penalties being served

Penalties (#51) give the event engine every strength state besides five-on-five
and three-on-three. Two sources could say how many skaters a team has: the
players on the ice, or the penalties it is serving. They differ during a delayed
penalty, when the team with the puck pulls its goalie for a sixth skater, and
will differ again when goalies are pulled late in a match (#52).

**Manpower comes from the penalty box.** `PenaltyBox` holds the timed penalties
on the game clock, and a team's manpower is five less the penalties it serves
(at most two), or in regular-season overtime three plus the opponent's excess.
The strength state, the units on the ice, and whether a goal is a power-play or
shorthanded goal all follow from it; an extra attacker is added on top. Reading
manpower from the players on the ice instead would make a delayed-penalty goal a
power-play goal and leave plus/minus wrong, as the NHL counts it at even
strength. Each `GoalEvent` therefore records its situation, so statistics do not
re-derive the penalty state from the play-by-play.

**Power-play opportunities are counted by the match**, alongside time on ice,
as an exception to deriving every statistic from the play-by-play
([ADR 0006](0006-event-based-match-engine.md)). An opportunity is an opponent
penalty that first gives the team the advantage, which can happen after the
penalty is called (four-on-four becomes five-on-four when the other penalty
ends), so it depends on penalty expiry that the play-by-play does not record.
Recording expiries as events would make the count derivable, but would add
events a box score does not need. Domain checks the counts it can: no power-play
goal without an opportunity, and no shorthanded goal unless the opponent had one.

**A penalized or ejected skater is replaced, not the group.** Rotations still
send out the lines, pairs, and units the user chose; anyone unavailable is
replaced in that group by the most rested available skater of the same kind.
This keeps usage targets and fatigue working without lineups for every
combination of absences, at the cost of occasional awkward groups, such as a
wing taking faceoffs for a centre in the box. Penalties are not called on a team
with fewer than eight available skaters, so a substitute always exists.

**A penalty shot is a shootout attempt in the run of play.** It uses the
shootout's shooter-against-goalie chance, and its xG is the reference shootout
rate, so a reference shooter scores at its xG like any other attempt. Goals on
penalty shots count neither as power-play or shorthanded goals nor toward
plus/minus, and end no penalty.

**The play-by-play is the test oracle's input.** Simulation tests replay each
match's penalties and goals through an independent statement of the manpower
rules and compare the expected strength, box occupancy, power-play
opportunities, and time on ice with the engine's output, rather than exposing
engine internals to tests.

The rates are provisional tuning values in `MatchTuning`, calibrated so a
generated league averages about three power-play opportunities a team at a 20%
success rate; calibration is #53. Changing them changes seeded results, an
engine version change for [reproducible saves](0002-reproducible-saves.md).
