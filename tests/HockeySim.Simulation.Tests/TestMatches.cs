using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Simulation.Tests;

internal static class TestMatches
{
    /// <summary>Simulates the match once for each seed from zero, so results are reproducible.</summary>
    public static List<MatchResult> SimulateMany(
        Match match,
        int count,
        OvertimeFormat overtime = OvertimeFormat.RegularSeason)
    {
        var simulator = new MatchSimulator();
        return Enumerable.Range(0, count)
            .Select(seed => simulator.Simulate(match, overtime, new RandomState((ulong)seed)))
            .ToList();
    }

    public static Match EvenMatch() => TestTeams.CreateMatch(TestTeams.Create("Home"), TestTeams.Create("Away"));

    public static HashSet<PlayerId> DressedSkaterIds(Team team) =>
        team.Lineup.DressedPlayers
            .Where(player => player.Position != Position.Goalie)
            .Select(player => player.Id)
            .ToHashSet();
}