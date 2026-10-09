using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Management.Scheduling;

/// <summary>
/// Generates the preseason: every team plays each divisional opponent once, in rounds in which
/// every team plays, one round every other day ending the day before opening day.
/// </summary>
internal static class PreseasonGenerator
{
    private const int DaysBetweenRounds = 2;

    public static SeasonSchedule Create(League league, DateOnly openingDay, ControlledRandom random)
    {
        ArgumentNullException.ThrowIfNull(league);
        ArgumentNullException.ThrowIfNull(random);

        var divisions = league.Conferences
            .SelectMany(conference => conference.Divisions)
            .Select(division => RandomOrder.Shuffle(division.Teams.Select(team => team.Id), random))
            .ToList();
        var hosts = divisions.SelectMany(division => ChooseHosts(division, random)).ToDictionary();
        var rounds = RandomOrder.Shuffle(RoundRobin.Combine(divisions.Select(RoundRobin.Create)), random);
        var firstDay = openingDay.AddDays(-rounds.Count * DaysBetweenRounds + 1);

        return new SeasonSchedule(rounds.SelectMany((round, index) => round.Select(pair =>
        {
            var home = hosts[pair];
            var away = home == pair.Lower ? pair.Higher : pair.Lower;
            return new ScheduledMatch(firstDay.AddDays(index * DaysBetweenRounds), home, away);
        })));
    }

    /// <summary>
    /// Decides who hosts each divisional pair so every team's home matches differ by at most one:
    /// around the shuffled circle, each team hosts the next teams up to half-way, and the teams
    /// directly opposite each other, in an even division, meet at either venue by chance.
    /// </summary>
    private static IEnumerable<KeyValuePair<TeamPair, TeamId>> ChooseHosts(IReadOnlyList<TeamId> division, ControlledRandom random)
    {
        var count = division.Count;
        for (var index = 0; index < count; index++)
        {
            for (var offset = 1; offset <= (count - 1) / 2; offset++)
            {
                var opponent = division[(index + offset) % count];
                yield return new(TeamPair.Create(division[index], opponent), division[index]);
            }
        }

        if (count % 2 == 0)
        {
            for (var index = 0; index < count / 2; index++)
            {
                var opposite = division[index + (count / 2)];
                yield return new(TeamPair.Create(division[index], opposite), random.Chance(0.5) ? division[index] : opposite);
            }
        }
    }
}