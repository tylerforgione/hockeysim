# Derive the playoff bracket and dates in the season

The regular season and preseason schedules are generated in Management, because
they draw on the controlled random stream, and handed to Domain's `Season` as
fixed schedules. The playoffs cannot work that way (#60): who plays whom depends
on the final standings and on each series' winner, and a series stops scheduling
games once it is decided. Their schedule has to grow as results arrive.

**Domain's `Season` builds the playoffs itself.** When `CompleteDay` finishes the
final regular-season day, the season seeds the bracket from its own standings
and schedules every first-round game one; each later day of playoff games
schedules the next game of every undecided series, or starts the next round
once a round is decided. The bracket, home ice, and the calendar (lockstep
rounds, a game every other day) use no randomness, so they follow from the
regular-season and playoff results alone.

The alternative was a Management scheduler that adds playoff games after each
day, with Domain only validating them, matching where the other generators
live. Every caller that advances the season would then have to remember to call
it, including the save restorer while it replays days, and a missed call would
leave a season whose schedule disagrees with its results. Keeping it in the
season means a loaded game rebuilds the bracket and its dates by replaying the
saved results through `CompleteDay`, exactly as it does team records and health
([ADR 0008](0008-health-derived-from-completed-matches.md)); the save holds only
the playoff results, and cannot hold a bracket that contradicts them.

The cost is that scheduling rules now live in two projects: generated schedules
in Management and the playoff calendar in Domain. If the playoff calendar ever
needs randomness or a richer calendar specification (see
[realistic season calendar](../future-features.md#realistic-season-calendar)),
Management can pass a calendar policy into the season, but the season should
keep deciding when a series or round needs its next game.
