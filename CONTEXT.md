# Hockey management

Vocabulary for HockeySim's single-player hockey management game.

## Language

**Management game**:
The overall game in which the user manages a hockey team and matches are simulated.

**Player**:
A hockey athlete belonging to the simulated world.
_Avoid_: Using player to mean the human user in domain documentation.

**Position**:
A player's natural position: centre, wing, defence, or goalie. Any skater can
play any skater lineup role, but plays a little below their ratings out of
position; goalies only play in goal.

**Out of position**:
A skater filling a lineup role other than their natural position: a centre on
the wing or a winger at centre (a moderate penalty), or a forward on defence or
a defenceman at forward (a larger one). No skater is out of position as an extra
attacker.

**Off-hand side**:
The side of the ice opposite a skater's handedness, such as a left shot at right
wing or right defence. Their backhand faces the boards, which costs a little in
board play, breakouts, and pinches, though an off-hand winger gets one-timers
on the forehand. Only wing and defence slots with a side have one: a centre, a
lone wing or defence player in a unit, and the extra attacker do not.

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

**AI team**:
Any team other than the managed team. The game sets its lineup, and it replaces
injured players itself (see match-day lineup).

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
Game state the user cannot see, such as a player's durability and wear. It is kept out
of everything Desktop displays and out of anything shown that could reveal it.

**Lineup**:
A team's dressed players for a match: four forward lines (left wing, centre,
right wing), three defence pairs, a starting goalie, and a backup goalie. Any
skater can fill any line or pair slot, whatever their position. It also names
who plays in each special situation: its special-situation units and two extra
attackers. A player who cannot play through an injury is never dressed for a
match; a player playing through one may be.

**Match-day lineup**:
The lineup a team dresses on a given day. The managed team's is the lineup the
user set, which must leave out players who cannot play before the team plays. An
AI team's is its preferred lineup with each player who cannot play replaced by
the healthy scratch who best suits their place, so the preferred lineup returns
as players heal.

**Special situation**:
Any manpower situation other than five-on-five, named from the team's own side:
a power play (5-on-4, 5-on-3, 4-on-3), a penalty kill (4-on-5, 3-on-5, 3-on-4),
four-on-four, or three-on-three (regular-season overtime).

**Special-situation unit**:
The skaters a lineup sends out together in one special situation. A lineup holds
two units for each power play, three 4-on-5 and two of each other penalty-kill
units, two 4-on-4 units, and three 3-on-3 units. Each slot has a skater role
(centre, wing, or defence) saying where that skater plays; the one centre takes
faceoffs and defence players man the points. Where a unit has two wings or two
defence players, the first plays the left side and the second the right. Any
dressed skater can fill any slot, and a player may be in several units but only
once in each.
_Avoid_: Line, when meaning a special-situation unit.

**Extra attacker**:
The skater sent on when the goalie is pulled, during a delayed penalty or late
in a match the team is losing. A lineup names two, a first and a second choice,
so one is available when the first is already on the ice. Any dressed skater can
be either. The extra attacker adds to whatever strength the penalties allow, so
a power play can be six-on-four.

**Pulling the goalie**:
Replacing the goalie with an extra attacker. Besides delayed penalties, a team
trailing by one goal with two minutes left in the third period, or by two with
three and a half, pulls its goalie to try to tie the match. The goalie returns
for a faceoff in the team's own zone and after any goal, and is pulled again
while the team still trails late.

**Empty-net goal**:
A goal scored while the conceding team's goalie was pulled. It counts as a shot
and a goal for the scorer and the team, but not against the goalie, and has no
expected-goal value.

**Match result**:
The outcome of a simulated match: a decisive score, how it was decided
(regulation, overtime, or shootout), shots, the play-by-play, any shootout,
each appearing player's match statistics, and the hidden wear players took.
A shootout winner is credited one deciding goal that no player scored.

