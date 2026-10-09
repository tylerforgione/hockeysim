using System.Collections.ObjectModel;

using HockeySim.Domain;

namespace HockeySim.Management.GameManagement.Snapshots;

public sealed class ScheduleSnapshot
{
    private readonly ReadOnlyCollection<ScheduledMatchSnapshot> _preseasonMatches;
    private readonly ReadOnlyCollection<ScheduledMatchSnapshot> _matches;

    private ScheduleSnapshot(IReadOnlyList<ScheduledMatchSnapshot> preseasonMatches, IReadOnlyList<ScheduledMatchSnapshot> matches)
    {
        _preseasonMatches = new ReadOnlyCollection<ScheduledMatchSnapshot>(preseasonMatches.ToList());
        _matches = new ReadOnlyCollection<ScheduledMatchSnapshot>(matches.ToList());
    }

    /// <summary>
    /// Gets every preseason match in chronological order, all before the regular season's.
    /// </summary>
    public IReadOnlyList<ScheduledMatchSnapshot> PreseasonMatches => _preseasonMatches;

    /// <summary>
    /// Gets every regular-season match in chronological order.
    /// </summary>
    public IReadOnlyList<ScheduledMatchSnapshot> Matches => _matches;

    internal static ScheduleSnapshot Create(Season season) =>
        new(
            season.PreseasonSchedule.Matches.Select(ScheduledMatchSnapshot.Create).ToList(),
            season.Schedule.Matches.Select(ScheduledMatchSnapshot.Create).ToList());
}

public sealed record ScheduledMatchSnapshot(DateOnly Date, TeamId HomeTeamId, TeamId AwayTeamId)
{
    internal static ScheduledMatchSnapshot Create(ScheduledMatch match) =>
        new(match.Date, match.HomeTeamId, match.AwayTeamId);
}