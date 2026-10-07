# Hockey management

Vocabulary for HockeySim's single-player hockey management game.

## Language

**Management game**:
The overall game in which the user manages a hockey team and matches are simulated.

**Player**:
A hockey athlete belonging to the simulated world.
_Avoid_: Using player to mean the human user in domain documentation.

**Biography**:
A player's identifying details: birth date, birthplace (city, the state or
province in Canada and the United States, and country), nationality,
handedness, height, and weight.

**Age**:
A player's completed years on a given date, derived from the birth date rather
than stored, so players age as the season passes. A player born on 29 February
turns a year older on 28 February in other years. Every player is 16 to 60 on a
season's opening day.

**Nationality**:
The country a player represents. Usually, but not always, the birth country.

**Handedness**:
The hand a skater shoots or a goalie catches with: left or right.

**Team**:
A group of hockey players competing together, with a roster and lineup.

**Match**:
An individual hockey contest between two teams, distinct from the overall
management game.

**Managed team**:
The one team the user controls. Only its lineup can be changed by the user;
other teams are read-only.

**Rating**:
A 0-100 measure of one player ability, such as skating, faceoffs, discipline
(how rarely the player takes penalties), stamina (how slowly they tire),
durability (resistance to injury), or toughness (physical play and fighting).
Every player has a value for every rating, including those their position does
not use; those are low and not shown.

**Overall rating**:
A player's ratings summarised as one 0-100 value, weighted for their position.
It never includes durability.

**Hidden information**:
Game state the user cannot see, such as a player's durability. It is kept out
of everything Desktop displays and out of anything shown that could reveal it.

**Lineup**:
A team's dressed players for a match: four forward lines (left wing, centre,
right wing), three defence pairs, a starting goalie, and a backup goalie. It
also names who plays in each special situation: its special-situation units and
two extra attackers.

**Special situation**:
Any manpower situation other than five-on-five, named from the team's own side:
a power play (5-on-4, 5-on-3, 4-on-3), a penalty kill (4-on-5, 3-on-5, 3-on-4),
four-on-four, or three-on-three (regular-season overtime).

**Special-situation unit**:
The skaters a lineup sends out together in one special situation. A lineup holds
two units for each power play, three 4-on-5 and two of each other penalty-kill
units, two 4-on-4 units, and three 3-on-3 units. Each slot has a skater role
(centre, wing, or defence) saying where that skater plays; the one centre takes
faceoffs and defence players man the points. Any dressed skater can fill any
slot, and a player may be in several units but only once in each.
_Avoid_: Line, when meaning a special-situation unit.

**Extra attacker**:
The skater sent on when the goalie is pulled. A lineup names two, a first and a
second choice, so one is available when the first is already on the ice. Any
dressed skater can be either.

**Match result**:
The outcome of a simulated match: a decisive score, how it was decided
(regulation, overtime, or shootout), shots, the play-by-play, any shootout, and
each appearing player's match statistics.
A shootout winner is credited one deciding goal that no player scored.

**Play-by-play**:
The timed events of a match in order: faceoffs, shot attempts, goals, hits,
takeaways, and giveaways, each with its period, time, strength state, and the
players on the ice for both teams. Only the match result holds it; a completed
match keeps the box score.

**Strength state**:
How many skaters each team has on the ice, such as five-on-five or
three-on-three. Goalies are not counted.

**Shift**:
A stretch a skater spends on the ice before changing. Forward lines and defence
pairs change separately, on the fly or at a stoppage; tired skaters play below
their ratings, and stamina sets how fast they tire and recover.

**Shot attempt**:
Any shot toward the net: on goal (saved or scored), missed, or blocked by a
defending skater. A shot on goal is one the goalie must stop.
_Avoid_: Shot, when meaning any attempt; a shot is on goal.

**Expected goals (xG)**:
The chance that an unblocked shot attempt scores for a league-average shooter
against a league-average goalie, from its danger level and whether it was a
rebound or on the rush. Blocked attempts have none. A player's or team's xG is
the sum over their unblocked attempts.