**Play-by-play**:
The timed events of a match in order: faceoffs, shot attempts, goals, hits,
takeaways, giveaways, penalties, and injuries, each with its period, time, strength state,
and the players on the ice for both teams. Only the match result holds it; a completed
match keeps the box score and the scoring and penalty summaries.

**Strength state**:
How many skaters each team has on the ice, such as five-on-five or
three-on-three. Goalies are not counted, but an extra attacker for a pulled
goalie is.

**Strength situation**:
How shot totals are split, from one team's side: five-on-five (both goalies in
net), power play, penalty kill, or other (four-on-four, three-on-three, or equal
manpower with a goalie pulled). Like a goal's situation, it follows the
penalties being served, so an extra attacker never makes a power play.
_Avoid_: Strength state, which counts the skaters on the ice including an extra
attacker.

**Penalty**:
A sanction on a skater for an infraction, such as hooking or fighting: a minor
(two minutes), double minor (four), major (five), misconduct (ten, without
leaving the team short), game misconduct (ejection, recorded as ten minutes),
or a penalty shot. A team serves at most two penalties that leave it short at
once; a further one waits until one ends.
_Avoid_: Calling a penalty shot or misconduct a power play.

**Penalty minutes**:
The minutes of every penalty assessed to a skater; a penalty shot carries none.

**Power play**:
Play in which a team has more skaters than the opponent because the opponent is
serving penalties. The other side is on the penalty kill, or shorthanded. A
power-play goal ends the shorthanded team's minor with the least time left, but
never a major. An extra attacker during a delayed penalty is not a power play.

**Power-play opportunity**:
An opponent penalty that gives a team the manpower advantage, counted once the
first time it does; coincidental penalties give none.

**Power-play goal**, **shorthanded goal**:
A goal scored on the power play, or while shorthanded. Assists on them are
power-play and shorthanded assists. Every other goal is at even strength or on
a penalty shot.

**Coincidental penalties**:
Penalties to both teams at the same stoppage, which leave neither team short,
except one minor each at full strength, which plays four-on-four.

**Delayed penalty**:
A foul by the team without the puck. Play continues, with the other team's
goalie pulled for an extra attacker, until the offenders touch the puck; a goal
in that time wipes out a minor.

**Penalty shot**:
A shooter alone against the goalie, awarded instead of a minor for a foul from
behind on a scoring chance. Its goal is unassisted, is neither a power-play nor
a shorthanded goal, and does not count toward plus/minus.

**Fight**:
Two skaters, one from each team, fighting; each takes a five-minute fighting
major, which leaves neither team short.

**Shift**:
A stretch a skater spends on the ice before changing. Forward lines and defence
pairs change separately, on the fly or at a stoppage; tired skaters play below
their ratings, and stamina sets how fast they tire and recover.

**Injury**:
Harm to one body part, such as a concussion or a sprained ankle, suffered in a
match from a hit (taken or given), a blocked shot, a fight, or a non-contact
strain. Each kind has a range of recovery times; how long a particular injury
takes is its severity. An injured player either cannot play until it heals or can
play through it at reduced ratings. Injuries happen only in matches that count:
never in the preseason, and not away from the rink.

**Recovery time**:
The league days an injury takes to heal, counted from the date of its match
whether or not the team plays: hurt on the 1st with seven days' recovery, a
player is healthy again on the 8th. It is hidden; the user sees the expected
return.

**Expected return**:
The staff's estimate of when an injured player will be healthy: a range of dates
that always holds the actual return date, about a quarter of the recovery time
either side of it.

**Playing through an injury**:
Playing while an injury heals, with a few points off the ratings it affects. A
player who cannot play through an injury is out of the match in which it
happens and of every match before it heals, and must not be dressed until then.

**Wear**:
Hidden damage a player's body part accumulates from impacts and injuries. It
never recovers and makes that part likelier to be injured again.

**Injury cap**:
The rule that every team keeps at least 18 skaters and 2 goalies able to play.
An injury that would break it does not happen, so with two goalies on a roster,
goalies only suffer injuries they can play through.

