# Season, schedule, and standings

The preseason and regular-season schedules, playing league days, applying
results, ranking standings, and the playoffs. Spans `HockeySim.Domain` (the season rules) and
`HockeySim.Management` (generation and orchestration). The match engine itself
is in [match engine](match-engine.md).

## Code map

| File | Holds |
| --- | --- |
| `Domain/Season.cs`, `SeasonPhase.cs` | The season aggregate: phases, `CompleteDay`, records, season statistics, player health |
| `Domain/SeasonTotals.cs` | Team records and team and player statistics for one part of the season: the regular season or the playoffs |
| `Domain/PlayerHealth.cs`, `Injury.cs`, `InjuryCatalogue.cs`, `InjuryDefinition.cs`, `InjuryType.cs`, `BodyPart.cs`, `InjuryCap.cs`, `ExpectedReturn.cs` | Injuries, healing by date, the staff's expected return, hidden wear, and the injury cap |
| `Domain/MatchHealthChanges.cs`, `MatchInjury.cs`, `InjuryCause.cs`, `WearGain.cs` | A completed match's injuries (with their causes) and wear |
| `Domain/SeasonSchedule.cs`, `ScheduledMatch.cs`, `Match.cs` | The schedule and its invariants |
| `Domain/CompletedMatch.cs`, `CompletedMatchTeam.cs`, `SkaterBoxScore.cs`, `GoalieBoxScore.cs`, `ExpectedGoalTotals.cs`, `MatchTime.cs` | A completed match's box score and its reconciliation rules |
| `Domain/MatchGoal.cs`, `MatchPenalty.cs`, `MatchClock.cs`, `GoalSituation.cs`, `Infraction.cs`, `PenaltyKind.cs` | The scoring and penalty summaries |
| `Domain/ShotTotals.cs`, `SituationalShotTotals.cs`, `StrengthSituation.cs` | Shot totals for and against by strength situation, and their shares (Corsi, Fenwick, xG) |
| `Domain/MatchDecision.cs` | Regulation, overtime, or shootout |
| `Domain/TeamRecord.cs`, `TeamSeasonStatistics.cs`, `SkaterSeasonStatistics.cs`, `GoalieSeasonStatistics.cs`, `SeasonAverages.cs` | Season totals and the rates derived from them |
| `Domain/StandingsRanking.cs`, `StandingsEntry.cs` | NHL tie-breaking |
| `Domain/WildCardStandings.cs`, `PlayoffRace.cs`, `PlayoffStatus.cs` | Wild-card qualification, and clinch and elimination statuses |
| `Domain/Playoffs.cs`, `PlayoffSeries.cs`, `PlayoffSeed.cs`, `PlayoffRound.cs` | Seeding the bracket, best-of-seven series and home ice, the playoff calendar, playoff totals, the champion |
| `Management/Scheduling/` | `ScheduleGenerator`: `MeetingPlanner` then `RoundCalendar`; `PreseasonGenerator`; `RoundRobin` rounds shared by both |
| `Management/SeasonPlay/LeagueDay.cs` | Plays a day's matches and converts results |
| `Management/GameManagement/GameManager.cs` | `AdvanceDay`, command serialization |
| `Management/GameManagement/Snapshots/SeasonSnapshot.cs`, `ScheduleSnapshot.cs`, `StandingsSnapshot.cs`, `PlayoffsSnapshot.cs` | Read-only season views |

Domain paths are under `src/HockeySim.Domain/`, Management paths under
`src/HockeySim.Management/`.

## Tests

