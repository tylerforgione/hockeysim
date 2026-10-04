using HockeySim.Domain;

namespace HockeySim.Management.Scheduling;

/// <summary>
/// One planned regular-season match between two teams, with its venue decided but no date yet.
/// </summary>
internal readonly record struct Meeting(TeamId HomeTeamId, TeamId AwayTeamId)
{
    public TeamPair Pair => TeamPair.Create(HomeTeamId, AwayTeamId);
}

/// <summary>
/// Identifies two teams regardless of which one is listed first or hosts.
/// </summary>
internal readonly record struct TeamPair(TeamId Lower, TeamId Higher)
{
    public static TeamPair Create(TeamId first, TeamId second) =>
        first.Value.CompareTo(second.Value) < 0 ? new(first, second) : new(second, first);
}