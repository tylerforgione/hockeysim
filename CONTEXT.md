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
