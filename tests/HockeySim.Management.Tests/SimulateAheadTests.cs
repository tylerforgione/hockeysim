using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Lineups;
using HockeySim.Management.Saves;
using HockeySim.Management.SeasonPlay;
using HockeySim.Simulation;
using HockeySim.Simulation.Randomness;

using Xunit;

using static HockeySim.Management.Tests.SeasonAdvancementTests;

namespace HockeySim.Management.Tests;

/// <summary>
/// Simulating ahead. Stopping days are checked with an engine that injures nobody, so no early
/// stop for an injury interrupts the target; the early stops for injuries use the real engine and
/// are compared with the same game played one day at a time.
/// </summary>
public sealed class SimulateAheadTests(SimulateAheadTests.SeasonSaves saves) : IClassFixture<SimulateAheadTests.SeasonSaves>
{
    // Far more days than an injury takes to arise with the seed used, and the window the injury
    // stops are compared over.
    private const int MaximumDays = 120;

    [Fact]
    public void EachPhaseOffersItsOwnTargets()
    {
        Assert.Equal(
            new[] { SimulationTarget.NextDay, SimulationTarget.NextMatch, SimulationTarget.EndOfPreseason },
            SimulationTargets.AvailableIn(SeasonPhase.Preseason));
        Assert.Equal(
            new[] { SimulationTarget.NextDay, SimulationTarget.NextMatch, SimulationTarget.OneWeek, SimulationTarget.OneMonth, SimulationTarget.EndOfRegularSeason },
            SimulationTargets.AvailableIn(SeasonPhase.RegularSeason));
        Assert.Equal(
            new[] { SimulationTarget.NextDay, SimulationTarget.NextMatch, SimulationTarget.EndOfRound, SimulationTarget.EndOfPlayoffs },
            SimulationTargets.AvailableIn(SeasonPhase.Playoffs));
    }

    [Fact]
    public void ATargetTheCurrentPhaseDoesNotOfferIsRejectedWithoutPlaying()
    {
        var manager = InjuryFreeManager();
        var before = StartGame(manager);

        Assert.Throws<ArgumentException>(() => manager.SimulateAhead(SimulationTarget.OneWeek, cancellationToken: Token));
        Assert.Throws<ArgumentException>(() => manager.SimulateAhead(SimulationTarget.EndOfRound, cancellationToken: Token));

        Assert.Equal(before.Season.CurrentDate, manager.GetSnapshot().Season.CurrentDate);
    }

