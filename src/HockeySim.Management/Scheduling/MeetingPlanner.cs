using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Management.Scheduling;

/// <summary>
/// Decides who plays whom and where, independent of dates: four meetings with each divisional
/// opponent, three with each other same-conference opponent, and two with each
/// opposite-conference opponent, giving every team 42 home and 42 away matches.
/// </summary>
internal static class MeetingPlanner
{
    private const int DivisionalMeetings = 4;
    private const int ConferenceMeetings = 3;
    private const int InterconferenceMeetings = 2;

    /// <summary>
    /// Returns every meeting in the season. A pair's meetings are listed with venues alternating,
    /// so a calendar that consumes them in order alternates venues through the season; with three
    /// meetings, the team hosting twice hosts the first and last.
    /// </summary>
    public static IReadOnlyList<Meeting> Create(League league, ControlledRandom random)
    {
        ArgumentNullException.ThrowIfNull(league);
        ArgumentNullException.ThrowIfNull(random);

        var placements = league.Conferences
            .SelectMany(conference => conference.Divisions.SelectMany(division =>
                division.Teams.Select(team => (team.Id, Conference: conference, Division: division))))
            .ToDictionary(entry => entry.Id, entry => (entry.Conference, entry.Division));
        var twiceHosts = ChooseTwiceHosts(league, random);
        var meetings = new List<Meeting>();

        for (var index = 0; index < league.Teams.Count; index++)
        {
            foreach (var opponent in league.Teams.Skip(index + 1))
            {
                var team = league.Teams[index];
                var pair = TeamPair.Create(team.Id, opponent.Id);
                var count = CountMeetings(placements[team.Id], placements[opponent.Id]);
                var firstHost = twiceHosts.TryGetValue(pair, out var twiceHost)
                    ? twiceHost
                    : random.Chance(0.5) ? team.Id : opponent.Id;
                var secondHost = firstHost == team.Id ? opponent.Id : team.Id;

                for (var meeting = 0; meeting < count; meeting++)
                {
                    meetings.Add(meeting % 2 == 0
                        ? new Meeting(firstHost, secondHost)
                        : new Meeting(secondHost, firstHost));
                }
            }
        }

        return meetings;
    }

    private static int CountMeetings(
        (Conference Conference, Division Division) team,
        (Conference Conference, Division Division) opponent)
    {
        if (team.Division == opponent.Division)
        {
            return DivisionalMeetings;
        }

        return team.Conference == opponent.Conference ? ConferenceMeetings : InterconferenceMeetings;
    }

    /// <summary>
    /// Chooses which team hosts two of the three meetings between cross-division conference
    /// opponents. Each team must host twice against exactly four of its eight such opponents for
    /// its season to split 42/42; a circulant over shuffled division orders guarantees that on
    /// both sides while the seed varies the opponents.
    /// </summary>
    private static Dictionary<TeamPair, TeamId> ChooseTwiceHosts(League league, ControlledRandom random)
    {
        var twiceHosts = new Dictionary<TeamPair, TeamId>();

        foreach (var conference in league.Conferences)
        {
            var first = RandomOrder.Shuffle(conference.Divisions[0].Teams.Select(team => team.Id), random);
            var second = RandomOrder.Shuffle(conference.Divisions[1].Teams.Select(team => team.Id), random);

            for (var firstIndex = 0; firstIndex < first.Count; firstIndex++)
            {
                for (var secondIndex = 0; secondIndex < second.Count; secondIndex++)
                {
                    var offset = (secondIndex - firstIndex + second.Count) % second.Count;
                    twiceHosts[TeamPair.Create(first[firstIndex], second[secondIndex])] =
                        offset < second.Count / 2 ? first[firstIndex] : second[secondIndex];
                }
            }
        }

        return twiceHosts;
    }
}