# Derive player health from completed matches

Injuries (#56) give players state that changes between matches: current
injuries, which heal by date, and hidden wear on each body part, which only
grows. Two places could hold it in a save: as its own section of current
health, or as what each completed match did to health.

**Each completed match records its health changes.** The engine reports every
injury (as an `InjuryEvent` in the play-by-play) and the wear each player's body
parts took. Management converts them into the completed match's
`MatchHealthChanges`, and `Season.CompleteDay` applies them with the rest of the
day, so health follows from the history just as team records and season
statistics do. Loading replays the saved days, which rebuilds health by
construction and holds it to the same rules as play: an injured player who
cannot play cannot appear, and no day can break the injury cap. Saving current
health instead would need the replay to skip applying it, leaving two sources of
truth that could disagree. The cost is a few dozen wear entries per completed
match in the save.

**Injuries heal by date, not by counting down.** An injury stores the date of
its match and its recovery days, and is active until its return date. Healing
needs no state change, so days without matches heal injuries too, and a loaded
game heals exactly as an uninterrupted one.

**The match engine decides injuries; Domain holds the catalogue and the state.**
The catalogue (body part, recovery range, whether an injury can be played
through and at which rating reductions) is part of the game's rules, so it is in
Domain. Which events cause which injuries, and how often, is engine tuning and
changes seeded results like any other tuning. The engine reads health through
`MatchHealth`, which also lets a match play without causing injuries or wear,
for the preseason (#59).

**Until #57, a dressed player who cannot play misses the match.** The engine
treats them like an ejected skater from the opening faceoff: substitutes fill
their slots and they do not appear. #57 keeps such players out of lineups and
adjusts AI teams, after which this case arises only for the user's team, which
Management will reject.
