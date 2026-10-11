using HockeySim.Desktop.Game;
using HockeySim.Desktop.Schedule;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.SeasonPlay;

using Xunit;

namespace HockeySim.Desktop.Tests;

/// <summary>Continue's menu of runs, their progress, Stop, and how a run's end is shown.</summary>
public sealed class SimulateAheadTests
{
    private static readonly DateOnly OpeningDay = new(2026, 10, 1);

    [Fact]
    public async Task TheMenuOffersTheCurrentPhasesOptions()
    {
        var session = GameTestData.StartPreseasonSession();
        var shell = new GameShellViewModel(session);

        Assert.Equal(["Next day", "To the next match", "To the end of the preseason"], shell.SimulateAheadOptions.Select(option => option.Label));
        Assert.True(shell.CanOpenSimulateAhead);
        Assert.All(shell.SimulateAheadOptions, option => Assert.True(option.Command.CanExecute(option.Target)));

        await shell.SimulateAheadCommand.ExecuteAsync(SimulationTarget.EndOfPreseason);

        Assert.Equal(OpeningDay, session.Snapshot.Season.CurrentDate);
        var days = OpeningDay.DayNumber - session.Snapshot.Schedule.PreseasonMatches[0].Date.DayNumber;
        Assert.Equal($"Simulated {days} days, to {MatchDisplay.ShortDate(OpeningDay)}.", shell.SimulationSummary);
        Assert.Equal(
            ["Next day", "To the next match", "One week", "One month", "To the playoffs"],
            shell.SimulateAheadOptions.Select(option => option.Label));
        Assert.False(shell.SimulateAheadCommand.CanExecute(SimulationTarget.EndOfPreseason));
    }

    [Fact]
    public async Task ARunPublishesOneSnapshotWhenItEndsAndSaysHowFarItWent()
    {
        // Nobody is injured in the preseason, so the run reaches its target.
        var manager = new GameManager();
        GameTestData.StartPreseasonSession(manager);
        var session = new GameSession(manager, "Test Career", isSaved: true);
        var shell = new GameShellViewModel(session);
        var nextMatchDay = session.Snapshot.Season.CurrentDate.AddDays(2);
        var publications = 0;
        session.PropertyChanged += (_, e) => publications += e.PropertyName == nameof(GameSession.Snapshot) ? 1 : 0;

        await shell.SimulateAheadCommand.ExecuteAsync(SimulationTarget.NextMatch);

        Assert.Equal(1, publications);
        Assert.Null(shell.AdvanceError);
        Assert.Equal(nextMatchDay, session.Snapshot.Season.CurrentDate);
        Assert.Equal(16, session.Snapshot.Season.PreseasonResults.Count);
        Assert.Equal($"{MatchDisplay.ShortDate(nextMatchDay)} · 16 preseason matches, including yours", shell.ContinueDescription);
        Assert.Equal($"Simulated 2 days, to {MatchDisplay.ShortDate(nextMatchDay)}.", shell.SimulationSummary);
        Assert.True(session.HasUnsavedChanges);
        Assert.Equal("Unsaved changes", shell.SaveStatus);

        shell.DismissSimulationSummaryCommand.Execute(null);

        Assert.False(shell.HasSimulationSummary);
    }

    [Fact]
    public async Task StopEndsARunAfterTheDayBeingPlayed()
    {
        using var engine = new GatedMatchSimulator();
        var session = GameTestData.StartPreseasonSession(new GameManager(engine));
        var shell = new GameShellViewModel(session);
        var firstDay = session.Snapshot.Season.CurrentDate;

        var running = shell.SimulateAheadCommand.ExecuteAsync(SimulationTarget.EndOfPreseason);
        await engine.WaitUntilPlayingAsync();

        Assert.True(shell.IsSimulatingAhead);
        Assert.Equal("Simulating…", shell.ContinueLabel);
        Assert.False(shell.AdvanceDayCommand.CanExecute(null));
        Assert.False(shell.CanOpenSimulateAhead);
        Assert.False(shell.SaveGameCommand.CanExecute(null));
        Assert.False(shell.ShowMainMenuCommand.CanExecute(null));
        Assert.True(shell.StopSimulatingCommand.CanExecute(null));

        shell.StopSimulatingCommand.Execute(null);
        engine.Release();
        await running;

        Assert.False(shell.IsSimulatingAhead);
        Assert.False(shell.StopSimulatingCommand.CanExecute(null));
        Assert.Equal("Continue", shell.ContinueLabel);
        Assert.Equal(firstDay.AddDays(1), session.Snapshot.Season.CurrentDate);
        Assert.Equal(16, session.Snapshot.Season.PreseasonResults.Count);
        Assert.Equal($"Simulated 1 day; stopped on {MatchDisplay.ShortDate(firstDay.AddDays(1))}.", shell.SimulationSummary);
        Assert.True(shell.AdvanceDayCommand.CanExecute(null));
    }

    [Fact]
    public async Task AFailurePartWayKeepsTheDaysPlayedAndNamesTheFailedDay()
    {
        // The first preseason day's 16 matches play; the third day's first match fails.
        var session = GameTestData.StartPreseasonSession(new GameManager(new FailingAfterMatchSimulator(16)));
        var shell = new GameShellViewModel(session);
        var failedDay = session.Snapshot.Season.CurrentDate.AddDays(2);

        await shell.SimulateAheadCommand.ExecuteAsync(SimulationTarget.EndOfPreseason);

        Assert.Equal(failedDay, session.Snapshot.Season.CurrentDate);
        Assert.Equal(16, session.Snapshot.Season.PreseasonResults.Count);
        Assert.Equal(
            $"Simulated 2 days; {MatchDisplay.ShortDate(failedDay)} could not be played, and no results from it were applied. {FailingMatchSimulator.FailureMessage}",
            shell.AdvanceError);
        Assert.False(shell.HasSimulationSummary);
        Assert.True(session.HasUnsavedChanges);
        Assert.True(shell.AdvanceDayCommand.CanExecute(null));
    }

    [Fact]
    public async Task ARunStoppedEarlySaysWhy()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        var start = session.Snapshot.Season.CurrentDate;

        await shell.SimulateAheadCommand.ExecuteAsync(SimulationTarget.EndOfRegularSeason);

        // The real engine injures one of the managed team's players long before the season ends.
        var days = session.Snapshot.Season.CurrentDate.DayNumber - start.DayNumber;
        Assert.Equal(SeasonPhase.RegularSeason, session.Snapshot.Season.Phase);
        Assert.Equal($"Simulated {(days == 1 ? "1 day" : $"{days} days")}; stopped because one of your players was injured.", shell.SimulationSummary);
    }
}