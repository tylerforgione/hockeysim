using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// One phase's calendar, such as the preseason or the regular season, in chronological order. A
/// team plays at most once on any date.
/// </summary>
public sealed class SeasonSchedule
{
    private readonly ReadOnlyCollection<ScheduledMatch> _matches;

    public SeasonSchedule(IEnumerable<ScheduledMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);

        var suppliedMatches = matches.ToList();
        if (suppliedMatches.Any(match => match is null))
        {
            throw new ArgumentException("A schedule cannot contain a missing match.", nameof(matches));
        }

        // OrderBy is stable, so matches on the same date keep the order they were supplied in.
        var matchList = suppliedMatches.OrderBy(match => match.Date).ToList();

        var teamsBookedByDate = new HashSet<(DateOnly Date, TeamId TeamId)>();
        foreach (var match in matchList)
        {
            if (!teamsBookedByDate.Add((match.Date, match.HomeTeamId))
                || !teamsBookedByDate.Add((match.Date, match.AwayTeamId)))
            {
                throw new ArgumentException(
                    $"A team cannot be scheduled more than once on {match.Date:yyyy-MM-dd}.",
                    nameof(matches));
            }
        }

        _matches = matchList.AsReadOnly();
    }

    public IReadOnlyList<ScheduledMatch> Matches => _matches;
}