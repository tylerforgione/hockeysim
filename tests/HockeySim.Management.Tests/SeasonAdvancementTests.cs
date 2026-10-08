using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Lineups;
using HockeySim.Management.NewGame;
using HockeySim.Simulation;
using HockeySim.Simulation.Randomness;

using Xunit;

namespace HockeySim.Management.Tests;

public sealed class SeasonAdvancementTests
{
    private const string ManagedTeamName = "Halifax Mariners";
    private static readonly DateOnly OpeningDay = new(2026, 10, 1);

    [Fact]
    public void ANewGameStartsOnOpeningDayWithNothingPlayed()
    {
        var snapshot = StartGame(new GameManager());

        Assert.Equal(OpeningDay, snapshot.Season.CurrentDate);
        Assert.Equal(snapshot.Schedule.Matches[0].Date, snapshot.Season.CurrentDate);
        Assert.False(snapshot.Season.IsComplete);
        Assert.Empty(snapshot.Season.Results);
        Assert.Empty(snapshot.Season.SkaterStatistics);
        Assert.Empty(snapshot.Season.GoalieStatistics);
        Assert.Equal(snapshot.League.Teams.Select(team => team.Id), snapshot.Season.TeamRecords.Select(record => record.TeamId));
        Assert.All(snapshot.Season.TeamRecords, record => Assert.Equal(0, record.GamesPlayed));
    }

    [Fact]
    public void AdvancingPlaysEveryMatchScheduledOnTheCurrentDate()
    {
        var manager = new GameManager();
        var before = StartGame(manager);

        var after = manager.AdvanceDay();

        var scheduled = before.Schedule.Matches.Where(match => match.Date == OpeningDay).ToList();
        Assert.Equal(16, scheduled.Count);
        Assert.Equal(
            scheduled.Select(match => (match.Date, match.HomeTeamId, match.AwayTeamId)),
            after.Season.Results.Select(result => (result.Date, result.Home.TeamId, result.Away.TeamId)));
        Assert.Equal(OpeningDay.AddDays(1), after.Season.CurrentDate);
        Assert.All(after.Season.TeamRecords, record => Assert.Equal(1, record.GamesPlayed));
        Assert.NotEqual(before.RandomState, after.RandomState);
    }

    [Fact]
    public void ADayWithoutMatchesMovesTheDateWithoutPlayingOrUsingRandomness()
    {
        var manager = new GameManager();
        StartGame(manager);
        var afterOpeningDay = manager.AdvanceDay();

        var afterEmptyDay = manager.AdvanceDay();

        Assert.DoesNotContain(afterOpeningDay.Schedule.Matches, match => match.Date == OpeningDay.AddDays(1));
        Assert.Equal(OpeningDay.AddDays(2), afterEmptyDay.Season.CurrentDate);
        Assert.Equal(afterOpeningDay.Season.Results.Count, afterEmptyDay.Season.Results.Count);
        Assert.Equal(afterOpeningDay.RandomState, afterEmptyDay.RandomState);
    }

    [Fact]
    public void ALineupChangeIsUsedForTheNextMatch()
    {
        var manager = new GameManager();
        var before = StartGame(manager);
        var team = ManagedTeam(before);
        var scratchedGoalieId = team.ScratchedPlayerIds.Single(
            id => team.Roster.Single(player => player.Id == id).Position == Position.Goalie);
        var scratchedSkater = team.Roster.First(
            player => team.ScratchedPlayerIds.Contains(player.Id) && player.Position != Position.Goalie);
        var previousStarterId = team.Lineup.StartingGoalieId;
        var (lineup, replacedSkaterId) = DressInBottomUnit(CurrentLineup(team.Lineup), scratchedSkater);
        manager.SetLineup(lineup with { StartingGoalieId = scratchedGoalieId, BackupGoalieId = previousStarterId });

        var after = manager.AdvanceDay();

        var result = after.Season.Results.Single(match =>
            match.Home.TeamId == team.Id || match.Away.TeamId == team.Id);
        var side = result.Home.TeamId == team.Id ? result.Home : result.Away;
        Assert.Equal(scratchedGoalieId, side.Goalie.PlayerId);
        Assert.DoesNotContain(after.Season.GoalieStatistics, goalie => goalie.PlayerId == previousStarterId);
        Assert.Contains(side.Skaters, skater => skater.PlayerId == scratchedSkater.Id);
        Assert.DoesNotContain(side.Skaters, skater => skater.PlayerId == replacedSkaterId);
    }

