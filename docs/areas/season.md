# Season, schedule, and standings

The regular-season schedule, playing league days, applying results, and
ranking standings. Spans `HockeySim.Domain` (the season rules) and
`HockeySim.Management` (generation and orchestration). The match engine itself
is in [match engine](match-engine.md).

## Code map

| File | Holds |
| --- | --- |
| `Domain/Season.cs` | The season aggregate: `CompleteDay`, records, season statistics |
| `Domain/SeasonSchedule.cs`, `ScheduledMatch.cs`, `Match.cs` | The schedule and its invariants |
| `Domain/CompletedMatch.cs`, `CompletedMatchTeam.cs`, `SkaterBoxScore.cs`, `GoalieBoxScore.cs`, `ExpectedGoalTotals.cs`, `MatchTime.cs` | A completed match's box score and its reconciliation rules |
| `Domain/MatchDecision.cs` | Regulation, overtime, or shootout |
| `Domain/TeamRecord.cs`, `SkaterSeasonStatistics.cs`, `GoalieSeasonStatistics.cs` | Season totals |
| `Domain/StandingsRanking.cs`, `StandingsEntry.cs` | NHL tie-breaking |
| `Management/Scheduling/` | `ScheduleGenerator`: `MeetingPlanner` then `RoundCalendar` |
| `Management/SeasonPlay/LeagueDay.cs` | Plays a day's matches and converts results |
| `Management/GameManagement/GameManager.cs` | `AdvanceDay`, command serialization |
| `Management/GameManagement/Snapshots/SeasonSnapshot.cs`, `ScheduleSnapshot.cs`, `StandingsSnapshot.cs` | Read-only season views |

Domain paths are under `src/HockeySim.Domain/`, Management paths under
`src/HockeySim.Management/`.

## Tests

| File | Covers |
| --- | --- |
| `Domain.Tests/SeasonTests.cs` | Day atomicity, points per decision, terminal state |
| `Domain.Tests/BoxScoreTests.cs` | Completed-match reconciliation rules |
| `Domain.Tests/StandingsTests.cs` | Each tie-breaker, head-to-head cases, odd-game exclusion |
| `Domain.Tests/SeasonScheduleTests.cs` | Schedule invariants |
| `Management.Tests/ScheduleTests.cs` | Opponent matrix, home/away balance, dates, over several seeds |
| `Management.Tests/SeasonAdvancementTests.cs` | Advancing days, failures leaving nothing applied, threading |
| `Management.Tests/StandingsTests.cs` | Standings tables and snapshot isolation |
| `Management.Tests/FullSeasonTests.cs` | A shared fixture plays a full 1,344-match season and reconciles it |
| `Domain.Tests/TestLeague.cs`, `TestResults.cs` | Builders |

## Behaviour

A new game also generates the regular-season schedule from the same controlled
random stream, after the league. Domain's `SeasonSchedule` holds scheduled
matches by team identity in date order and rejects a team playing twice on one
date or against itself; Management's generator guarantees the league balance:
four meetings with each divisional opponent, three with each other
same-conference opponent, two with each opposite-conference opponent, and 42
home and 42 away matches per team. Generation has two stages: a meeting planner
decides who plays whom and who hosts, independent of dates, then a calendar
assigns dates. The current calendar packs the meetings into 84 rounds in which
every team plays once, opening on October 1 of the season year with a round
every other day. Authentic NHL dates, travel, rest, and rotation constraints are
not modelled; see [future features](../future-features.md#realistic-season-calendar). The schedule is exposed as a read-only
snapshot and is unchanged by managed-team selection.

Domain's `Season` aggregate holds the league, schedule, current date, the
completed-match history, team records, and player season statistics. A new
season's current date is opening day. `CompleteDay` accepts exactly one
completed match for each match scheduled on the current date, validates the
whole day (scheduled teams, decisive scores, statistics that reconcile with the
score, rostered players) before changing anything, and then moves to the next
calendar day; a scheduled match therefore cannot be completed twice, and a day
is never partly applied. Management's `AdvanceDay` command simulates the day's
matches in schedule order from the current lineups on one continuous random
stream, converts each Simulation result into a Domain completed match, and
commits the random state only after the season accepts the day. `GameManager`
serializes its commands, rejects commands issued from inside a day being played,
and rejects advancement once the season is complete. The engine is injected
through Simulation's `IMatchSimulator`; `MatchDecision` lives in Domain because
both the engine and the history use it.

Domain's `Season.RankStandings` ranks any group of league teams by the
[NHL tie-breaking procedure](https://www.nhl.com/info/standings-info/tie-breaking-procedure):
points, fewer games played, regulation wins, regulation and overtime wins, wins,
head-to-head points among the tied clubs, goal differential, then goals for.
It partitions each tied group criterion by criterion rather than sorting
pairwise, because head-to-head depends on which clubs are tied. Head-to-head
excludes the odd game (the first game in the city that hosted the extra meeting
between two clubs) and, for more than two clubs, compares the share of available
points; it is skipped for a group when any tied club has no counted games among
the others. The official text does not say what happens when head-to-head
separates only part of a larger tie; the clubs still level continue to goal
differential rather than recomputing head-to-head among themselves. That choice
is provisional and isolated in `StandingsRanking`. Teams level on every
criterion share a rank and keep league team order. Management exposes league,
conference, and division tables in `SeasonSnapshot.Standings`, each ranked
independently. Playoff qualification is not modelled.
