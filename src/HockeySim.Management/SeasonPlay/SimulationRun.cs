using HockeySim.Domain;

namespace HockeySim.Management.SeasonPlay;

/// <summary>
/// Decides, after each day of a run, whether simulating ahead stops there. Targets such as the end
/// of a playoff round have no date known in advance, because playoff games are scheduled one at a
/// time, so every check reads the season as it stands after the day.
/// </summary>
internal sealed class SimulationRun
{
    private readonly SimulationTarget _target;
    private readonly TeamId _managedTeamId;
    private readonly DateOnly _startDate;
    private readonly SeasonPhase _startPhase;
    private readonly PlayoffRound? _startRound;

    /// <exception cref="ArgumentException">The season's phase does not offer <paramref name="target"/>.</exception>
    public SimulationRun(SimulationTarget target, Season season, TeamId managedTeamId)
    {
        if (!SimulationTargets.AvailableIn(season.Phase).Contains(target))
        {
            throw new ArgumentException($"Simulating ahead to {target} is not available in the {season.Phase}.", nameof(target));
        }

        _target = target;
        _managedTeamId = managedTeamId;
        _startDate = season.CurrentDate;
        _startPhase = season.Phase;
        _startRound = season.Playoffs?.CurrentRound;
    }

    /// <param name="playedDate">The day just played; the season has moved on to the next day.</param>
    /// <param name="hasPlayersToReplace">Whether the managed team now plays with dressed players who cannot play.</param>
    /// <returns>Why the run stops after this day, or <see langword="null"/> to play the next.</returns>
    public SimulationStop? StopAfter(Season season, DateOnly playedDate, bool hasPlayersToReplace)
    {
        if (IsTargetReached(season))
        {
            return SimulationStop.TargetReached;
        }

        if (season.IsComplete)
        {
            return SimulationStop.SeasonComplete;
        }

        if (season.Phase != _startPhase)
        {
            return SimulationStop.PhaseChanged;
        }

        if (WasManagedPlayerInjured(season, playedDate))
        {
            return SimulationStop.ManagedPlayerInjured;
        }

        return hasPlayersToReplace ? SimulationStop.PlayersToReplace : null;
    }

    private bool IsTargetReached(Season season) => _target switch
    {
        SimulationTarget.NextDay => true,
        SimulationTarget.NextMatch => season.CurrentDateMatches.Any(Involves),
        SimulationTarget.OneWeek => season.CurrentDate >= _startDate.AddDays(7),
        SimulationTarget.OneMonth => season.CurrentDate >= _startDate.AddMonths(1),
        SimulationTarget.EndOfPreseason => season.Phase != SeasonPhase.Preseason,
        SimulationTarget.EndOfRegularSeason => season.Phase == SeasonPhase.Playoffs,
        SimulationTarget.EndOfRound => season.IsComplete || season.Playoffs?.CurrentRound != _startRound,
        SimulationTarget.EndOfPlayoffs => season.IsComplete,
        _ => throw new InvalidOperationException($"Unknown simulation target {_target}."),
    };

    /// <summary>
    /// Whether the managed team's match on <paramref name="playedDate"/> injured one of its players
    /// badly enough to keep them out: the injuries the head trainer reports on their own.
    /// </summary>
    private bool WasManagedPlayerInjured(Season season, DateOnly playedDate) =>
        season.CompletedMatches.Concat(season.Playoffs?.CompletedMatches ?? [])
            .Where(match => match.Date == playedDate)
            .SelectMany(match => match.Health.Injuries)
            .Any(injury => injury.TeamId == _managedTeamId && !injury.Definition.CanPlayThrough);

    private bool Involves(ScheduledMatch match) => match.HomeTeamId == _managedTeamId || match.AwayTeamId == _managedTeamId;
}