**Shot danger**:
How dangerous a shot attempt's location is: low from the point and perimeter,
medium from the faceoff circles, and high from the slot and crease. A rebound is
always high danger.

**Appearance**:
Taking part in a match, which counts as one game played. Every dressed skater
and the starting goalie appear; the dressed backup goalie and scratches do not.

**Match statistics**:
An appearing player's individual production in one match. Skaters record goals,
assists, points, plus/minus, time on ice, shots on goal, shot attempts, hits,
blocked shots, faceoffs won and lost, takeaways, giveaways, and xG; the starting
goalie records shots against, saves, goals against, xG against, and time on ice.
They are derived from the play-by-play and reconcile with the score and shots,
excluding the shootout: shootout attempts and the deciding goal count toward no
player.

**Plus/minus**:
For a skater, the goals their team scored while they were on the ice less the
goals it conceded, not counting power-play goals.

**Time on ice**:
How long a player was on the ice in regulation and overtime, in whole seconds.
The starting goalie's is the whole match.

**Schedule**:
The ordered regular-season calendar of matches. Every team plays 84: four
against each divisional opponent, three against each other same-conference
opponent, and two against each opposite-conference opponent, with 42 at home and
42 away. A team plays at most once on any date.

**League day**:
One calendar date of the season. Advancing a league day plays every match
scheduled on the current date, which may be none, and moves to the next date.
A day's results are applied together or not at all.

**Completed match**:
The canonical record of a scheduled match after it is played: the scheduled
date and teams, how it was decided, each side's score and shots, and the box
score of every appearing player. A scheduled match is completed at most once.

**Box score**:
An appearing player's match statistics as recorded in a completed match.

**Team record**:
A team's current-season wins and losses, each kept by how the match was decided
(regulation, overtime, or shootout), with goals for and against. Team goals
include a shootout deciding goal.

**Standings points**:
Two for any win, one for an overtime or shootout loss, and none for a regulation
loss.

**Standings**:
A ranked table of teams (the league, a conference, or a division) from the
season's results so far. Teams are ordered by standings points, then the NHL
tie-breakers: fewer games played, regulation wins, regulation and overtime wins,
wins, head-to-head points among the tied clubs, goal differential, and goals
for. Teams level on all of them share a rank.

**Head-to-head**:
The standings points tied clubs earned in games among themselves. Where two
clubs have met an odd number of times, the first game in the city that hosted
the extra meeting (the odd game) is not counted. For more than two clubs, the
share of available points is compared.

**Season statistics**:
A player's current-season totals, accumulated from their box scores. Like match
statistics, they exclude the shootout.

**Completed season**:
The state after the final scheduled match is played. It can still be browsed
and managed, but no further league days can be played; playoffs and the next
season are not modelled.

**Saved game**:
A stored copy of a whole game, from which play resumes exactly as it would have
continued. Loading a saved game replaces the current game only when the whole
save is valid; team records, season statistics, and standings are rebuilt from
its completed matches.

**Save name**:
The name a saved game is stored under, chosen by the user. Names that differ
only in letter case are the same save, so saving under an existing name
replaces that save.

**Unsaved progress**:
Changes to the game in progress since it was last saved or loaded, or the whole
game if it has never been saved. Loading, starting a new game, or exiting
discards it, so the user is asked first.

**Overtime**:
Sudden-death play when regulation ends tied. In the regular season it is one
five-minute three-on-three period with the three-on-three units, then a
shootout; in the playoffs it is as many twenty-minute five-on-five periods as it
takes, with no shootout.

**Shootout**:
Alternating one-on-one attempts that decide a regular-season match still tied
after overtime.

**Scratch**:
A rostered player who is not dressed in the current lineup.

**Inbox message**:
A message delivered to the user from someone in the game world, such as the
owner, staff, or a player. Messages describe real game state and can be marked
read.