**Shot attempt**:
Any shot toward the net: on goal (saved or scored), missed, or blocked by a
defending skater. A shot on goal is one the goalie must stop.
_Avoid_: Shot, when meaning any attempt; a shot is on goal.

**Expected goals (xG)**:
The chance that an unblocked shot attempt scores for a league-average shooter
against a league-average goalie, from its danger level and whether it was a
rebound or on the rush. Blocked attempts and attempts at an empty net have none.
A player's or team's xG is the sum over their unblocked attempts.

**On-ice shot totals**:
Both teams' shot attempts, unblocked attempts, shots on goal, goals, and xG
while a skater was on the ice, kept by strength situation. A team keeps the same
totals for the whole match. Penalty shots are not counted.

**Corsi**, **Fenwick**:
Corsi counts shot attempts and Fenwick unblocked attempts, for (CF, FF) and
against (CA, FA), on the ice or for a team. Their percentages (CF%, FF%) are the
share for, as are the shot, goal, and xG percentages (SF%, GF%, xGF%). A share
is undefined until either side has a count.

**Shot danger**:
How dangerous a shot attempt's location is: low from the point and perimeter,
medium from the faceoff circles, and high from the slot and crease. A rebound is
always high danger.

**Appearance**:
Taking part in a match, which counts as one game played. Every dressed skater
able to play and the starting goalie appear; a dressed skater who cannot play
through an injury, the dressed backup goalie, and scratches do not. When the
starting goalie cannot play, the backup starts instead.

**Match statistics**:
An appearing player's individual production in one match. Skaters record goals,
assists, points, plus/minus, time on ice, shots on goal, shot attempts, hits,
blocked shots, faceoffs won and lost, takeaways, giveaways, xG, penalty minutes,
power-play and shorthanded goals and assists, and empty-net goals; the starting
goalie records shots against, saves, goals against (neither counting empty-net
goals), xG against, and time on ice. A team also records its power-play
opportunities.
They are derived from the play-by-play and reconcile with the score and shots,
excluding the shootout: shootout attempts and the deciding goal count toward no
player.

**Goals saved above expected (GSAx)**:
A goalie's xG against less goals against: positive when the goalie stopped more
than a league-average goalie would have.

**Goals-against average (GAA)**:
A goalie's goals against per sixty minutes in net. Undefined before any time in
net.

**Shutout**:
A start in which the goalie is charged with no goal. Empty-net goals and a
shootout are not charged to the goalie, so neither prevents one.

**Plus/minus**:
For a skater, the goals their team scored while they were on the ice less the
goals it conceded, not counting power-play or penalty-shot goals.

**Time on ice**:
How long a player was on the ice in regulation and overtime, in whole seconds.
The starting goalie's is the whole match, less any time pulled for an extra
attacker.

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
date and teams, how it was decided, each side's score, shots, and shot totals,
the box score of every appearing player, the scoring and penalty summaries, and
the injuries and hidden wear the match caused.
A scheduled match is completed at most once.

**Scoring summary**:
Every goal scored by a player in a completed match, in order: period, time,
team, scorer, assists, situation, and whether the net was empty. A shootout's
deciding goal is not listed.

**Penalty summary**:
Every penalty in a completed match, in order: period, time, team, player,
infraction, and kind.

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
A player's current-season totals, accumulated from every statistic in their box
scores, and the rates derived from them, such as faceoff percentage, time on ice
per game, save percentage, and GAA. A team's season statistics are its power
play, penalty kill, faceoffs, and shot totals. Like match statistics, they
exclude the shootout.

**Power-play percentage**, **penalty-kill percentage**:
Power-play goals per power-play opportunity; and the share of the times a team
was shorthanded (the opponents' opportunities) in which it did not concede a
power-play goal.

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
read. The head trainer reports each injury to a managed-team player, with its
expected return, and each recovery.
