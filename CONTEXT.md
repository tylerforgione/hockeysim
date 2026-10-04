# Hockey management

Vocabulary for HockeySim's single-player hockey management game.

## Language

**Management game**:
The overall game in which the user manages a hockey team and matches are simulated.

**Player**:
A hockey athlete belonging to the simulated world.
_Avoid_: Using player to mean the human user in domain documentation.

**Team**:
A group of hockey players competing together, with a roster and lineup.

**Match**:
An individual hockey contest between two teams, distinct from the overall
management game.

**Managed team**:
The one team the user controls. Only its lineup can be changed by the user;
other teams are read-only.

**Lineup**:
A team's dressed players for a match: four forward lines (left wing, centre,
right wing), three defence pairs, a starting goalie, and a backup goalie.

**Match result**:
The outcome of a simulated match: a decisive score, how it was decided
(regulation, overtime, or shootout), shots, the goals players scored with their
assists, and each appearing player's match statistics.
A shootout winner is credited one deciding goal that no player scored.

**Appearance**:
Taking part in a match, which counts as one game played. Every dressed skater
and the starting goalie appear; the dressed backup goalie and scratches do not.

**Match statistics**:
An appearing player's individual production in one match. Skaters record goals,
assists, and points; the starting goalie records shots against, saves, and goals
against. They reconcile with the score and shots, excluding the shootout:
shootout attempts and the deciding goal count toward no player.

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

**Overtime**:
A five-minute sudden-death period played when regulation ends tied.

**Shootout**:
Alternating one-on-one attempts that decide a match still tied after overtime.

**Scratch**:
A rostered player who is not dressed in the current lineup.

**Inbox message**:
A message delivered to the user from someone in the game world, such as the
owner, staff, or a player. Messages describe real game state and can be marked
read.
