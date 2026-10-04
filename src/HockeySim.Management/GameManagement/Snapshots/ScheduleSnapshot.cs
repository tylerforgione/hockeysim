using System.Collections.ObjectModel;

using HockeySim.Domain;

namespace HockeySim.Management.GameManagement.Snapshots;

public sealed class ScheduleSnapshot
{
    private readonly ReadOnlyCollection<ScheduledMatchSnapshot> _matches;

    private ScheduleSnapshot(IReadOnlyList<ScheduledMatchSnapshot> matches)
    {
        _matches = new ReadOnlyCollection<ScheduledMatchSnapshot>(matches.ToList());
    }

    /// <summary>
    /// Gets every regular-season match in chronological order.
    /// </summary>
    public IReadOnlyList<ScheduledMatchSnapshot> Matches => _matches;

    internal static ScheduleSnapshot Create(SeasonSchedule schedule) =>
        new(schedule.Matches.Select(ScheduledMatchSnapshot.Create).ToList());
}

public sealed record ScheduledMatchSnapshot(DateOnly Date, TeamId HomeTeamId, TeamId AwayTeamId)
{
    internal static ScheduledMatchSnapshot Create(ScheduledMatch match) =>
        new(match.Date, match.HomeTeamId, match.AwayTeamId);
}