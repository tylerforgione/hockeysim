using System.Collections.ObjectModel;

using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Simulation;

/// <summary>
/// The outcome of a simulated match. Results describe what happened; applying them to the game
/// world is Management's responsibility.
/// </summary>
public sealed class MatchResult
{
    public const int RegulationPeriodCount = 3;
    public const int OvertimePeriod = RegulationPeriodCount + 1;

    private readonly ReadOnlyCollection<GoalEvent> _goals;

    internal MatchResult(
        MatchTeamResult home,
        MatchTeamResult away,
        MatchDecision decision,
        IEnumerable<GoalEvent> goals,
        ShootoutResult? shootout,
        RandomState randomState)
    {
        Home = home;
        Away = away;
        Decision = decision;
        _goals = goals.ToList().AsReadOnly();
        Shootout = shootout;
        RandomState = randomState;
    }

    public MatchTeamResult Home { get; }

    public MatchTeamResult Away { get; }

    public MatchDecision Decision { get; }

    /// <summary>
    /// Player-attributable goals in chronological order. Excludes the shootout deciding goal.
    /// </summary>
    public IReadOnlyList<GoalEvent> Goals => _goals;

    /// <summary>
    /// The deciding shootout, present only when <see cref="Decision"/> is
    /// <see cref="MatchDecision.Shootout"/>.
    /// </summary>
    public ShootoutResult? Shootout { get; }

    public TeamId WinnerId => Home.Score > Away.Score ? Home.TeamId : Away.TeamId;

    /// <summary>
    /// The random stream position after the match. Preserve it to continue the stream.
    /// </summary>
    public RandomState RandomState { get; }
}