| File | Covers |
| --- | --- |
| `Domain.Tests/SeasonTests.cs` | Day atomicity, points per decision, the move to the playoffs |
| `Domain.Tests/PreseasonTests.cs` | Phases, preseason results counting toward nothing, no preseason injuries or wear, the move to opening day |
| `Domain.Tests/InjuryTests.cs`, `SeasonHealthTests.cs` | The injury catalogue, healing, expected returns, playing through, wear, the cap, and applying a day's health changes |
| `Domain.Tests/BoxScoreTests.cs` | Completed-match reconciliation rules |
| `Domain.Tests/MatchSummaryTests.cs` | Summaries agreeing with the box scores, shot totals across teams and skaters |
| `Domain.Tests/ShotTotalsTests.cs` | Shot totals, their shares, reversing, and adding |
| `Domain.Tests/SeasonStatisticsTests.cs` | Accumulating every statistic and each derived rate and percentage, undefined values |
| `Domain.Tests/StandingsTests.cs` | Each tie-breaker, head-to-head cases, odd-game exclusion |
| `Domain.Tests/PlayoffRaceTests.cs` | Wild-card qualification, each status on constructed scenarios, tie-breakers in guarantees, final statuses |
| `Domain.Tests/PlayoffsTests.cs` | Seeding and wild-card crossover, home ice in each round, the 2-2-1-1-1 pattern and dates, decided series, champion and completion, no shootouts, separate totals, playoff injuries |
| `Domain.Tests/SeasonScheduleTests.cs` | Schedule invariants |
| `Management.Tests/ScheduleTests.cs` | Opponent matrix, home/away balance, dates, over several seeds |
| `Management.Tests/PreseasonTests.cs` | The preseason schedule (opponents, dates, home balance), exclusion from every total, the move to opening day, determinism, saving mid-preseason |
| `Management.Tests/SeasonAdvancementTests.cs` | Advancing days, failures leaving nothing applied, threading |
| `Management.Tests/StandingsTests.cs` | Standings and wild-card tables and snapshot isolation |
| `Management.Tests/FullSeasonTests.cs` | A shared fixture plays a full 1,344-match season and its playoffs and reconciles them: every season total and summary, every playoff status held to the end, the bracket, home ice and dates, and playoff totals |
| `Management.Tests/PlayoffsTests.cs` | Overtime format by phase, saving and loading mid-series, continuing a loaded game to the same champion |
| `Domain.Tests/TestLeague.cs`, `TestResults.cs`, `Management.Tests/PreseasonPlay.cs` | Builders; most Management tests start on opening day after playing the preseason |

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