    [Fact]
    public void SimulatingAheadBeforeAGameStartsOrAfterTheSeasonIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new GameManager().SimulateAhead(SimulationTarget.NextDay, cancellationToken: Token));

        var manager = Load(saves.Playoffs);
        manager.SimulateAhead(SimulationTarget.EndOfPlayoffs, cancellationToken: Token);
        Assert.Throws<InvalidOperationException>(() => manager.SimulateAhead(SimulationTarget.NextDay, cancellationToken: Token));
    }

    [Fact]
    public void NextDayPlaysOneDay()
    {
        var manager = InjuryFreeManager();
        var start = StartGame(manager);

        var result = manager.SimulateAhead(SimulationTarget.NextDay, cancellationToken: Token);

        Assert.Equal(SimulationStop.TargetReached, result.Stop);
        Assert.Equal(1, result.DaysPlayed);
        Assert.Equal(start.Season.CurrentDate.AddDays(1), result.Snapshot.Season.CurrentDate);
        Assert.Equal(16, result.Snapshot.Season.PreseasonResults.Count);
    }

    [Fact]
    public void NextMatchStopsOnTheManagedTeamsNextMatchDayWithoutPlayingIt()
    {
        var manager = InjuryFreeManager();
        var start = StartGame(manager);
        var expected = ManagedMatchDates(start.Schedule.PreseasonMatches, start.ManagedTeamId)
            .First(date => date > start.Season.CurrentDate);

        var result = manager.SimulateAhead(SimulationTarget.NextMatch, cancellationToken: Token);

        Assert.Equal(SimulationStop.TargetReached, result.Stop);
        Assert.Equal(expected, result.Snapshot.Season.CurrentDate);
        Assert.DoesNotContain(result.Snapshot.Season.PreseasonResults, match => match.Date == expected);
        Assert.Equal(expected.DayNumber - start.Season.CurrentDate.DayNumber, result.DaysPlayed);
    }

    [Fact]
    public void EndOfPreseasonStopsOnOpeningDay()
    {
        var manager = InjuryFreeManager();
        var start = StartGame(manager);

        var result = manager.SimulateAhead(SimulationTarget.EndOfPreseason, cancellationToken: Token);

        Assert.Equal(SimulationStop.TargetReached, result.Stop);
        Assert.Equal(start.Schedule.Matches[0].Date, result.Snapshot.Season.CurrentDate);
        Assert.Equal(start.Schedule.PreseasonMatches[^1].Date.AddDays(1), result.Snapshot.Season.CurrentDate);
        Assert.Equal(SeasonPhase.RegularSeason, result.Snapshot.Season.Phase);
        Assert.Equal(start.Schedule.PreseasonMatches.Count, result.Snapshot.Season.PreseasonResults.Count);
    }

    [Theory]
    [InlineData(SimulationTarget.OneWeek, 7)]
    [InlineData(SimulationTarget.OneMonth, 31)]
    public void AWeekOrAMonthStopsThatManyDaysLater(SimulationTarget target, int days)
    {
        var manager = Load(saves.OpeningDay);

        var result = manager.SimulateAhead(target, cancellationToken: Token);

        Assert.Equal(SimulationStop.TargetReached, result.Stop);
        Assert.Equal(days, result.DaysPlayed);
        Assert.Equal(new DateOnly(2026, 10, 1).AddDays(days), result.Snapshot.Season.CurrentDate);
    }

    [Fact]
    public void EndOfRegularSeasonStopsTheDayAfterItsLastMatchWithThePlayoffsSeeded()
    {
        var result = saves.RegularSeasonResult;

        Assert.Equal(SimulationStop.TargetReached, result.Stop);
        Assert.Equal(result.Snapshot.Schedule.Matches[^1].Date.AddDays(1), result.Snapshot.Season.CurrentDate);
        Assert.Equal(SeasonPhase.Playoffs, result.Snapshot.Season.Phase);
        Assert.Equal(1344, result.Snapshot.Season.Results.Count);
        Assert.NotNull(result.Snapshot.Season.Playoffs);
    }

    [Fact]
    public void AMonthThatWouldRunIntoThePlayoffsStopsWhenThePhaseChanges()
    {
        var manager = Load(saves.LateRegularSeason);
        var lastDay = manager.GetSnapshot().Schedule.Matches[^1].Date;

        var result = manager.SimulateAhead(SimulationTarget.OneMonth, cancellationToken: Token);

        Assert.Equal(SimulationStop.PhaseChanged, result.Stop);
        Assert.Equal(lastDay.AddDays(1), result.Snapshot.Season.CurrentDate);
        Assert.Equal(SeasonPhase.Playoffs, result.Snapshot.Season.Phase);
    }

    [Fact]
    public void NextMatchFromTheLastPreseasonMatchStopsWhenTheRegularSeasonStarts()
    {
        var manager = InjuryFreeManager();
        var start = StartGame(manager);
        var lastPreseasonDay = start.Schedule.PreseasonMatches[^1].Date;
        while (manager.GetSnapshot().Season.CurrentDate < lastPreseasonDay)
        {
            manager.AdvanceDay();
        }

        var result = manager.SimulateAhead(SimulationTarget.NextMatch, cancellationToken: Token);

        // Every team plays on opening day, so the next match and the new phase arrive together; the
        // target is reported.
        Assert.Equal(SimulationStop.TargetReached, result.Stop);
        Assert.Equal(1, result.DaysPlayed);
        Assert.Equal(SeasonPhase.RegularSeason, result.Snapshot.Season.Phase);
    }

    [Fact]
    public void EndOfRoundStopsTheDayAfterTheRoundsLastGame()
    {
        var manager = Load(saves.Playoffs);

        var result = manager.SimulateAhead(SimulationTarget.EndOfRound, cancellationToken: Token);

        var playoffs = result.Snapshot.Season.Playoffs!;
        var firstRound = playoffs.Series.Where(series => series.Round == PlayoffRound.FirstRound).ToList();
        Assert.Equal(SimulationStop.TargetReached, result.Stop);
        Assert.Equal(PlayoffRound.SecondRound, playoffs.CurrentRound);
        Assert.All(firstRound, series => Assert.NotNull(series.WinnerId));
        Assert.Equal(firstRound.SelectMany(series => series.Games).Max(game => game.Date).AddDays(1), result.Snapshot.Season.CurrentDate);
        Assert.DoesNotContain(playoffs.Results, game => game.Date >= result.Snapshot.Season.CurrentDate);
    }

    [Fact]
    public void EndOfPlayoffsStopsTheDayAfterTheFinalsDecidingGame()
    {
        var manager = Load(saves.Playoffs);

        var result = manager.SimulateAhead(SimulationTarget.EndOfPlayoffs, cancellationToken: Token);

        var season = result.Snapshot.Season;
        Assert.Equal(SimulationStop.TargetReached, result.Stop);
        Assert.True(season.IsComplete);
        Assert.NotNull(season.Playoffs!.ChampionId);
        Assert.Equal(season.Playoffs.Results[^1].Date.AddDays(1), season.CurrentDate);
    }

    [Fact]
    public void NextMatchInThePlayoffsStopsOnTheManagedTeamsNextGame()
    {
        var manager = Load(saves.Playoffs);
        var series = manager.GetSnapshot().Season.Playoffs!.Series[0];
        manager.SelectManagedTeam(series.HigherRanked.TeamId);
        manager.AdvanceDay();

        var result = manager.SimulateAhead(SimulationTarget.NextMatch, cancellationToken: Token);

        var nextGame = result.Snapshot.Season.Playoffs!.Series[0].NextGame;
        Assert.Equal(SimulationStop.TargetReached, result.Stop);
        Assert.NotNull(nextGame);
        Assert.Equal(nextGame.Date, result.Snapshot.Season.CurrentDate);
    }

    [Fact]
    public void ATeamWithNoPlayoffGamesAheadRunsToTheEndOfTheSeason()
    {
        var manager = Load(saves.Playoffs);
        var playoffs = manager.GetSnapshot().Season.Playoffs!;
        var qualifiers = playoffs.Series.SelectMany(series => new[] { series.HigherRanked.TeamId, series.LowerRanked.TeamId }).ToHashSet();
        manager.SelectManagedTeam(manager.GetSnapshot().League.Teams.First(team => !qualifiers.Contains(team.Id)).Id);

        var result = manager.SimulateAhead(SimulationTarget.NextMatch, cancellationToken: Token);

        Assert.Equal(SimulationStop.SeasonComplete, result.Stop);
        Assert.True(result.Snapshot.Season.IsComplete);
    }

    [Fact]
    public void ARunPlaysExactlyWhatPlayingOneDayAtATimeWould()
    {
        var reference = Load(saves.OpeningDay);
        for (var day = 0; day < 31; day++)
        {
            reference.AdvanceDay();
        }

        var manager = Load(saves.OpeningDay);
        var result = manager.SimulateAhead(SimulationTarget.OneMonth, cancellationToken: Token);

        var expected = reference.GetSnapshot();
        Assert.Equal(Fingerprint(expected), Fingerprint(result.Snapshot));
        Assert.Equal(expected.RandomState, result.Snapshot.RandomState);
        Assert.Equal(expected.Inbox.Select(message => message.Subject), result.Snapshot.Inbox.Select(message => message.Subject));
    }

    [Fact]
    public void ProgressIsReportedAfterEachDayButTheLast()
    {
        var manager = Load(saves.OpeningDay);
        var progress = new RecordingProgress();

        manager.SimulateAhead(SimulationTarget.OneWeek, progress, Token);

        Assert.Equal(Enumerable.Range(1, 6).Select(day => new DateOnly(2026, 10, 1).AddDays(day)), progress.Dates);
    }

    [Fact]
    public void ACancelledRunStopsBeforeItsNextDay()
    {
        var manager = Load(saves.OpeningDay);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var progress = new RecordingProgress(date =>
        {
            if (date == new DateOnly(2026, 10, 4))
            {
                cancellation.Cancel();
            }
        });

        var result = manager.SimulateAhead(SimulationTarget.OneMonth, progress, cancellation.Token);

        Assert.Equal(SimulationStop.Cancelled, result.Stop);
        Assert.Equal(3, result.DaysPlayed);
        Assert.Equal(new DateOnly(2026, 10, 4), result.Snapshot.Season.CurrentDate);
    }

    [Fact]
    public void ARunCancelledBeforeItStartsPlaysNothing()
    {
        var manager = Load(saves.OpeningDay);
        var before = manager.GetSnapshot();

        var result = manager.SimulateAhead(SimulationTarget.OneWeek, cancellationToken: new CancellationToken(canceled: true));

        Assert.Equal(SimulationStop.Cancelled, result.Stop);
        Assert.Equal(0, result.DaysPlayed);
        Assert.Equal(before.Season.CurrentDate, result.Snapshot.Season.CurrentDate);
        Assert.Equal(before.RandomState, result.Snapshot.RandomState);
    }

    [Fact]
    public void AFailurePartWayKeepsTheCompleteDaysBeforeIt()
    {
        var simulator = new FailingSimulator(failingCall: 40);
        var manager = new GameManager(simulator);
        manager.LoadGame(new MemorySaveStore(saves.OpeningDay));

        var error = Assert.Throws<InvalidOperationException>(() => manager.SimulateAhead(SimulationTarget.OneMonth, cancellationToken: Token));

        // Match days alternate with empty days, 16 matches each, so call 40 is on the third match day.
        Assert.Equal(FailingSimulator.Message, error.Message);
        var after = manager.GetSnapshot();
        Assert.Equal(new DateOnly(2026, 10, 5), after.Season.CurrentDate);
        Assert.Equal(32, after.Season.Results.Count);

        var reference = Load(saves.OpeningDay);
        for (var day = 0; day < 4; day++)
        {
            reference.AdvanceDay();
        }

        Assert.Equal(Fingerprint(reference.GetSnapshot()), Fingerprint(after));
        Assert.Equal(reference.GetSnapshot().RandomState, after.RandomState);
    }

    [Fact]
    public void RunsStopAfterEachDayAManagedPlayerIsInjuredOutButNotForKnocks()
    {
        // Runs to the end of the regular season, replacing injured players when a run stops for
        // them, until the window has passed.
        var limit = new DateOnly(2026, 10, 1).AddDays(MaximumDays);
        var manager = new GameManager();
        StartAtOpeningDay(manager);
        var injuryStops = new List<DateOnly>();
        while (manager.GetSnapshot().Season.CurrentDate < limit)
        {
            var result = manager.SimulateAhead(SimulationTarget.EndOfRegularSeason, cancellationToken: Token);
            if (result.Stop == SimulationStop.ManagedPlayerInjured)
            {
                injuryStops.Add(result.Snapshot.Season.CurrentDate.AddDays(-1));
            }
            else
            {
                Assert.Equal(SimulationStop.PlayersToReplace, result.Stop);
                manager.SetLineup(InjuredPlayerReplacement.ReplacingInjured(result.Snapshot));
            }
        }

        // The same game played a day at a time, replacing the same players at the same points.
        var reference = new GameManager();
        StartAtOpeningDay(reference);
        var injuredOutDays = new List<DateOnly>();
        var knockDays = new List<DateOnly>();
        while (reference.GetSnapshot().Season.CurrentDate < manager.GetSnapshot().Season.CurrentDate)
        {
            var playedDate = reference.GetSnapshot().Season.CurrentDate;
            var injuries = ManagedTeam(reference.AdvanceDayReplacingInjured()).Roster
                .SelectMany(player => player.Injuries)
                .Where(injury => injury.Date == playedDate)
                .ToList();
            if (injuries.Any(injury => !injury.CanPlayThrough))
            {
                injuredOutDays.Add(playedDate);
            }
            else if (injuries.Count > 0)
            {
                knockDays.Add(playedDate);
            }
        }

        Assert.Equal(Fingerprint(reference.GetSnapshot()), Fingerprint(manager.GetSnapshot()));
        Assert.NotEmpty(injuryStops);
        Assert.Equal(injuredOutDays, injuryStops);
        Assert.NotEmpty(knockDays);
    }

    [Fact]
    public void ARunStopsWhenTheManagedTeamPlaysTodayWithPlayersToReplace()
    {
        var manager = new GameManager();
        StartAtOpeningDay(manager);
        SimulationResult result;
        var days = 0;
        do
        {
            result = manager.SimulateAhead(SimulationTarget.EndOfRegularSeason, cancellationToken: Token);
            days += result.DaysPlayed;
        }
        while (result.Stop == SimulationStop.ManagedPlayerInjured && days < MaximumDays);

        Assert.Equal(SimulationStop.PlayersToReplace, result.Stop);
        Assert.NotEmpty(result.Snapshot.PlayersToReplace);
        Assert.DoesNotContain(result.Snapshot.Season.Results, match => match.Date == result.Snapshot.Season.CurrentDate);

        // Today's match waits for the lineup, so the next run cannot start until it is changed.
        Assert.Throws<UnavailablePlayersException>(() => manager.SimulateAhead(SimulationTarget.NextDay, cancellationToken: Token));
        manager.SetLineup(InjuredPlayerReplacement.ReplacingInjured(result.Snapshot));
        Assert.Equal(1, manager.SimulateAhead(SimulationTarget.NextDay, cancellationToken: Token).DaysPlayed);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IEnumerable<DateOnly> ManagedMatchDates(IEnumerable<ScheduledMatchSnapshot> matches, TeamId teamId) =>
        matches.Where(match => match.HomeTeamId == teamId || match.AwayTeamId == teamId).Select(match => match.Date);

    private static GameManager InjuryFreeManager() => new(new InjuryFreeSimulator());

    private static GameManager Load(GameSave save)
    {
        var manager = InjuryFreeManager();
        manager.LoadGame(new MemorySaveStore(save));
        return manager;
    }

    /// <summary>
    /// Saves of one injury-free season at the points the tests start from, played once and shared
    /// because a whole regular season is slow to play.
    /// </summary>
    public sealed class SeasonSaves
    {
        public SeasonSaves()
        {
            var manager = InjuryFreeManager();
            StartGame(manager);
            manager.SimulateAhead(SimulationTarget.EndOfPreseason, cancellationToken: Token);
            OpeningDay = Save(manager);

            var lastDay = manager.GetSnapshot().Schedule.Matches[^1].Date;
            while (manager.GetSnapshot().Season.CurrentDate < lastDay.AddDays(-3))
            {
                manager.AdvanceDay();
            }

            LateRegularSeason = Save(manager);
            RegularSeasonResult = manager.SimulateAhead(SimulationTarget.EndOfRegularSeason, cancellationToken: Token);
            Playoffs = Save(manager);
        }

        public GameSave OpeningDay { get; }

        /// <summary>Three days before the final regular-season day.</summary>
        public GameSave LateRegularSeason { get; }

        /// <summary>The run from <see cref="LateRegularSeason"/> to the end of the regular season.</summary>
        public SimulationResult RegularSeasonResult { get; }

        /// <summary>The day after the final regular-season day, before the first playoff game.</summary>
        public GameSave Playoffs { get; }

        private static GameSave Save(GameManager manager)
        {
            var store = new MemorySaveStore();
            manager.SaveGame(store);
            return store.Saved!;
        }
    }

    /// <summary>The real engine with injuries and wear switched off, as in the preseason.</summary>
    private sealed class InjuryFreeSimulator : IMatchSimulator
    {
        private readonly MatchSimulator _engine = new();

        public MatchResult Simulate(Match match, OvertimeFormat overtime, MatchHealth health, RandomState randomState)
        {
            var players = match.Home.Roster.Concat(match.Away.Roster).Select(player => health.For(player.Id));
            return _engine.Simulate(match, overtime, new MatchHealth(health.Date, players, injuriesPossible: false), randomState);
        }
    }

    private sealed class FailingSimulator(int failingCall) : IMatchSimulator
    {
        public const string Message = "Engine failure.";

        private readonly IMatchSimulator _engine = new InjuryFreeSimulator();
        private int _calls;

        public MatchResult Simulate(Match match, OvertimeFormat overtime, MatchHealth health, RandomState randomState) =>
            ++_calls == failingCall
                ? throw new InvalidOperationException(Message)
                : _engine.Simulate(match, overtime, health, randomState);
    }

    /// <summary>Records each report as it is made, on the run's own thread.</summary>
    private sealed class RecordingProgress(Action<DateOnly>? onReport = null) : IProgress<DateOnly>
    {
        public List<DateOnly> Dates { get; } = [];

        public void Report(DateOnly value)
        {
            Dates.Add(value);
            onReport?.Invoke(value);
        }
    }
}