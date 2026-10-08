# Players, lineups, and the new game

Generating a new game's world, the players in it (ratings and biographies),
team lineups with their special-situation units, and the inbox. Spans
`HockeySim.Domain` (the rules) and `HockeySim.Management` (generation and
commands).

## Code map

| File | Holds |
| --- | --- |
| `Domain/League.cs`, `Conference.cs`, `Division.cs`, `Team.cs`, `TeamId.cs` | The league structure |
| `Domain/Player.cs`, `PlayerId.cs`, `Position.cs`, `Rating.cs`, `RatingScore.cs` | Players and their ratings |
| `Domain/OverallRating.cs` | Overall-rating weights (keep in step with the table below) |
| `Domain/PlayerBiography.cs`, `Birthplace.cs`, `Country.cs`, `Handedness.cs`, `Height.cs`, `Weight.cs` | Biographies |
| `Domain/Lineup.cs`, `ForwardLine.cs`, `DefencePair.cs` | Lines, pairs, and dressed players |
| `Domain/SkaterFit.cs`, `PositionFit.cs`, `SkaterSide.cs` | Out-of-position and off-hand rules for any-role lineups |
| `Domain/SpecialSituation*.cs`, `SkaterRole.cs`, `DefaultSpecialSituationUnits.cs` | Special-situation units and their defaults |
| `Management/NewGame/LeagueGenerator.cs` | Builds the league |
| `Management/NewGame/PlayerRatingGenerator.cs`, `PlayerBiographyGenerator.cs`, `PlayerOriginData.cs`, `FictionalLeagueData.cs` | Generated players, names, and teams |
| `Management/Lineups/` | `SetLineupCommand` and its selections |
| `Management/Inbox/`, `Snapshots/InboxMessageSnapshot.cs` | Inbox messages and the new-game messages |
| `Management/GameManagement/GameManager.cs` | Commands: new game, select team, set lineup, read message |
| `Management/GameManagement/Snapshots/GameSnapshot.cs` | `PlayerSnapshot`, team, and lineup snapshots |

Domain paths are under `src/HockeySim.Domain/`, Management paths under
`src/HockeySim.Management/`.

## Tests

| File | Covers |
| --- | --- |
| `Domain.Tests/PlayerTests.cs`, `OverallRatingTests.cs`, `PlayerBiographyTests.cs` | Ratings, overall weights, ages and biography rules |
| `Domain.Tests/LineupTests.cs`, `SpecialSituationUnitTests.cs` | Lineup and unit invariants, defaults |
| `Domain.Tests/SkaterFitTests.cs` | Position fit, off-hand sides, unit slot sides |
| `Management.Tests/NewGameTests.cs` | Generated-world invariants, team selection, reproducibility |
| `Management.Tests/PlayerRatingGenerationTests.cs`, `PlayerBiographyGenerationTests.cs` | Generated distributions over several seeds |
| `Management.Tests/LineupTests.cs`, `SpecialSituationUnitTests.cs` | Lineup commands, rejected changes, snapshot isolation |
| `Management.Tests/InboxTests.cs` | Inbox messages |

## The new game and lineups

Domain, Simulation, Management, Infrastructure, and Desktop exist today. Domain
protects generated-world invariants through validated construction and
read-only collections. Management owns a headless new-game workflow,
controlled random state, managed-team selection, and read-only snapshots. Fictional names are kept separate from the
league and roster rules that use them.

Management also delivers inbox messages to the user. New-game messages are
derived from the generated managed team, so they never describe state the game
does not hold; selecting a different managed team replaces them with messages
for that team. Marking a message read is a Management command.
Domain's `Lineup` also holds the special-situation units and two extra
attackers. `SpecialSituationFormat` fixes each situation's unit count and the
skater role of every slot (5-on-4 and 5-on-3: LW C RW / LD RD; 4-on-3, 4-on-5,
and 4-on-4: C W / LD RD; 3-on-5 and 3-on-4: C / LD RD; 3-on-3: C W / D), and
every format has exactly one centre, who takes faceoffs. Where a format has two
wings or two defence players, its `Sides` put the first on the left and the
second on the right; the centre and a lone wing or defence player have no side.