The preseason is generated next, from the same stream. Each team plays each of
its seven divisional opponents once: around a shuffled circle of each division,
a team hosts the next three teams and the team opposite it is host or visitor by
chance, so every team has three or four home matches. The pairings form seven
rounds in which every team plays (a round robin within each division), one round
every other day ending the day before opening day (September 18 to 30). Its
schedule is exposed beside the regular season's as
`ScheduleSnapshot.PreseasonMatches`. Training camp and roster cuts are not
modelled; see [future features](../future-features.md#preseason-roster-decisions).

Domain's `Season` aggregate holds the league, the preseason and regular-season
schedules, current date, the completed-match history of each, team records, team
and player season statistics, and each rostered player's health. A new season's
current date is its first preseason day, or opening day without a preseason. Its
`Phase` is the preseason until opening day, the regular season until every
regular-season match is played (`IsRegularSeasonComplete`), then the playoffs;
`CurrentDateMatches` reads the current phase's schedule. Each phase keeps its
own history rather than tagging one: preseason matches in `PreseasonMatches`,
regular-season matches in `CompletedMatches`, and playoff matches in
`Playoffs.CompletedMatches`. The records, statistics, standings, head-to-head,
and playoff race only ever see the regular season; health sees the regular
season and the playoffs. `CompleteDay` accepts exactly one
completed match for each match scheduled on the current date, validates the
whole day (scheduled teams, decisive scores, statistics that reconcile with the
score, rostered players, no appearance by a player who cannot play, and the
injury cap, and no shootout in the playoffs) before changing anything, applies
each match's injuries and wear to the players' health, and then moves to the
next calendar day; a scheduled match
therefore cannot be completed twice, and a day is never partly applied. A
preseason day is validated the same way, is also rejected if any match injures
or wears anyone, and its results are only kept. Management's `AdvanceDay` command first rejects the day,
changing nothing, if the managed team plays on the current date with a dressed
player who cannot play (see
[injured players in lineups](players-and-lineups.md#injured-players-in-lineups)).
It then simulates the day's matches in schedule order, the managed team from its
lineup and each AI team from its match-day lineup, with the players' health on
the current date, on one continuous random stream, preseason matches with injuries switched off
in the engine (`MatchHealth.InjuriesPossible`) and playoff matches with playoff
overtime; converts each Simulation
result into a Domain completed match; commits the random state only after the
season accepts the day; and then delivers the head trainer's injury reports. `GameManager`
serializes its commands, rejects commands issued from inside a day being played,
and rejects advancement once the season is complete, which is when the playoffs
crown a champion. The engine is injected
through Simulation's `IMatchSimulator`; `MatchDecision`, `GoalSituation`,
`Infraction`, and `PenaltyKind` live in Domain because both the engine and the
history use them.

### Player health

`Season.HealthOf` gives a player's `PlayerHealth`: every injury this season and
the hidden wear on each body part, rebuilt from the completed matches (see
[ADR 0008](../adr/0008-health-derived-from-completed-matches.md)). Each completed
match carries its `MatchHealthChanges`: the injuries (period, time, team,
player, type, cause, and recovery days) and the wear each appearing player's body parts
took. An injury is active from its match date until the date its recovery days
later, so injuries heal on days without matches too; a player can play on a date
unless an active injury cannot be played through, and plays through the rest at
the sum of their rating reductions. Wear only grows. `CompleteDay` rejects
injuries that would leave a team fewer than 18 skaters or 2 goalies able to play
(`InjuryCap`), and injuries or wear for players who did not appear.

Each `PlayerSnapshot` lists the player's injuries that have not healed on the
current date: type, body part, whether it can be played through, its match date,
and the staff's `ExpectedReturn`, a range a quarter of the recovery time either
side of the return date, kept within the injury's catalogue range. The range is
worked out from the injury alone, so it uses no randomness. The exact recovery
time and wear are hidden and never reach a snapshot.

A completed match keeps the box score and, from the play-by-play, a scoring
summary (each player goal's period, time, team, scorer, assists, situation, and
whether the net was empty) and a penalty summary (each penalty's period, time,
team, player, infraction, and kind). The rest of the play-by-play is not kept;
see [future features](../future-features.md#play-by-play-retention). The
completed match rejects summaries out of time order, or that do not credit
exactly each skater's goals, assists, power-play, shorthanded, and empty-net
goals and assists, and penalty minutes.

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
independently.

### Playoff qualification

Sixteen teams qualify, in the NHL wild-card format: the top three of each
division, then the two best of each conference's other teams (the wild cards).
`Season.RankWildCard` ranks a conference this way: each division's table
supplies its top three with their division ranks, and the conference's other
ten teams are ranked among themselves, so head-to-head there considers only
teams tied within the race. Teams level on every criterion at a cut-off are
taken in table order, which is league team order. Once the regular season
is complete, these teams are the playoff qualifiers.

`Season.PlayoffStatuses` reports each team's guaranteed status, shown in the
standings by the NHL's letters: clinched a playoff spot (x), the division (y),
the conference (z), or the best record in the league (p), or eliminated (e).
A team reports only its strongest. During the season `PlayoffRace` reports a
status only when no outcome of the remaining schedule could change it, and
errs towards reporting later. It compares teams pairwise on the per-team
criteria (points, final games played, regulation wins, regulation and overtime
wins, wins), using each team's worst finish (losing every remaining game in
regulation) and best (winning every one, in regulation). A team is certainly
ahead of another only when its worst finish outranks the other's best; a team
that could draw level on all of those is treated as possibly ahead, because
head-to-head and goals depend on which clubs tie and on future scores. A team
clinches the best record, its conference, or its division when no team in that
group can finish ahead of it. It clinches a playoff spot when at most two
division rivals can finish ahead of it, or when at most one wild-card contender
can: a team from a division that finishes ahead of it but outside that
division's top three has the division's top three ahead of it too, so each
division contributes at most (teams that can finish ahead − 3) contenders. It
is eliminated when three division rivals and, by the same count over teams
certainly ahead, two wild-card contenders are certainly ahead. Counting teams
pairwise also ignores that rivals take points from each other, which again only
delays a status. In a simulated season most statuses appear over the last
quarter of the schedule. Once the season is complete there is nothing left to
guarantee: the final standings, with every tie-breaker, decide every team.
Statuses are derived from the season and not saved. Management attaches each
team's status to every standings entry and adds each conference's wild-card
view to `SeasonSnapshot.Standings`.

### Playoffs

Completing the final regular-season day seeds the playoffs (`Season.Playoffs`,
`null` until then) from each conference's final wild-card standings. The
bracket is the NHL's: within each division's bracket the division winner meets a
wild card (the conference's better division winner, by the full tie-breakers,
meets the lower wild card) and second meets third; the bracket's winners meet
in the second round, the two bracket winners of a conference in the conference
final, and the conference champions in the final. Each round's series are kept
in bracket order (conference, then division bracket), so neighbouring series
feed the same series in the next round. A `PlayoffSeed` records how each team
qualified: its division finish, or its wild-card rank.

Each `PlayoffSeries` is best of seven. The higher-ranked team hosts games one,
two, five, and seven (2-2-1-1-1). As in the [NHL format](https://www.nhl.com/info/standings-info/playoff-format),
through the second round the higher-ranked team is the one that placed higher
in its bracket regardless of points (a division qualifier above a wild card, a
better division finish above a worse one), so a wild card hosts no one before
the conference final even with more points. From the conference final it is the
better regular-season record, the two teams ranked against each other with
every tie-breaker.

The playoff calendar is derived in the season, not generated (see
[ADR 0011](../adr/0011-playoffs-derived-in-the-season.md)). Play starts two days
after the final regular-season day. Rounds run in lockstep: every undecided
series in a round plays on the same days, every other day, and each next game
is scheduled only once the previous one is played, so a decided series schedules
no more. The next round starts two days after the round's last game.
`Playoffs.Schedule` grows as games are scheduled, and
`ScheduleSnapshot.PlayoffMatches` shows it. Days between games pass without
matches. A realistic NHL playoff calendar is not modelled; see
[future features](../future-features.md#realistic-season-calendar).

Playoff matches are played like regular-season matches but with playoff overtime
(see [match engine](match-engine.md)), and `CompleteDay` rejects a playoff
shootout. They injure and wear players like the regular season. Their records
and statistics are kept apart in the playoffs' own `SeasonTotals`, for the
sixteen qualifiers only, and never reach the standings, the playoff statuses,
or head-to-head. When the final is decided, its winner is the `Champion`, the
season `IsComplete`, and no further days can be played; the current date is the
day after the final game. Management exposes the bracket, every series' games
and next game, the champion, the playoff results, and the playoff totals in
`SeasonSnapshot.Playoffs`.

### Season statistics

The regular season and the playoffs each accumulate every box-score statistic,
excluding the shootout, in their own `SeasonTotals`. Each
skater's totals sum their goals, assists, plus/minus, time on ice, shots, shot
attempts, hits, blocks, faceoffs won and lost, takeaways, giveaways, xG, penalty
minutes, power-play, shorthanded, and empty-net goals and assists, and their
on-ice shot totals by strength situation. Each goalie's totals sum shots and
goals against, xG against, time in net, and shutouts: starts in which the goalie
was charged with no goal, so an empty-net goal or a shootout does not prevent
one. Each team's `TeamSeasonStatistics` sum its power-play goals and
opportunities, the opponents' power-play goals and opportunities (its penalty
kill), shorthanded goals, faceoffs, and its shot totals by situation; wins,
losses, and goals stay in the `TeamRecord`.

Domain derives the rates and percentages, each undefined (`null`) until it can
be calculated:

| Value | Formula | Undefined until |
| --- | --- | --- |
| Corsi, Fenwick, shots, goals, xG percentage | for ÷ (for + against) of attempts, unblocked attempts, shots, goals, or xG | either side has one |
| Faceoff percentage | won ÷ (won + lost) | a faceoff is taken |
| Time on ice per game | total ÷ games, to the nearest second | a first appearance |
| Save percentage | saves ÷ shots against | a shot is faced |
| Goals-against average | goals against × 60 minutes ÷ time in net | time in net |
| Goals saved above expected (GSAx) | xG against − goals against | always defined |
| Power-play percentage | power-play goals ÷ opportunities | a first opportunity |
| Penalty-kill percentage | 1 − power-play goals against ÷ times shorthanded | the team is first shorthanded |

Power-play and shorthanded points are goals plus assists. Desktop shows the
five-on-five shot totals as the advanced measures; the others are kept for
situational views (see [future features](../future-features.md#situational-statistics-views)).
Management exposes team statistics in `SeasonSnapshot.TeamStatistics` and passes
Domain's derived values through the player snapshots rather than recomputing
them.
