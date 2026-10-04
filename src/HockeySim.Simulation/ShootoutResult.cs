using System.Collections.ObjectModel;

using HockeySim.Domain;

namespace HockeySim.Simulation;

/// <summary>
/// The shootout that decided a match tied after overtime, in the order attempts were taken.
/// </summary>
public sealed class ShootoutResult
{
    private readonly ReadOnlyCollection<ShootoutAttempt> _attempts;

    internal ShootoutResult(IEnumerable<ShootoutAttempt> attempts, TeamId winnerId)
    {
        _attempts = attempts.ToList().AsReadOnly();
        WinnerId = winnerId;
    }

    public IReadOnlyList<ShootoutAttempt> Attempts => _attempts;

    public TeamId WinnerId { get; }

    public int GoalsFor(TeamId teamId) =>
        _attempts.Count(attempt => attempt.TeamId == teamId && attempt.Scored);
}