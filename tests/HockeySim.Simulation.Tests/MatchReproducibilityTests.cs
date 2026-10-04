using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

using Xunit;

namespace HockeySim.Simulation.Tests;

public sealed class MatchReproducibilityTests
{
    [Fact]
    public void SameTeamsAndRandomStateProduceAnIdenticalResult()
    {
        var match = TestTeams.CreateMatch(TestTeams.Create("Home"), TestTeams.Create("Away"));

        for (var seed = 0UL; seed < 50; seed++)
        {
            var first = new MatchSimulator().Simulate(match, new RandomState(seed));
            var second = new MatchSimulator().Simulate(match, new RandomState(seed));

            Assert.Equal(Fingerprint(first), Fingerprint(second));
            Assert.Equal(first.RandomState, second.RandomState);
        }
    }

    [Fact]
    public void DifferentRandomStatesProduceDifferentResults()
    {
        var match = TestTeams.CreateMatch(TestTeams.Create("Home"), TestTeams.Create("Away"));
        var simulator = new MatchSimulator();

        var fingerprints = Enumerable.Range(0, 20)
            .Select(seed => Fingerprint(simulator.Simulate(match, new RandomState((ulong)seed))))
            .Distinct()
            .Count();

        Assert.Equal(20, fingerprints);
    }

    [Fact]
    public void ResultAdvancesTheRandomStateSoTheNextMatchIsNotARepeat()
    {
        var match = TestTeams.CreateMatch(TestTeams.Create("Home"), TestTeams.Create("Away"));
        var simulator = new MatchSimulator();
        var initialState = new RandomState(7);

        var first = simulator.Simulate(match, initialState);
        var second = simulator.Simulate(match, first.RandomState);

        Assert.NotEqual(initialState, first.RandomState);
        Assert.NotEqual(Fingerprint(first), Fingerprint(second));
    }

    [Fact]
    public void SimulationDoesNotChangeTheTeams()
    {
        var home = TestTeams.Create("Home");
        var away = TestTeams.Create("Away");
        var match = TestTeams.CreateMatch(home, away);
        var homeLineup = home.Lineup;
        var awayLineup = away.Lineup;
        var before = TeamFingerprint(home) + TeamFingerprint(away);

        for (var seed = 0UL; seed < 50; seed++)
        {
            new MatchSimulator().Simulate(match, new RandomState(seed));
        }

        Assert.Same(home, match.Home);
        Assert.Same(away, match.Away);
        Assert.Same(homeLineup, home.Lineup);
        Assert.Same(awayLineup, away.Lineup);
        Assert.Equal(before, TeamFingerprint(home) + TeamFingerprint(away));
    }

    private static string Fingerprint(MatchResult result) =>
        string.Join(
            "|",
            TeamResultFingerprint(result.Home),
            TeamResultFingerprint(result.Away),
            result.Decision,
            string.Join(",", result.Goals),
            string.Join(",", result.Shootout?.Attempts ?? []),
            result.RandomState);

    private static string TeamResultFingerprint(MatchTeamResult team) =>
        $"{team.TeamId}:{team.Score}:{team.Shots}:{team.Goalie}:{string.Join(",", team.Skaters)}";

    private static string TeamFingerprint(Team team) =>
        string.Join(
            "|",
            team.Id,
            string.Join(",", team.Roster.Select(player =>
                $"{player.Id}:{player.Number}:{string.Join(";", player.Ratings.Select(rating => $"{rating.Key}={rating.Value.Value}"))}")),
            string.Join(",", team.Lineup.DressedPlayers.Select(player => player.Id)),
            team.Lineup.StartingGoalie.Id,
            team.Lineup.BackupGoalie.Id);
}