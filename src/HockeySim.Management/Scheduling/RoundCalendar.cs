using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Management.Scheduling;

/// <summary>
/// The simplest calendar: 84 rounds in which every team plays exactly once, one round every
/// other day from the opening day. Venues come from the meeting plan, so a calendar with
/// breaks, uneven nights, or rest rules can take the same meetings instead.
/// </summary>
/// <remarks>
/// Rounds come from decomposing this league's fixed structure: two league-wide round robins
/// (31 rounds each) give every pair two meetings, two division round robins (7 rounds each) add
/// the extra divisional meetings, and a round robin between the two divisions of each conference
/// (8 rounds) adds the third cross-division meeting. Each round's pairings then take the pair's
/// next planned meeting, keeping its venue.
/// </remarks>
internal static class RoundCalendar
{
    private const int DaysBetweenRounds = 2;
    private const int LeagueRoundRobinCount = 2;
    private const int DivisionRoundRobinCount = 2;

    public static IReadOnlyList<ScheduledMatch> Assign(
        League league,
        IReadOnlyList<Meeting> meetings,
        DateOnly openingDay,
        ControlledRandom random)
    {
        ArgumentNullException.ThrowIfNull(league);
        ArgumentNullException.ThrowIfNull(meetings);
        ArgumentNullException.ThrowIfNull(random);

        var rounds = RandomOrder.Shuffle(CreateRounds(league, random), random);
        var unscheduled = meetings
            .GroupBy(meeting => meeting.Pair)
            .ToDictionary(group => group.Key, group => new Queue<Meeting>(group));
        var matches = new List<ScheduledMatch>(meetings.Count);

        for (var roundIndex = 0; roundIndex < rounds.Count; roundIndex++)
        {
            var date = openingDay.AddDays(roundIndex * DaysBetweenRounds);

            foreach (var pair in rounds[roundIndex])
            {
                if (!unscheduled.TryGetValue(pair, out var pairMeetings) || pairMeetings.Count == 0)
                {
                    throw new InvalidOperationException("The rounds do not match the planned meetings.");
                }

                var meeting = pairMeetings.Dequeue();
                matches.Add(new ScheduledMatch(date, meeting.HomeTeamId, meeting.AwayTeamId));
            }
        }

        if (unscheduled.Values.Any(pairMeetings => pairMeetings.Count > 0))
        {
            throw new InvalidOperationException("The rounds do not match the planned meetings.");
        }

        return matches;
    }

    private static List<IReadOnlyList<TeamPair>> CreateRounds(League league, ControlledRandom random)
    {
        var rounds = new List<IReadOnlyList<TeamPair>>();
        for (var pass = 0; pass < LeagueRoundRobinCount; pass++)
        {
            rounds.AddRange(CreateRoundRobin(RandomOrder.Shuffle(league.Teams.Select(team => team.Id), random)));
        }

        var conferences = league.Conferences
            .Select(conference => conference.Divisions
                .Select(division => RandomOrder.Shuffle(division.Teams.Select(team => team.Id), random))
                .ToList())
            .ToList();

        var divisions = conferences.SelectMany(divisionsInConference => divisionsInConference).ToList();
        for (var pass = 0; pass < DivisionRoundRobinCount; pass++)
        {
            rounds.AddRange(CombineRounds(divisions.Select(CreateRoundRobin)));
        }

        rounds.AddRange(CombineRounds(conferences.Select(divisionsInConference =>
            CreateCrossDivisionRounds(divisionsInConference[0], divisionsInConference[1]))));

        return rounds;
    }

    /// <summary>
    /// Pairs every team with every other team once, using the circle method: one team stays
    /// fixed while the rest rotate, so each round is a perfect matching.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<TeamPair>> CreateRoundRobin(IReadOnlyList<TeamId> teams)
    {
        var rotating = teams.Skip(1).ToList();
        var rounds = new List<IReadOnlyList<TeamPair>>(rotating.Count);

        for (var round = 0; round < rotating.Count; round++)
        {
            var order = new List<TeamId>(teams.Count) { teams[0] };
            order.AddRange(rotating.Skip(round).Concat(rotating.Take(round)));

            rounds.Add(Enumerable.Range(0, order.Count / 2)
                .Select(index => TeamPair.Create(order[index], order[order.Count - 1 - index]))
                .ToList());
        }

        return rounds;
    }

    /// <summary>
    /// Pairs every team in one division with every team in the other once.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<TeamPair>> CreateCrossDivisionRounds(
        IReadOnlyList<TeamId> first,
        IReadOnlyList<TeamId> second) =>
        Enumerable.Range(0, second.Count)
            .Select(offset => (IReadOnlyList<TeamPair>)Enumerable.Range(0, first.Count)
                .Select(index => TeamPair.Create(first[index], second[(index + offset) % second.Count]))
                .ToList())
            .ToList();

    /// <summary>
    /// Merges same-numbered rounds from disjoint groups of teams into league-wide rounds.
    /// </summary>
    private static IEnumerable<IReadOnlyList<TeamPair>> CombineRounds(
        IEnumerable<IReadOnlyList<IReadOnlyList<TeamPair>>> groups)
    {
        var groupList = groups.ToList();
        return Enumerable.Range(0, groupList[0].Count)
            .Select(round => groupList.SelectMany(group => group[round]).ToList());
    }
}