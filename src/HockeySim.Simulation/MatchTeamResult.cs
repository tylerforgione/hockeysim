using HockeySim.Domain;

namespace HockeySim.Simulation;

/// <summary>
/// One team's side of a match result.
/// </summary>
/// <param name="Score">
/// The decisive final score. A shootout winner is credited one goal that no player scored, so
/// the score can exceed the team's <see cref="GoalEvent"/> count by one.
/// </param>
/// <param name="Shots">Shots on goal in regulation and overtime; shootout attempts are excluded.</param>
/// <param name="GoalieId">The starting goalie, who plays the entire match.</param>
public sealed record MatchTeamResult(TeamId TeamId, int Score, int Shots, PlayerId GoalieId);