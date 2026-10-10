using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Simulation;
using HockeySim.Simulation.Randomness;

using Xunit;

using static HockeySim.Management.Tests.SaveTestData;
using static HockeySim.Management.Tests.SeasonAdvancementTests;

namespace HockeySim.Management.Tests;

public sealed class PreseasonTests
{
    private static readonly DateOnly FirstPreseasonDay = new(2026, 9, 18);
    private static readonly DateOnly OpeningDay = new(2026, 10, 1);

    public static TheoryData<ulong> Seeds => new() { 1, 84, 1344, 987654321, ulong.MaxValue };

    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryTeamPlaysEachDivisionalOpponentOnceWithHomeMatchesBalanced(ulong seed)
    {
        var snapshot = StartGame(new GameManager(), seed);
        var matches = snapshot.Schedule.PreseasonMatches;

        Assert.Equal(112, matches.Count);
        foreach (var division in snapshot.League.Conferences.SelectMany(conference => conference.Divisions))
        {
            Assert.All(division.Teams, team =>
            {
                var played = matches.Where(match => match.HomeTeamId == team.Id || match.AwayTeamId == team.Id).ToList();
                var opponents = played.Select(match => match.HomeTeamId == team.Id ? match.AwayTeamId : match.HomeTeamId);

                Assert.Equal(
                    division.Teams.Select(opponent => opponent.Id).Where(id => id != team.Id).OrderBy(id => id.Value),
                    opponents.OrderBy(id => id.Value));
                Assert.InRange(played.Count(match => match.HomeTeamId == team.Id), 3, 4);
            });
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryTeamPlaysEveryOtherDayUntilTheDayBeforeOpeningDay(ulong seed)
    {
        var snapshot = StartGame(new GameManager(), seed);
        var matches = snapshot.Schedule.PreseasonMatches;

        Assert.Equal(matches.OrderBy(match => match.Date).Select(match => match.Date), matches.Select(match => match.Date));
        Assert.Equal(
            Enumerable.Range(0, 7).Select(round => FirstPreseasonDay.AddDays(round * 2)),
            matches.Select(match => match.Date).Distinct());
        Assert.Equal(OpeningDay.AddDays(-1), matches[^1].Date);
        Assert.Equal(OpeningDay, snapshot.Schedule.Matches[0].Date);
        Assert.All(matches.GroupBy(match => match.Date), day =>
        {
            var teamsPlaying = day.SelectMany(match => new[] { match.HomeTeamId, match.AwayTeamId }).ToList();
            Assert.Equal(32, teamsPlaying.Count);
            Assert.Equal(32, teamsPlaying.Distinct().Count());
        });
    }

    [Fact]
    public void ThePreseasonIsGeneratedFromTheRandomStream()
    {
        var first = StartGame(new GameManager(), seed: 424242);
        var second = StartGame(new GameManager(), seed: 424242);
        var other = StartGame(new GameManager(), seed: 424243);

        Assert.Equal(first.Schedule.PreseasonMatches, second.Schedule.PreseasonMatches);
        Assert.NotEqual(Venues(first), Venues(other));
    }

    [Fact]
    public void ANewGameStartsOnTheFirstPreseasonDay()
    {
        var snapshot = StartGame(new GameManager());

        Assert.Equal(SeasonPhase.Preseason, snapshot.Season.Phase);
        Assert.Equal(FirstPreseasonDay, snapshot.Season.CurrentDate);
        Assert.Empty(snapshot.Season.PreseasonResults);
        Assert.Empty(snapshot.Season.Results);
    }

    [Fact]
    public void PreseasonResultsAreKeptButCountTowardNothing()
    {
        var manager = new GameManager();
        var start = StartGame(manager);

        var openingDay = manager.PlayPreseason();

        Assert.Equal(
            start.Schedule.PreseasonMatches.Select(match => (match.Date, match.HomeTeamId, match.AwayTeamId)),
            openingDay.Season.PreseasonResults.Select(result => (result.Date, result.Home.TeamId, result.Away.TeamId)));
        Assert.All(openingDay.Season.PreseasonResults, result => Assert.NotEmpty(result.Home.Skaters));
        Assert.Empty(openingDay.Season.Results);
        Assert.Equal(start.Season.TeamRecords, openingDay.Season.TeamRecords);
        Assert.Equal(start.Season.TeamStatistics, openingDay.Season.TeamStatistics);
        Assert.Empty(openingDay.Season.SkaterStatistics);
        Assert.Empty(openingDay.Season.GoalieStatistics);
        Assert.All(openingDay.Season.Standings.League, entry => Assert.Equal(0, entry.Record.GamesPlayed));
        Assert.All(openingDay.League.Teams.SelectMany(team => team.Roster), player => Assert.Empty(player.Injuries));
        Assert.Empty(openingDay.PlayersToReplace);
    }

    [Fact]
    public void PreseasonMatchesCannotInjureOrWearAnyone()
    {
        var simulator = new HealthRecordingSimulator();
        var manager = new GameManager(simulator);
        StartGame(manager);

        manager.PlayPreseason();
        var preseasonMatches = simulator.InjuriesPossible.Count;
        manager.AdvanceDay();

        Assert.Equal(112, preseasonMatches);
        Assert.All(simulator.InjuriesPossible.Take(preseasonMatches), possible => Assert.False(possible));
        Assert.All(simulator.InjuriesPossible.Skip(preseasonMatches), possible => Assert.True(possible));
    }

    [Fact]
    public void AdvancingPastTheLastPreseasonDayLeadsToOpeningDay()
    {
        var manager = new GameManager();
        var snapshot = StartGame(manager);
        while (snapshot.Season.CurrentDate < OpeningDay.AddDays(-1))
        {
            snapshot = manager.AdvanceDay();
        }

        Assert.Equal(SeasonPhase.Preseason, snapshot.Season.Phase);
        Assert.Equal(96, snapshot.Season.PreseasonResults.Count);

        var openingDay = manager.AdvanceDay();

        Assert.Equal(OpeningDay, openingDay.Season.CurrentDate);
        Assert.Equal(SeasonPhase.RegularSeason, openingDay.Season.Phase);
        Assert.Equal(112, openingDay.Season.PreseasonResults.Count);
        Assert.Empty(openingDay.Season.Results);

        var afterOpeningDay = manager.AdvanceDay();

        Assert.Equal(16, afterOpeningDay.Season.Results.Count);
        Assert.All(afterOpeningDay.Season.Results, result => Assert.Equal(OpeningDay, result.Date));
        Assert.Equal(112, afterOpeningDay.Season.PreseasonResults.Count);
    }

    [Fact]
    public void TheManagedTeamCanChangeItsLineupDuringThePreseason()
    {
        var manager = new GameManager();
        var team = ManagedTeam(StartGame(manager));
        var newStarterId = team.Lineup.BackupGoalieId;
        manager.SetLineup(CurrentLineup(team.Lineup) with
        {
            StartingGoalieId = newStarterId,
            BackupGoalieId = team.Lineup.StartingGoalieId,
        });

        var snapshot = manager.AdvanceDay();

        var result = snapshot.Season.PreseasonResults.Single(match => match.Home.TeamId == team.Id || match.Away.TeamId == team.Id);
        Assert.Equal(newStarterId, (result.Home.TeamId == team.Id ? result.Home : result.Away).Goalie.PlayerId);
    }

    [Fact]
    public void TheSameStartAndActionsProduceTheSamePreseason()
    {
        var first = new GameManager();
        StartGame(first);
        var second = new GameManager();
        StartGame(second);

        var firstOpeningDay = first.PlayPreseason();
        var secondOpeningDay = second.PlayPreseason();

        Assert.Equal(Fingerprint(firstOpeningDay.Season.PreseasonResults), Fingerprint(secondOpeningDay.Season.PreseasonResults));
        Assert.Equal(firstOpeningDay.RandomState, secondOpeningDay.RandomState);
    }

    [Fact]
    public void AGameSavedMidPreseasonLoadsAndContinuesExactly()
    {
        var uninterrupted = new GameManager();
        StartGame(uninterrupted);
        var manager = new GameManager();
        StartGame(manager);
        for (var day = 0; day < 5; day++)
        {
            uninterrupted.AdvanceDay();
            manager.AdvanceDay();
        }

        var store = new MemorySaveStore();
        manager.SaveGame(store);
        var loaded = new GameManager();
        var restored = loaded.LoadGame(store);

        Assert.Equal(SeasonPhase.Preseason, restored.Season.Phase);
        Assert.Equal(48, restored.Season.PreseasonResults.Count);
        Assert.Equal(Describe(manager.GetSnapshot()), Describe(restored));

        uninterrupted.PlayPreseason();
        loaded.PlayPreseason();
        for (var day = 0; day < 3; day++)
        {
            uninterrupted.AdvanceDayReplacingInjured();
            loaded.AdvanceDayReplacingInjured();
        }

        Assert.Equal(Describe(uninterrupted.GetSnapshot()), Describe(loaded.GetSnapshot()));
    }

    private static string Venues(GameSnapshot snapshot)
    {
        // Team identities differ between seeds, so compare the preseason by team name.
        var names = snapshot.League.Teams.ToDictionary(team => team.Id, team => team.Name);
        return string.Join(
            '|',
            snapshot.Schedule.PreseasonMatches.Select(match => $"{match.Date:O}:{names[match.HomeTeamId]}:{names[match.AwayTeamId]}"));
    }

    /// <summary>Plays matches with the real engine and records whether each could injure anyone.</summary>
    private sealed class HealthRecordingSimulator : IMatchSimulator
    {
        private readonly MatchSimulator _engine = new();

        public List<bool> InjuriesPossible { get; } = [];

        public MatchResult Simulate(Match match, OvertimeFormat overtime, MatchHealth health, RandomState randomState)
        {
            InjuriesPossible.Add(health.InjuriesPossible);
            return _engine.Simulate(match, overtime, health, randomState);
        }
    }
}