    [Fact]
    public void AdvancingBeforeAGameStartsIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new GameManager().AdvanceDay());
    }

    [Fact]
    public void AFailedMatchLeavesTheDayUnplayedAndTheRandomStateUnchanged()
    {
        var simulator = new InterceptingSimulator();
        var manager = new GameManager(simulator);
        var before = StartGame(manager);
        simulator.BeforeMatch = call =>
        {
            if (call == 5)
            {
                throw new InvalidOperationException("Engine failure.");
            }
        };

        var error = Assert.Throws<InvalidOperationException>(() => manager.AdvanceDay());

        Assert.Equal("Engine failure.", error.Message);
        var after = manager.GetSnapshot();
        Assert.Equal(before.Season.CurrentDate, after.Season.CurrentDate);
        Assert.Equal(before.RandomState, after.RandomState);
        Assert.Empty(after.Season.Results);
        Assert.Empty(after.Season.SkaterStatistics);
        Assert.All(after.Season.TeamRecords, record => Assert.Equal(0, record.GamesPlayed));

        // Retrying produces exactly what an uninterrupted game would have played.
        simulator.BeforeMatch = null;
        var retried = manager.AdvanceDay();
        var reference = new GameManager();
        StartGame(reference);
        var uninterrupted = reference.AdvanceDay();
        Assert.Equal(Fingerprint(uninterrupted), Fingerprint(retried));
        Assert.Equal(uninterrupted.RandomState, retried.RandomState);
    }

    [Fact]
    public void CommandsIssuedWhileADayIsBeingPlayedAreRejected()
    {
        var simulator = new InterceptingSimulator();
        var manager = new GameManager(simulator);
        var before = StartGame(manager);
        var lineup = CurrentLineup(ManagedTeam(before).Lineup);
        Exception? nestedAdvance = null;
        Exception? nestedLineupChange = null;
        simulator.BeforeMatch = call =>
        {
            if (call == 1)
            {
                nestedAdvance = Record.Exception(() => manager.AdvanceDay());
                nestedLineupChange = Record.Exception(() => manager.SetLineup(lineup));
            }
        };

        var after = manager.AdvanceDay();

        Assert.IsType<InvalidOperationException>(nestedAdvance);
        Assert.IsType<InvalidOperationException>(nestedLineupChange);
        Assert.Equal(OpeningDay.AddDays(1), after.Season.CurrentDate);
        Assert.Equal(16, after.Season.Results.Count);
    }

    [Fact]
    public async Task AdvancementsFromDifferentThreadsDoNotOverlap()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // Generous timeouts: CI runs test projects in parallel on slow runners.
        var timeout = TimeSpan.FromSeconds(30);
        var firstDayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseFirstDay = new ManualResetEventSlim();
        var simulator = new InterceptingSimulator();
        var manager = new GameManager(simulator);
        StartGame(manager);
        simulator.BeforeMatch = call =>
        {
            if (call == 1)
            {
                firstDayStarted.TrySetResult();
                releaseFirstDay.Wait(timeout);
            }
        };

        var first = Task.Run(manager.AdvanceDay, cancellationToken);
        await firstDayStarted.Task.WaitAsync(timeout, cancellationToken);
        var second = Task.Run(manager.AdvanceDay, cancellationToken);

        var finishedFirst = await Task.WhenAny(second, Task.Delay(200, cancellationToken));
        Assert.NotSame(second, finishedFirst);
        releaseFirstDay.Set();
        await Task.WhenAll(first, second).WaitAsync(timeout, cancellationToken);

        var final = manager.GetSnapshot();
        Assert.Equal(OpeningDay.AddDays(2), final.Season.CurrentDate);
        Assert.Equal(16, final.Season.Results.Count);
    }

    [Fact]
    public void EarlierSnapshotsAreUnchangedByAdvancingAndCannotBeModified()
    {
        var manager = new GameManager();
        var before = StartGame(manager);

        var after = manager.AdvanceDay();

        Assert.Empty(before.Season.Results);
        Assert.Equal(OpeningDay, before.Season.CurrentDate);
        Assert.All(before.Season.TeamRecords, record => Assert.Equal(0, record.GamesPlayed));
        Assert.Throws<NotSupportedException>(() => ((IList<CompletedMatchSnapshot>)after.Season.Results).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<TeamRecordSnapshot>)after.Season.TeamRecords).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IList<SkaterSeasonStatisticsSnapshot>)after.Season.SkaterStatistics).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IList<SkaterBoxScoreSnapshot>)after.Season.Results[0].Home.Skaters).Clear());
    }

    [Fact]
    public void TheSameStartAndActionsProduceTheSameResults()
    {
        static GameSnapshot Play()
        {
            var manager = new GameManager();
            var start = StartGame(manager);
            for (var day = 0; day < 10; day++)
            {
                manager.AdvanceDay();
            }

            var team = ManagedTeam(manager.GetSnapshot());
            var backupId = team.Lineup.BackupGoalieId;
            manager.SetLineup(CurrentLineup(team.Lineup) with
            {
                StartingGoalieId = backupId,
                BackupGoalieId = team.Lineup.StartingGoalieId,
            });

            GameSnapshot snapshot = start;
            for (var day = 0; day < 10; day++)
            {
                snapshot = manager.AdvanceDay();
            }

            return snapshot;
        }

        var first = Play();
        var second = Play();

        Assert.Equal(Fingerprint(first), Fingerprint(second));
        Assert.Equal(first.RandomState, second.RandomState);
        Assert.Equal(first.Season.TeamRecords, second.Season.TeamRecords);
        Assert.Equal(first.Season.SkaterStatistics, second.Season.SkaterStatistics);
        Assert.Equal(first.Season.GoalieStatistics, second.Season.GoalieStatistics);
    }

    internal static GameSnapshot StartGame(GameManager manager, ulong seed = 12345) =>
        manager.StartNewGame(new NewGameCommand(2026, new RandomState(seed), ManagedTeamName));

    internal static TeamSnapshot ManagedTeam(GameSnapshot snapshot) =>
        snapshot.League.Teams.Single(team => team.Id == snapshot.ManagedTeamId);

    internal static SetLineupCommand CurrentLineup(LineupSnapshot lineup) => SetLineupCommand.From(lineup);

    /// <summary>
    /// Puts a scratched skater into the matching slot of the fourth line or third pair, and into the
    /// replaced player's unit slots.
    /// </summary>
    private static (SetLineupCommand Lineup, PlayerId ReplacedId) DressInBottomUnit(
        SetLineupCommand lineup,
        PlayerSnapshot skater)
    {
        var line = lineup.ForwardLines[^1];
        var pair = lineup.DefencePairs[^1];
        var (dressed, replacedId) = skater.Position switch
        {
            Position.Wing => (
                lineup with { ForwardLines = [.. lineup.ForwardLines.SkipLast(1), line with { LeftWingId = skater.Id }] },
                line.LeftWingId),
            Position.Centre => (
                lineup with { ForwardLines = [.. lineup.ForwardLines.SkipLast(1), line with { CentreId = skater.Id }] },
                line.CentreId),
            _ => (
                lineup with { DefencePairs = [.. lineup.DefencePairs.SkipLast(1), pair with { LeftDefenceId = skater.Id }] },
                pair.LeftDefenceId),
        };
        return (dressed.ReplaceInUnits(replacedId, skater.Id), replacedId);
    }

    /// <summary>
    /// A complete textual description of every result, including each box score, so equal
    /// fingerprints mean identical results.
    /// </summary>
    internal static string Fingerprint(GameSnapshot snapshot) =>
        string.Join(
            Environment.NewLine,
            snapshot.Season.Results.Select(result =>
                $"{result.Date:yyyy-MM-dd} {result.Decision} {Side(result.Home)} @ {Side(result.Away)}"));

    // Box-score records print every statistic, and doubles print exactly, so equal fingerprints
    // mean every box-score value matches.
    private static string Side(CompletedMatchTeamSnapshot side) =>
        $"{side.TeamId}:{side.Score}/{side.Shots} "
        + $"[{string.Join(",", side.Skaters)}] "
        + $"G {side.Goalie}";

    /// <summary>
    /// Plays matches with the real engine but lets a test act before each one: fail it, or call
    /// back into the game while a day is in progress.
    /// </summary>
    private sealed class InterceptingSimulator : IMatchSimulator
    {
        private readonly MatchSimulator _engine = new();
        private int _calls;

        public Action<int>? BeforeMatch { get; set; }

        public MatchResult Simulate(Match match, OvertimeFormat overtime, RandomState randomState)
        {
            BeforeMatch?.Invoke(Interlocked.Increment(ref _calls));
            return _engine.Simulate(match, overtime, randomState);
        }
    }
}