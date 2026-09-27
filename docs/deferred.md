# Deferred game-design decisions

This document records known game-design limitations that are intentionally
left for later work. Each item describes current behavior and the direction to
consider; it does not replace a scoped GitHub issue when implementation begins.

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
