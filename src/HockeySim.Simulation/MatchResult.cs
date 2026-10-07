using System.Collections.ObjectModel;

using HockeySim.Domain;
using HockeySim.Simulation.Events;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Simulation;

/// <summary>
/// The outcome of a simulated match. Results describe what happened; applying them to the game
/// world is Management's responsibility.
/// </summary>
public sealed class MatchResult
{
    public const int RegulationPeriodCount = 3;

    /// <summary>The first overtime period. Playoff overtime continues with periods after it.</summary>
    public const int OvertimePeriod = RegulationPeriodCount + 1;

    private readonly ReadOnlyCollection<MatchEvent> _events;
    private readonly ReadOnlyCollection<GoalEvent> _goals;

    internal MatchResult(
        MatchTeamResult home,
        MatchTeamResult away,
        MatchDecision decision,
        IEnumerable<MatchEvent> events,
        TimeSpan playingTime,
        ShootoutResult? shootout,
        RandomState randomState)
    {
        Home = home;
        Away = away;
        Decision = decision;
        _events = events.ToList().AsReadOnly();
        _goals = _events.OfType<GoalEvent>().ToList().AsReadOnly();
        PlayingTime = playingTime;
        Shootout = shootout;
        RandomState = randomState;
    }

    public MatchTeamResult Home { get; }

    public MatchTeamResult Away { get; }

    public MatchDecision Decision { get; }

    /// <summary>
    /// The play-by-play of regulation and overtime in chronological order. Events at the same
    /// moment keep the order they happened in. The shootout is in <see cref="Shootout"/>.
    /// </summary>
    public IReadOnlyList<MatchEvent> Events => _events;

    /// <summary>
    /// Player-attributable goals in chronological order. Excludes the shootout deciding goal.
    /// </summary>
    public IReadOnlyList<GoalEvent> Goals => _goals;

    /// <summary>
    /// The game time played in regulation and overtime, up to an overtime winner. Each starting
    /// goalie's time on ice.
    /// </summary>
    public TimeSpan PlayingTime { get; }

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