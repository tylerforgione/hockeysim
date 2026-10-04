using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// Orders teams by the NHL regular-season procedure: standings points, then the official
/// tie-breakers in turn (https://www.nhl.com/info/standings-info/tie-breaking-procedure).
/// </summary>
/// <remarks>
/// Ranking partitions a tied group criterion by criterion instead of sorting with a pairwise
/// comparer, because head-to-head depends on which clubs are tied: the same two clubs can compare
/// differently inside a three-club tie than on their own.
/// </remarks>
internal static class StandingsRanking
{
    /// <summary>
    /// Produces an ordering for one group of tied teams; equal teams compare as zero.
    /// </summary>
    private delegate Comparison<TeamRecord> Criterion(
        IReadOnlyList<TeamRecord> tiedTeams,
        IReadOnlyList<CompletedMatch> completedMatches);

    private static readonly Criterion[] Criteria =
    [
        Greater(record => record.Points),

        // 1. Fewer games played, which is the superior points percentage among teams level on points.
        Greater(record => -record.GamesPlayed),

        // 2-4. Regulation wins, then regulation and overtime wins, then all wins.
        Greater(record => record.RegulationWins),
        Greater(record => record.RegulationAndOvertimeWins),
        Greater(record => record.Wins),

        // 5. Points earned in games among the tied clubs.
        HeadToHead,

        // 6-7. Season goal differential, then goals for; both include shootout deciding goals.
        Greater(record => record.GoalDifferential),
        Greater(record => record.GoalsFor),
    ];

    /// <param name="records">The teams to rank, in the order used for teams left entirely level.</param>
    public static ReadOnlyCollection<StandingsEntry> Rank(
        IReadOnlyList<TeamRecord> records,
        IReadOnlyList<CompletedMatch> completedMatches)
    {
        var entries = new List<StandingsEntry>(records.Count);
        foreach (var group in Separate(records, 0, completedMatches))
        {
            var rank = entries.Count + 1;
            entries.AddRange(group.Select(record => new StandingsEntry(rank, record)));
        }

        return entries.AsReadOnly();
    }

    /// <summary>
    /// Splits a group of teams level before <paramref name="criterionIndex"/> into ordered
    /// subgroups, applying later criteria only within each subgroup still tied.
    /// </summary>
    private static IEnumerable<IReadOnlyList<TeamRecord>> Separate(
        IReadOnlyList<TeamRecord> group,
        int criterionIndex,
        IReadOnlyList<CompletedMatch> completedMatches)
    {
        if (group.Count == 1 || criterionIndex == Criteria.Length)
        {
            yield return group;
            yield break;
        }

        var compare = Criteria[criterionIndex](group, completedMatches);

        // Order is stable, so teams level on this criterion keep their incoming order.
        var ordered = group.Order(Comparer<TeamRecord>.Create(compare)).ToList();

        var start = 0;
        for (var index = 1; index <= ordered.Count; index++)
        {
            if (index < ordered.Count && compare(ordered[index - 1], ordered[index]) == 0)
            {
                continue;
            }

            // Clubs still level move on to the next criterion, including after head-to-head: the
            // official text does not say to recompute head-to-head when it separates only part of
            // a larger tie, so the remaining clubs keep their group result and go to goal
            // differential. Recomputing would restart this subgroup at the head-to-head criterion.
            var subgroup = ordered.GetRange(start, index - start);
            foreach (var separated in Separate(subgroup, criterionIndex + 1, completedMatches))
            {
                yield return separated;
            }

            start = index;
        }
    }

    private static Criterion Greater(Func<TeamRecord, int> value) =>
        (_, _) => (first, second) => value(second).CompareTo(value(first));

    /// <summary>
    /// Ranks tied clubs by their share of available points in games among each other. For two
    /// clubs this equals comparing points, since both played the same counted games.
    /// </summary>
    /// <remarks>
    /// When any tied club has no counted game against the others, such as early in the season,
    /// its share is undefined and the criterion leaves the whole group level.
    /// </remarks>
    private static Comparison<TeamRecord> HeadToHead(
        IReadOnlyList<TeamRecord> tiedTeams,
        IReadOnlyList<CompletedMatch> completedMatches)
    {
        var totals = tiedTeams.ToDictionary(record => record.TeamId, _ => (Points: 0, Games: 0));
        foreach (var match in CountedMeetings(totals.Keys.ToHashSet(), completedMatches))
        {
            foreach (var teamId in new[] { match.Home.TeamId, match.Away.TeamId })
            {
                var (points, games) = totals[teamId];
                totals[teamId] = (points + PointsEarned(match, teamId), games + 1);
            }
        }

        if (totals.Values.Any(total => total.Games == 0))
        {
            return (_, _) => 0;
        }

        // Compare points / (2 × games) without division: a ranks first when
        // a.Points × b.Games exceeds b.Points × a.Games.
        return (first, second) =>
        {
            var (firstPoints, firstGames) = totals[first.TeamId];
            var (secondPoints, secondGames) = totals[second.TeamId];
            return (secondPoints * firstGames).CompareTo(firstPoints * secondGames);
        };
    }

    /// <summary>
    /// The completed matches between tied clubs that head-to-head counts. Where two clubs have
    /// played an odd number of games, the first game in the city hosting the extra game is
    /// excluded.
    /// </summary>
    private static IEnumerable<CompletedMatch> CountedMeetings(
        HashSet<TeamId> tiedTeamIds,
        IReadOnlyList<CompletedMatch> completedMatches)
    {
        var pairings = completedMatches
            .Where(match => tiedTeamIds.Contains(match.Home.TeamId) && tiedTeamIds.Contains(match.Away.TeamId))
            .OrderBy(match => match.Date)
            .GroupBy(match => Pairing(match.Home.TeamId, match.Away.TeamId));

        foreach (var pairing in pairings)
        {
            var meetings = pairing.ToList();
            if (meetings.Count % 2 == 1)
            {
                // With an odd number of games between two clubs, one club has hosted more.
                var extraGameHost = meetings
                    .GroupBy(match => match.Home.TeamId)
                    .MaxBy(hosted => hosted.Count())!
                    .Key;
                meetings.Remove(meetings.First(match => match.Home.TeamId == extraGameHost));
            }

            foreach (var match in meetings)
            {
                yield return match;
            }
        }
    }

    private static (TeamId, TeamId) Pairing(TeamId first, TeamId second) =>
        first.Value.CompareTo(second.Value) < 0 ? (first, second) : (second, first);

    // Reuses the team record's points rule so head-to-head never disagrees with the standings.
    private static int PointsEarned(CompletedMatch match, TeamId teamId) =>
        new TeamRecord(teamId).Add(match).Points;
}