using System.Collections.ObjectModel;

using HockeySim.Domain;

namespace HockeySim.Simulation;

/// <summary>
/// One team's side of a match result, including the individual statistics of every player who
/// appeared. Individual statistics reconcile with the team totals: skater goals sum to the
/// team's goal events, and the goalie's shots and goals against match the opponent's totals less
/// its empty-net goals.
/// </summary>
public sealed class MatchTeamResult
{
    private readonly ReadOnlyCollection<SkaterMatchStatistics> _skaters;

    internal MatchTeamResult(
        TeamId teamId,
        int score,
        int shots,
        int powerPlayOpportunities,
        IEnumerable<SkaterMatchStatistics> skaters,
        GoalieMatchStatistics goalie)
    {
        TeamId = teamId;
        Score = score;
        Shots = shots;
        PowerPlayOpportunities = powerPlayOpportunities;
        _skaters = skaters.ToList().AsReadOnly();
        Goalie = goalie;
    }

    public TeamId TeamId { get; }

    /// <summary>
    /// The decisive final score. A shootout winner is credited one goal that no player scored, so
    /// the score can exceed the team's <see cref="Events.GoalEvent"/> count by one.
    /// </summary>
    public int Score { get; }

    /// <summary>Shots on goal in regulation and overtime; shootout attempts are excluded.</summary>
    public int Shots { get; }

    /// <summary>
    /// Opponent penalties that gave this team a manpower advantage: each one counts once, the first
    /// time the team has more skaters available while it is being served.
    /// </summary>
    public int PowerPlayOpportunities { get; }

    /// <summary>Goals scored on the power play.</summary>
    public int PowerPlayGoals => _skaters.Sum(skater => skater.PowerPlayGoals);

    /// <summary>Every dressed skater, in lineup order: forward lines, then defence pairs.</summary>
    public IReadOnlyList<SkaterMatchStatistics> Skaters => _skaters;

    /// <summary>The starting goalie, who plays the entire match.</summary>
    public GoalieMatchStatistics Goalie { get; }
}