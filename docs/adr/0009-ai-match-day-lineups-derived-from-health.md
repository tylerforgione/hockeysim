# Derive AI teams' match-day lineups from health

Injured players who cannot play must not be dressed (#57). The managed team's
user fixes their own lineup; AI teams must replace injured players from their
healthy scratches and restore their lineups when the players return. Restoring
needs the lineup a team would dress if everyone were healthy, so something has
to remember it.

**An AI team's `Team.Lineup` stays the lineup it prefers, and its match-day
lineup is worked out each day.** Management's `MatchDayLineup` replaces every
dressed player who cannot play on the current date with the best-suited healthy
scratch, who also takes their unit and extra-attacker slots. League days dress
AI teams in that lineup, and snapshots show it. As players heal, the preferred
lineup returns by itself.

The alternative was to change AI teams' lineups as injuries happen and keep a
second, preferred lineup to restore from. That puts two lineups in the save that
must agree with each other and with health, and needs restore logic of its own.
Deriving the match-day lineup keeps the save unchanged, follows from health just
as health follows from the completed matches ([ADR 0008](0008-health-derived-from-completed-matches.md)),
and gives a loaded game the same lineups as an uninterrupted one.

The cost is that an AI team cannot yet choose a lineup that differs from its
preferred one for other reasons, such as resting a player; when AI teams manage
their own lineups, that choice becomes their preferred lineup and the same
replacement still applies on top of it.

The managed team is not adjusted: Management rejects a day on which it would
dress a player who cannot play, so the user decides every replacement. The
engine still copes with such a player, as ADR 0008 describes, but Management no
longer gives it one.
