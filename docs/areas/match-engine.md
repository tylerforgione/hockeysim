# Match engine

`HockeySim.Simulation`: plays one match from two lineups and a random state and
returns the result. It never changes the game world; Management applies results
([ADR 0001](../adr/0001-headless-game-ownership.md)). Design decisions:
[ADR 0006](../adr/0006-event-based-match-engine.md) (event-based engine) and
[ADR 0007](../adr/0007-manpower-from-penalties-served.md) (manpower from
penalties served).

## Code map

Paths are under `src/HockeySim.Simulation/`.

| File | Holds |
| --- | --- |
| `IMatchSimulator.cs`, `MatchSimulator.cs` | The entry point Management calls; builds a `MatchPlay` |
| `OvertimeFormat.cs` | Regular-season or playoff overtime |
| `Play/MatchPlay.cs` | The match loop and every play decision (see below) |
| `Play/MatchSide.cs` | One team during a match: who is on the ice, line changes, strength state, pulled goalie |
| `Play/Rotation.cs` | The groups rotated through one set of positions, and target ice-time shares |
| `Play/SkaterState.cs`, `Play/PlayerStrength.cs` | A skater's energy and composite strengths from ratings |
| `Play/PenaltyBox.cs` | Timed penalties on the game clock |
| `Play/ShootoutPlay.cs` | Shootouts (penalty shots reuse its attempt) |
| `Play/ExpectedGoalsModel.cs` | xG by shot context (its values are in `MatchTuning`) |
| `Play/MatchTuning.cs` | Every tuning constant, calibrated to NHL averages (see [Calibration](#calibration)) |
| `Play/MatchStatisticsBuilder.cs` | Derives all statistics from the play-by-play |
| `Play/OnIceSkater.cs` | A skater in a slot: role, side, and strengths adjusted for out-of-position and off-hand play |
| `Play/Zone.cs`, `Play/Probability.cs` | Small helpers |
| `Events/` | Play-by-play event records and their enums (goal situations, infractions, and penalty kinds are in Domain, since completed matches record them too) |
| `MatchResult.cs`, `MatchTeamResult.cs`, `SkaterMatchStatistics.cs`, `GoalieMatchStatistics.cs`, `Shootout*.cs` | The returned result |
| `Randomness/ControlledRandom.cs`, `RandomState.cs` | The single deterministic generator, also used by Management |

`MatchPlay.cs` is large, so it helps to search by method rather than read it
whole:

| Concern | Methods |
| --- | --- |
| Periods and clock | `Play`, `PlayPeriod`, `EndPeriod`, `Elapse`, `StepSeconds` |
| Possession by zone | `PlayStep`, `PlayDefensiveZone`, `PlayNeutralZone`, `PlayOffensiveZone`, `PlayRebound`, `Turnover`, `GiveTo`, `Stoppage`, `TakeFaceoff` |
| Shots and goals | `Shoot`, `MissFromDistance`, `ScoreGoal`, `ChooseAssist`, `DrawDanger`, `ChooseShooter`, `BaseBlockChance` |
| Physical play | `Hit`, `AfterHit` |
| Penalties and manpower | `TryFoul`, `CommitFoul`, `TakePenaltyShot`, `EndDelayedPenalty`, `AssessPenalties`, `ExpirePenalties`, `ApplyManpower`, `CountPowerPlay`, `Manpower` |
| Pulling the goalie | `PullGoaliesToTieTheMatch`, `PullOrReturnGoalie`, `ReturnGoaliesPulledToTieTheMatch` |
| Skill comparison | `AttackingEdge`, `SkillEdge`, `AttackingEdgeFactor`, `HitRateFactor` |

## Tests

`tests/HockeySim.Simulation.Tests/`. Two test files restate the rules on their
own and act as oracles for the engine: `ManpowerReplay.cs` (the strength state
the penalties being served call for) and `GoaliePullRule.cs` (when a team may
pull its goalie to tie a match). Keep them independent of the engine's code.

| File | Covers |
| --- | --- |
| `MatchResultInvariantTests.cs` | Result invariants across many seeds |
| `MatchReproducibilityTests.cs` | Determinism, unchanged inputs |
| `MatchStrengthTests.cs` | Relative statistical checks for lineup strength and goalie quality |
| `PlayByPlayTests.cs` | Event order, faceoff restarts, on-ice players, xG on shots |
| `EventStatisticsTests.cs`, `MatchStatisticsTests.cs` | Statistics recounted from the events and reconciled |
| `OnIceStatisticsTests.cs` | On-ice and team shot totals recounted by situation, with `ManpowerReplay` as the oracle |
| `OvertimeTests.cs` | Three-on-three, shootouts, playoff overtime |
| `ShiftAndFatigueTests.cs`, `PhysicalPlayTests.cs` | Ice-time shares, stamina, hits |
| `LineupFitTests.cs` | Relative statistical checks for out-of-position and off-hand play |
| `PenaltyTests.cs` | Every manpower rule, replayed through `ManpowerReplay` |
| `GoaliePullTests.cs` | Pulls, empty-net goals, extra attackers, checked against `GoaliePullRule` |
| `TestTeams.cs`, `TestMatches.cs`, `TestBiography.cs` | Builders |

Management's `FullSeasonTests.cs` plays a whole season through the engine, and
its `CalibrationTests.cs` plays a generated league's season and checks its league
averages against the NHL targets in `NhlTargets.cs`, measured by
`LeagueMeasurements.cs` (see [Calibration](#calibration)).

## Using the engine

Simulation plays a match as play-by-play hockey events from both teams' lineups
as they stand at match start. It takes an explicit random state and an
`OvertimeFormat`, and returns the state after the match with the result,
without changing the teams. Management plays every
league match with regular-season overtime.

## How a match is played

`MatchSimulator` plays a match on a game clock in whole seconds as a sequence of
possession steps. In each step the team with the puck tries to move it from its
own zone through the neutral zone into the attacking zone and create a shot,
and the defending team tries to win it back. A step can exit or enter a zone
(carrying the puck in starts a rush; a dump-in is contested), cycle, end in a
shot attempt, a hit, or a turnover, or stop play for icing, offside, a frozen
puck, or a puck out of play. Every stoppage and period start is restarted with a
faceoff between the centres on the ice, decided by their faceoff ratings, and
its location (centre ice or either team's zone) decides who starts where.
Outcome chances are base rates shifted in log-odds by the gap between the
attackers' offence and the defenders' defence, so stronger units shoot more and
turn the puck over less. Turnovers are recorded as the defender's takeaway or
the carrier's giveaway only some of the time; the rest are loose pucks credited
to nobody, as in NHL scoring.

Play is five-on-five in regulation unless penalties are being served (see
[Penalties and special teams](#penalties-and-special-teams)). At five-on-five,
forward lines and defence pairs rotate independently; every other strength
state rotates the lineup's units for it. Each skater's energy falls on the ice and recovers on the bench,
at rates set by stamina, and a tired skater plays below their ratings. A group
changes when it tires or has been out for a long shift, on the fly when its team
is not attacking or at a stoppage once it has been out a while; the coach then
sends out the rested group furthest behind its target share of ice time (forward
lines 36/24/22/18%, pairs 48/31/21%). Fatigue evens out what the groups actually
play, so across a season forward lines get about 32/28/22/19% of the forwards'
ice time and pairs about 37/34/29% of the defence's. Higher lines therefore play
more, and a low-stamina group has shorter shifts and plays less. Skaters recover partly in
each intermission.

A shot attempt draws a context (see below) and a shooter from the skaters on
the ice: forwards from the slot, defence from the point, better attackers more
often. A defending skater may block it, more likely for a point shot and for a
good shot blocker. An unblocked attempt may miss the net, depending on the
shooter's accuracy, and a shot on goal beats the goalie depending on the
shooter's finishing and the goalie's reflexes and positioning. A save may give
up a rebound, less often for a goalie with good rebound control, which the
attackers can shoot again within a second or two. Each goal credits up to two
assists to on-ice teammates, favouring playmakers. Hits come from the defending
team, more often and more effectively for skaters with high checking,
toughness, and size; size moves physicality by about a rating point per inch
over 6'1" and per four pounds over 200 lb, within limits, and also helps a puck
carrier keep the puck through a hit.

Every slot on the ice has a role (centre, wing, or defence) and, for a wing or
defence slot that has one, a side; a skater plays a little below their ratings
in a slot that does not suit them (see
[Positions and handedness](#positions-and-handedness)).

The tuning values live together in `Simulation/Play/MatchTuning.cs`, measured
from a reference rating of 65, the centre of generated talent, and calibrated so
a generated league's season approximates recent NHL league averages (see
[Calibration](#calibration)).

A match tied after regulation is decided by its `OvertimeFormat`. Regular-season
overtime is five minutes of three-on-three sudden death with the lineup's
three-on-three units, then a shootout: three rounds, then sudden-death rounds,
with shooters in order of shootout strength (accuracy and puck control) against
the goalie's goaltending; a skater ejected or still serving a penalty when
overtime ends does not shoot. Playoff overtime is as many twenty-minute
five-a-side sudden-death periods as it takes, with no shootout. Regulation is
played identically under either format. Penalties carry over from regulation
into either overtime. Goalies are pulled to tie a match only in regulation.

### Positions and handedness

Any skater can fill any skater slot. A forward line plays left wing, centre,
and right wing; a pair, left and right defence; a unit, its format's roles and
sides; and an extra attacker, a wing with no side. A skater's strengths in the
slot are their ratings adjusted in rating points by `OnIceSkater`, then scaled
by fatigue as usual:

| Assignment | Effect in the slot |
| --- | --- |
| Centre on the wing, or winger at centre | Offence, defence, and faceoffs −3 |
| Forward on defence, or defenceman at forward | Offence, defence, and faceoffs −8 |
| Wing on the off-hand side | Defence −1.5 and puck protection −2.5 (board play, breakouts); finishing +1.5 (one-timers on the forehand) |
| Defence on the off-hand side | Defence −2 (breakouts); offence −1.5 (pinches to keep the puck in) |

A skater is off-hand on the side opposite their handedness. The centre, a lone
wing or defence player in a unit, and an extra attacker have no side, so
handedness does not matter there. The effects combine, so a right-shot
defenceman at left wing takes both the larger position penalty and the off-hand
wing effects. Offence and defence move the attacking edge (shot rates, danger,
turnovers), faceoffs decide draws taken from the centre slot, puck protection
keeps the puck through a hit, and finishing beats the goalie.

Out of position matters a good deal: between equally rated teams, one with its
wings and defence trading places on the top three lines and pairs wins about
44% of matches. Off-hand play is deliberately small, as in the NHL, where many left
shots play the right side: a team with every wing and defence player off-hand
takes a little under half of the shot attempts (about 49.3%) against the same
team on its natural sides. A
substitute for a skater in the box or ejected takes the slot's role and side, so
the effects apply to them too. Splitting positions by side is a
[future feature](../future-features.md#side-specific-positions).

### Penalties and special teams

Penalties come from the play. In each step of possession the defending team
may foul (more often when it is being beaten on skill, the manpower advantage
aside) and the attacking team may too, each more often when its skaters on the
ice are undisciplined or tired, since fatigue lowers effective discipline. The
offender is drawn from the skaters on the ice, favouring the undisciplined, and
the infraction from the situation: hooking, tripping, holding, and interference
against a rush; those and stick fouls, cross-checking, and roughing in the
defending zone; delay of game (over the glass) by a team with the puck in its own
zone. Some high-sticking is a double minor, and a dangerous foul is sometimes a
major, which always carries a game misconduct (an ejection) unless it is for
fighting. After a hit, the hitter may be penalized (boarding, charging,
elbowing, roughing, interference), the two players may take coincidental
roughing minors in a scrum (sometimes with ten-minute misconducts), or the
toughest skater of the hit team may fight the hitter, more likely between tough
players and in a match three or more goals apart; both fighters take five-minute
majors. Neither fights nor scrums happen in overtime. A foul from behind on a
rush is sometimes awarded a penalty shot instead: the fouled team's shooter
alone against the goalie, decided like a shootout attempt.

A foul by the team with the puck stops play at once. A foul by the other team is
a delayed penalty: the team with the puck pulls its goalie for its first
available extra attacker and plays six skaters until the offenders touch the
puck or play stops, then the penalty is called with the faceoff in the
offenders' zone. A goal in that time wipes out a minor (and one minor of a double
minor); a delayed penalty still pending as a period ends is called then.

`PenaltyBox` keeps the timed penalties on the game clock, so they carry over
between periods. A team serves at most two penalties that leave it short at
once; a further one waits and starts, at its full length, when one ends. A
power-play goal ends the conceding team's running minor with the least time
left (only the current half of a double minor), never a major. Penalties on
both teams at one stoppage are coincidental and leave neither team short,
except one minor each at full strength with no other penalties, which plays
four-on-four. Misconducts never leave a team short, and start once the player's
other penalties are over. In regulation and playoff overtime a team has five
skaters less its penalties; in regular-season overtime it has three plus one for
each penalty the opponent serves beyond its own, so a penalty there makes
four-on-three, and a team never has fewer than three. Each strength state
(5-on-4, 5-on-3, 4-on-3 and their penalty-kill sides, 4-on-4, 3-on-3) plays the
lineup's units for it, the first power-play unit 60% of the time when rested and
the 4-on-5 units 40/35/25%. A skater in the box or ejected cannot go on; the
most rested available skater of the same kind (forward or defence) takes the
place in their group. Each extra skater on the ice adds to the attackers' edge,
so power plays shoot more from better ice, and a team killing a penalty may ice
the puck. Penalties are not called on a team with fewer than eight available
skaters, so a substitute always exists. Goalie and bench penalties and
instigator rules are not modelled (see
[future features](../future-features.md#goalie-bench-and-instigator-penalties)),
nor are suspensions ([off-ice events](../future-features.md#off-ice-events)).

### Pulling the goalie

Late in the third period a team trailing by one goal pulls its goalie with two
minutes left, and a team trailing by two with three and a half, close to recent
NHL averages; the timing is a tuning value until
[tactics](../future-features.md#tactics-and-formations) let the user choose it. It
pulls once it has the puck outside its own zone, or for a faceoff in the
attacking zone. The goalie comes back for a faceoff in the team's own zone and
after any goal, and goes out again while the rule still applies, so a team that
scores to trail by one, or concedes to trail by two, pulls again. A team that is
tied, ahead, or three or more behind never pulls, and the goalie returns when
regulation ends.

The extra attacker joins whatever strength state the penalties allow, exactly as
during a delayed penalty: the team keeps rotating the lines, pairs, or units for
that state and adds the first of the lineup's two extra attackers who is
available and not already on the ice. A five-on-four power play therefore
becomes six-on-four, and a team killing a penalty plays five-on-five. Manpower,
and so a goal's situation, still comes from the penalties being served
([ADR 0007](../adr/0007-manpower-from-penalties-served.md)): a goal by six skaters
against five is at even strength, and an empty-net goal against a team that
pulled its goalie while shorthanded is a power-play goal. The extra skater adds
to the attackers' edge like any other.

Every shot attempt at an empty net that reaches it scores. The leading team also
shoots for the empty net from its own zone or the neutral zone, reaching it less
often; a miss from its own zone is icing unless it is killing a penalty. A foul
from behind on a rush at an empty net is an ordinary penalty rather than a
penalty shot; awarded goals are not modelled. Between evenly matched teams about
one pull in seven ties the match and four in ten concede an empty-net goal.

### Play-by-play and match statistics

The `MatchResult` carries the play-by-play: faceoffs, shot attempts (saved,
missed, or blocked), goals, hits, takeaways, giveaways, and penalties, each
with its period, time, strength state, and the players on the ice for both
sides; a goalie pulled for an extra attacker is shown as an empty net. A
faceoff records where it was taken: at centre ice or in a team's defensive zone. A
penalty records the team, skater, infraction, and kind (minor, double minor,
major, misconduct, game misconduct, or penalty shot) at the whistle; a fight is
a fighting major to each fighter. A goal records its situation: even strength,
power play, or shorthanded, decided by the penalties being served rather than by
who is on the ice, so an extra attacker during a delayed penalty does not make
a power play; or a penalty shot, which counts as neither, ends no penalty, and
is unassisted. Only
Simulation holds the play-by-play; Management keeps the box score in the
completed match and saves, and Desktop shows the box score. Every individual
statistic is derived from the play-by-play, except time on ice, which comes from
the shifts, and power-play opportunities, which come from the penalties being
served, so they reconcile by construction. Skaters record goals, assists,
plus/minus (goals for less goals against while on the ice, excluding power-play
and penalty-shot goals), time on ice, shots on goal, shot attempts, hits,
blocked shots, faceoffs won and lost, takeaways, giveaways, individual xG,
penalty minutes, power-play and shorthanded goals and assists, and empty-net
goals (scored while the opponent's goalie was pulled). A team
records its power-play opportunities: each opponent penalty counts once, the
first time the team has more skaters while it is served, so a 5-on-3 is two and
coincidental penalties are none. The starting goalie plays the whole match,
apart from any time pulled for an extra attacker, and records shots and goals
against, xG against, and time on ice. As in NHL scoring, an empty-net goal is a
shot and a goal for the scoring team but neither a shot nor a goal against the
goalie. Shootout attempts count toward no player.

Every skater also records on-ice shot totals: both teams' shot attempts,
unblocked attempts, shots on goal, goals, and xG while they were on the ice,
and each team records the same for the whole match. These are kept separately
for each strength situation from the team's own side: five-on-five (both
goalies in net), power play, penalty kill, and other (four-on-four,
three-on-three, or equal manpower with a goalie pulled). As with goal
situations, the manpower comes from the penalties being served, so an extra
attacker is not counted: the builder takes each team's skaters on the ice less
one for a pulled goalie. Penalty shots are left out, as they are from
plus/minus, so the team's totals are its attempts less its penalty shots. Corsi
(all attempts), Fenwick (unblocked attempts), and the other shares are derived
from these totals; see [season](season.md).

Domain's `CompletedMatch` holds every one of these statistics and rejects a
match that does not reconcile: a team's shots must equal its skaters' shots on
goal, each goalie's shots, goals, and xG against must match the opponent's
skaters less their empty-net goals, empty-net goals are counted among a
skater's goals, one team's faceoff wins must be the other's losses, a team cannot block
more attempts than the opponent failed to get on goal, no skater's plus/minus
can exceed the goals scored, power-play and shorthanded goals and assists are
counted among a skater's goals and assists (at most two assists per such goal),
a team scores on the power play only with a power-play opportunity, and it
scores shorthanded only if the opponent had one. Each team's shot totals must be
the other's seen from the opposite side (for and against swapped, power play and
penalty kill exchanged), cannot exceed its skaters' attempts, shots, goals, and
xG, and bound every one of its skaters' on-ice totals. Time on ice is kept in
whole seconds and xG totals are compared within a rounding tolerance. Management
also keeps the goals and penalties from the play-by-play as the completed match's
scoring and penalty summaries; see [season](season.md).

### Expected goals

Every unblocked shot attempt at a goalie has an expected-goal (xG) value: the
chance that a league-average (reference-rated) shooter scores on a
league-average goalie from the same context. Blocked attempts have no xG, as in
public NHL models built on unblocked (Fenwick) attempts, and neither do attempts
at an empty net, which those models also leave out; a goalie's xG against is
therefore every attempt they faced. The context is the attempt's danger level, where
it was taken from, and whether it was a rebound or on the rush:

| Context | xG |
| --- | ---: |
| Low danger (point, perimeter) | 0.016 |
| Medium danger (faceoff circles) | 0.045 |
| High danger (slot, crease) | 0.118 |
| Rebound (always high danger) | odds × 2.0 (0.211) |
| Rush | odds × 1.3 (low 0.021, medium 0.058, high 0.148) |
| Penalty shot | 0.320, a reference shootout attempt; always on goal |

The engine splits an attempt's xG into reaching the net and beating the goalie.
A reference shooter reaches the net with a fixed chance for the context (62%
low, 68% medium, 72% high, 78% rebound), and a shot on goal scores with the xG
divided by that chance, so the two together give exactly the xG. The shooter's
accuracy then shifts the first chance, and the shooter's finishing against the
goalie's saving shifts the second, each in log-odds from the reference rating.
Skilled shooters and weak goalies therefore score above their xG and the reverse
below it, which is what goals saved above expected will measure (#54). Tired
skaters play below their ratings, so league-wide goals other than empty-net goals
run a few percent under xG. How often each context arises depends on the play:
better attackers get to the slot more often and better defenders keep them to
the outside.

Simulating a 1,344-match season takes about 1.2 to 1.7 seconds of engine time
(about 1 ms a match, 267 events each), measured on an Apple-silicon Mac in a
Release build, so a 16-match league day plays in well under a second. The
Management full-season and calibration tests, which also build a snapshot after
each day, each take about three seconds.

### Calibration

The tuning values are calibrated so a generated league's regular season
approximates the averages of the 2023-24, 2024-25, and 2025-26 NHL regular
seasons. Management's `CalibrationTests` play one generated season (seed 2026)
and check each target within a wide tolerance (in `NhlTargets.cs`), so a test
fails on broken tuning rather than on small rebalancing. The measured column is
that season; other seeds vary by about 0.1 goals and two points of regulation
share. Values are per team per game unless marked per match; standings points
are scaled from 82 games to 84.

| Target | NHL | Measured | Source |
| --- | ---: | ---: | --- |
| Goals (with shootout winners) | 3.06 | 3.03 | Hockey-Reference |
| Shots on goal | 28.8 | 29.4 | Hockey-Reference |
| Shot attempts | 59.5 | 59.7 | NHL.com team real-time |
| Expected goals | 3.12 | 3.10 | MoneyPuck, all situations |
| Save percentage | .900 | .905 | Hockey-Reference |
| Regulation / overtime / shootout share of matches | 78.0 / 15.0 / 7.1% | 81.5 / 10.8 / 7.7% | Hockey-Reference games |
| Power-play opportunities | 2.87 | 2.91 | Hockey-Reference |
| Power-play percentage | 21.2% | 19.9% | Hockey-Reference |
| Penalty minutes | 8.84 | 8.61 | NHL.com team penalties |
| Fights per match | about 0.20 | 0.21 | NHL.com majors, most for fighting |
| Hits | 21.5 | 21.7 | NHL.com team real-time |
| Blocked shots | 15.1 | 15.4 | NHL.com team real-time |
| Takeaways | 4.7 | 4.8 | NHL.com, 2024-25 and 2025-26 |
| Giveaways | 14.8 | 14.9 | NHL.com, 2024-25 and 2025-26 |
| Faceoffs per match | 56.4 | 57.3 | NHL.com team faceoffs |
| Centre-ice share of faceoffs | 30% | 29% | NHL.com neutral-zone faceoffs |
| Empty-net goals per match | 0.375 | 0.411 | NHL.com team real-time |
| Forward lines' share of forward ice time | 31/27/23/19% | 32/28/22/19% | NHL.com skater time on ice |
| Defence pairs' share of defence ice time | 39/34/28% | 37/34/29% | NHL.com skater time on ice |
| Standings points: spread, fewest, most | 15.4, 54, 120 | 13.5, 71, 122 | Hockey-Reference standings |

Sources: [Hockey-Reference league averages](https://www.hockey-reference.com/leagues/stats.html),
season pages and game results; the NHL.com statistics API (`api.nhle.com/stats/rest/en/team/`
`realtime`, `summary`, `faceoffpercentages`, and `penalties`, and `skater/summary` and
`skater/penalties`); and [MoneyPuck](https://moneypuck.com/data.htm) team data. Notes on the
targets:

- NHL.com changed how it tracks takeaways and giveaways after 2023-24 (about 7.0
  and 7.3 that season), so those targets use the two later seasons.
- Fights are estimated from major penalties, which are mostly fighting majors;
  NHL.com does not report fights separately.
- MoneyPuck's xG includes empty-net attempts, which this engine leaves without xG.
- Line and pair shares rank each team's regulars by time on ice per game and
  group forwards in threes and defence in twos, which only approximates real
  lines.

Known gaps: too few matches reach overtime and too few overtime matches are
decided before a shootout, so regulation share runs a few points high and
overtime share about four points low. The engine has no score effects (a
trailing team pressing, a leading one sitting back), which narrow margins in real
matches. The top defence pair plays a little less than the NHL's, because
fatigue limits how much more a pair can play than its target.

To print every measurement beside its target, run the calibration tests with
live output from the Release build:

```sh
tests/HockeySim.Management.Tests/bin/Release/net10.0/HockeySim.Management.Tests -class "HockeySim.Management.Tests.CalibrationTests" -showLiveOutput
```