Any skater can fill any skater slot: every place on a forward line or defence
pair, every unit slot, and either extra attacker. A player's `Position` is their
natural position. Goalies fill only the two goalie places, and only goalies fill
them. Roster rules are unchanged. Domain's `SkaterFit` judges each assignment:
`PositionFit` is natural, another forward position (centre and wing), or across
forwards and defence, and a skater is off-hand on the side opposite their
handedness. The match engine plays an ill-suited skater a little below their
ratings (see [Positions and handedness](match-engine.md#positions-and-handedness)),
and the Lines page warns when a skater is out of position. The lineup rejects a line, pair,
or unit that holds a goalie or the same player twice, a unit with a scratched
skater, and a lineup without exactly the required units. Generated teams
get line-derived defaults from `Lineup.CreateWithDefaultUnits`, which lives in
Domain because it is a deterministic function of the lines and every test
project's lineup helpers reuse it: power plays, four-on-four, and three-on-three
draw on the top lines and pairs, penalty kills on the second to fourth lines'
centres and left wings, and the first two centres are the extra attackers.
Management's `SetLineup` replaces the whole lineup, units included, so a lines
change that scratches a unit player is rejected unless its units change too.
Generated lineups play everyone at their natural position, and within each line
or pair a left and a right shot play on their forehand sides; a pair that shoots
the same way keeps one player off-hand.
The event engine plays every strength state that penalties create with the
matching units (see [Penalties and special teams](match-engine.md#penalties-and-special-teams))
and sends on an extra attacker whenever a goalie is pulled, during a delayed
penalty or late in a match (see [Pulling the goalie](match-engine.md#pulling-the-goalie)).

## Player ratings

Domain's `Player` holds a 0-100 value for every `Rating`: the skater skills,
the three goaltending ratings, and faceoffs, discipline, stamina, durability,
and toughness. The match engine uses faceoffs, stamina, and toughness; discipline
drives penalties, and durability waits for injuries. A new game
generates ratings by position in Management's `PlayerRatingGenerator`. Each
player draws one talent level that the position's skills follow, shifted by a
position profile with a little variation per rating. For example, centres take
faceoffs and defence block shots. Traits (discipline, stamina, durability,
toughness) vary independently of talent. Ratings outside the position, such as
a goalie's skating or a skater's reflexes, are drawn low. The event engine is
calibrated against leagues generated this way (see
[match engine calibration](match-engine.md#calibration)), so changing the
distributions means re-checking that calibration.

Domain's `OverallRating` derives a player's overall rating from their
ratings. It is a weighted mean for their position, rounded to the nearest whole
number (halves round up). The weights are whole percentages that total 100 for
each position. Unlisted ratings, including durability, carry no weight:

| Rating | Centre | Wing | Defence | Goalie |
| --- | ---: | ---: | ---: | ---: |
| Skating | 14 | 15 | 14 | |
| Shot power | 6 | 9 | 6 | |
| Shot accuracy | 10 | 13 | 3 | |
| Puck control | 11 | 11 | 6 | |
| Passing | 11 | 9 | 9 | |
| Offensive awareness | 13 | 14 | 6 | |
| Defensive awareness | 9 | 7 | 17 | |
| Checking | 3 | 4 | 9 | |
| Shot blocking | 2 | 2 | 10 | |
| Stick checking | 5 | 5 | 9 | |
| Faceoffs | 8 | | | |
| Discipline | 2 | 2 | 3 | |
| Stamina | 4 | 5 | 5 | 5 |
| Toughness | 2 | 4 | 3 | |
| Reflexes | | | | 40 |
| Positioning | | | | 35 |
| Rebound control | | | | 20 |

Durability is hidden information. Management's `PlayerSnapshot` carries the
overall rating and every other rating, but not durability. Because the overall
rating gives durability no weight, it cannot reveal it. Saves still carry every
rating, durability included. Desktop shows the overall rating as the OVR column
on both roster rating tables and in the player profile. The profile lists only
the ratings that matter to the position: the skater skills plus faceoffs,
stamina, toughness, and discipline for a skater, and the goaltending ratings,
stamina, and discipline for a goalie. Exact ratings stay visible for now; see
[scouting](../future-features.md#scouting-and-hidden-information).

## Player biographies

Domain's `PlayerBiography` holds each player's birth date, `Birthplace`,
nationality (`Country`), `Handedness`, `Height` (whole inches), and `Weight`
(whole pounds). Rosters list size in imperial units, so they are stored that
way; a metric display setting is a
[future feature](../future-features.md#display-settings). A birthplace names a
state or province exactly when its country is Canada or the United States.
Age is never stored: `Player.AgeOn(date)` derives it, and `Season` rejects a
league with any player younger than 16 or older than 60 on opening day.
`PlayerSnapshot.Age` is the age on the game's current date, so ages tick over on
birthdays as days are played.

Management's `PlayerBiographyGenerator` draws a nationality from
`PlayerOriginData`, weighted to approximate recent NHL rosters (about 41%
Canada, 27% United States, then Sweden, Finland, Russia, Czechia, and smaller
hockey nations), then a name from that nation's pools, so names always fit the
nationality. About 6% of players are born in another country. Ages on opening
day run from 18 to 40 and cluster in the mid-twenties. Most players shoot left
and nine in ten goalies catch left; defence and goalies are taller on average,
and weight follows height. Like ratings, these values are provisional. The
match engine uses height and weight in physical play, and handedness on the
wings and defence.

Desktop's roster tables add nationality (a three-letter code such as CAN or
SUI) and handedness columns, and the player profile shows height, weight,
whether the player shoots or catches, birth date, birthplace, and nationality.
