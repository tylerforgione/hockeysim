# Split the match engine into parts that share one match state

The event-based engine ([ADR 0006](0006-event-based-match-engine.md)) grew in
one class, `MatchPlay`, to over 1,200 lines as penalties, goalie pulls, and
injuries arrived (#51, #52, #44). Every engine change had to read and edit most
of that file. Splitting it (#74) raised one question: how do the pieces reach
the state they all read and change, such as the puck, the clock and score, a
pending faceoff, a delayed penalty, and the play-by-play?

**One `MatchState` holds the shared state, and each part receives it with only
the parts it calls.** `MatchState` keeps the two sides, the penalty box, the
clock, score, puck, pending faceoff, delayed penalty, the play-by-play, and the
single `ControlledRandom`, with small helpers (the opponent, the players on the
ice, weighted choice of a skater) but no play rules. The rules live in parts
built by `MatchPlay`, each taking the state and its collaborators through its
constructor: `SkillComparison`, `GoaliePulls`, `InjuryPlay`, and
`PenaltyAssessment` take only the state; `Possession` takes `PenaltyAssessment`;
`ShotPlay` takes those; `FoulPlay` adds `ShotPlay`; `ZonePlay` adds `FoulPlay`.
`MatchPlay` keeps the periods, clock, line changes, and the result. A part's
constructor therefore states what it can affect, and the parts form no cycle.

Keeping the graph acyclic decided where some rules sit. A stoppage calls a
pending delayed penalty, so stoppages and puck changes (`Possession`) depend on
`PenaltyAssessment`, not the other way round. A penalty shot assesses its
penalty and scores like any goal, so it is taken in `ShotPlay`, which
`FoulPlay` calls when it awards one.

**Partial classes were rejected.** They would have cut the file sizes with less
change, but every partial still sees every field, so a reader of one file
cannot tell what it touches and the coupling the split was meant to expose
stays hidden. **Passing `MatchPlay` itself to each part**, with its fields made
accessible, was rejected for the same reason, and because it would make every
part depend on the coordinator.

The cost is a mutable state object with public setters that any part holding it
can change; it is internal to Simulation and lives for one match, so nothing
outside a match can reach it. The parts hold references rather than being
pure functions, so tests still exercise them only through `MatchSimulator`, as
before.

**The split preserves seeded results.** Every random draw comes from the one
generator in the state, so moving code between parts must keep the draws in the
same order. The split was checked by comparing the complete results of
thousands of seeded matches, play-by-play included, before and after; changing
the order would change seeded results, an engine version change for
[reproducible saves](0002-reproducible-saves.md).
