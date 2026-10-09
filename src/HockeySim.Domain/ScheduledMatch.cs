namespace HockeySim.Domain;

/// <summary>
/// A meeting between two teams on a calendar date, identified by team so the
/// schedule never holds references into mutable team state.
/// </summary>
public sealed class ScheduledMatch
{
    public ScheduledMatch(DateOnly date, TeamId homeTeamId, TeamId awayTeamId)
    {
        if (homeTeamId.Value == Guid.Empty || awayTeamId.Value == Guid.Empty)
        {
            throw new ArgumentException("A scheduled match requires both team identities.");
        }

        if (homeTeamId == awayTeamId)
        {
            throw new ArgumentException("A team cannot be scheduled to play against itself.");
        }

        Date = date;
        HomeTeamId = homeTeamId;
        AwayTeamId = awayTeamId;
    }

    public DateOnly Date { get; }

    public TeamId HomeTeamId { get; }

    public TeamId AwayTeamId { get; }
}