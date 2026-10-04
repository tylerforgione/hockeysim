using System.Collections.ObjectModel;

using HockeySim.Domain;

namespace HockeySim.Simulation;

/// <summary>
/// One team's side of a match result, including the individual statistics of every player who
/// appeared. Individual statistics reconcile with the team totals: skater goals sum to the
/// team's goal events, and the goalie's shots and goals against match the opponent's totals.
/// </summary>
public sealed class MatchTeamResult
{
    private readonly ReadOnlyCollection<SkaterMatchStatistics> _skaters;

    internal MatchTeamResult(
        TeamId teamId,
        int score,
        int shots,
        IEnumerable<SkaterMatchStatistics> skaters,
        GoalieMatchStatistics goalie)
    {
        TeamId = teamId;
        Score = score;
        Shots = shots;
        _skaters = skaters.ToList().AsReadOnly();
        Goalie = goalie;
    }

    public TeamId TeamId { get; }

    /// <summary>
    /// The decisive final score. A shootout winner is credited one goal that no player scored, so
    /// the score can exceed the team's <see cref="GoalEvent"/> count by one.
    /// </summary>
    public int Score { get; }

    /// <summary>Shots on goal in regulation and overtime; shootout attempts are excluded.</summary>
    public int Shots { get; }

    /// <summary>Every dressed skater, in lineup order: forward lines, then defence pairs.</summary>
    public IReadOnlyList<SkaterMatchStatistics> Skaters => _skaters;

    /// <summary>The starting goalie, who plays the entire match.</summary>
    public GoalieMatchStatistics Goalie { get; }
}