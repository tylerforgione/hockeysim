using System.Globalization;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Lines;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Roster;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;

using Xunit;

namespace HockeySim.Desktop.Tests;

/// <summary>
/// Injuries as Desktop presents them. Injuries come from the real engine, so each test plays
/// seeded days until the managed team has the injury it needs.
/// </summary>
public sealed class InjuryDisplayTests
{
    // Far more days than any of these situations takes to arise with the test seed.
    private const int MaximumDays = 120;

    [Fact]
    public void ContinueWaitsUntilInjuredPlayersWhoCannotPlayAreReplaced()
    {
        var session = SessionWhereContinueIsBlocked();
        var shell = new GameShellViewModel(session);

        Assert.True(shell.HasPlayersToReplace);
        Assert.False(shell.AdvanceDayCommand.CanExecute(null));
        Assert.EndsWith("Injured players to replace", shell.ContinueDescription, StringComparison.Ordinal);
        Assert.All(session.Snapshot.PlayersToReplace, id =>
            Assert.Contains(PlayerDisplay.FullName(session.PlayersById[id]), shell.PlayersToReplaceMessage, StringComparison.Ordinal));

        shell.OpenLinesCommand.Execute(null);
        Assert.Equal(ShellPage.Lines, shell.CurrentPageKind);

        session.SetLineup(InjuredPlayerReplacement.ReplacingInjured(session.Snapshot));

        Assert.False(shell.HasPlayersToReplace);
        Assert.Empty(shell.PlayersToReplaceMessage);
        Assert.True(shell.AdvanceDayCommand.CanExecute(null));
    }

    [Fact]
    public void TheLinesPageWarnsUnderASlotHoldingAPlayerWhoCannotPlayAndMarksThemInTheChoices()
    {
        var session = SessionWhereContinueIsBlocked();
        var lines = new LinesPageViewModel(session);
        var outId = session.Snapshot.PlayersToReplace[0];

        var slot = AllDressedSlots(lines.Lineup).First(candidate => candidate.SelectedPlayer?.Id == outId);
        Assert.Equal("Injured · cannot play", slot.InjuryNote);
        Assert.True(slot.IsOutNote);
        var option = slot.Options.Single(candidate => candidate.Id == outId);
        Assert.Equal("OUT", option.InjuryMarker);
        Assert.Contains("expected back", option.InjurySummary, StringComparison.Ordinal);

        Assert.All(
            AllDressedSlots(lines.Lineup).Where(candidate => candidate.SelectedPlayer is { HasInjury: false }),
            healthy => Assert.Null(healthy.InjuryNote));
    }

    [Fact]
    public void RostersMarkInjuredPlayersAndListThemInTheInjuryReport()
    {
        var session = SessionWhereContinueIsBlocked();
        var roster = new TeamRosterViewModel(session, session.ManagedTeam.Id);
        var injured = session.ManagedTeam.Roster.Where(player => player.Injuries.Count > 0).ToList();

        Assert.True(roster.HasInjuries);
        Assert.Equal(injured.Sum(player => player.Injuries.Count), roster.InjuryReport.Count);
        Assert.Equal(
            roster.InjuryReport.OrderBy(row => !row.IsOut).Select(row => row.Name),
            roster.InjuryReport.Select(row => row.Name));

        var rows = roster.Skaters.Concat(roster.Goalies).ToList();
        foreach (var player in session.ManagedTeam.Roster)
        {
            var row = rows.Single(candidate => candidate.Player.Id == player.Id);
            Assert.Equal(player.Injuries.Count == 0 ? null : player.CanPlay ? "INJ" : "OUT", row.InjuryMarker);
            Assert.Equal(!player.CanPlay, row.IsOut);
        }

        var outPlayer = session.ManagedTeam.Roster.First(player => !player.CanPlay);
        var injury = outPlayer.Injuries.First(candidate => !candidate.CanPlayThrough);
        var reportRow = roster.InjuryReport.First(row => row.Name == PlayerDisplay.FullName(outPlayer) && row.IsOut);
        Assert.Equal(InjuryDisplay.Name(injury.Type), reportRow.Injury);
        Assert.Equal("Out", reportRow.Status);
        Assert.Equal(InjuryDisplay.ExpectedReturn(injury.ExpectedReturn), reportRow.ExpectedReturn);
    }

    [Fact]
    public void AHealthyTeamHasAnEmptyInjuryReport()
    {
        var session = GameTestData.StartSession();
        var roster = new TeamRosterViewModel(session, session.ManagedTeam.Id);

        Assert.False(roster.HasInjuries);
        Assert.All(roster.Skaters.Concat(roster.Goalies), row => Assert.False(row.HasInjury));
        Assert.Equal("Healthy", roster.SelectedPlayer!.Health);
        Assert.False(roster.SelectedPlayer.HasInjuries);
    }

    [Fact]
    public void ThePlayerProfileShowsEachInjuryWithItsExpectedReturnAndEffect()
    {
        var session = SessionWhereContinueIsBlocked();
        var outPlayer = session.ManagedTeam.Roster.First(player => !player.CanPlay);
        var roster = new TeamRosterViewModel(session, session.ManagedTeam.Id, outPlayer.Id);
        var profile = roster.SelectedPlayer!;

        Assert.Equal("Out injured", profile.Health);
        Assert.True(profile.IsOut);
        Assert.Equal(outPlayer.Injuries.Count, profile.Injuries.Count);
        var outInjury = profile.Injuries.First(injury => injury.IsOut);
        Assert.Equal("Cannot play until healed", outInjury.Effect);
        Assert.StartsWith("Expected back ", outInjury.ExpectedReturn, StringComparison.Ordinal);
    }

    [Fact]
    public void APlayThroughInjuryShowsTheRatingsItReduces()
    {
        var injury = new InjurySnapshot(
            InjuryType.SprainedAnkle,
            BodyPart.Ankle,
            CanPlayThrough: true,
            new DateOnly(2026, 10, 1),
            new ExpectedReturn(new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 9)));

        var line = new PlayerInjuryViewModel(injury);

        Assert.Equal("Sprained ankle", line.Name);
        Assert.Equal("Playing through", line.Status);
        Assert.False(line.IsOut);
        Assert.Equal("Plays at −5 Skating, −3 Positioning", line.Effect);
    }

    [Fact]
    public void TheExpectedReturnIsADateRangeOrASingleDate()
    {
        var earliest = new DateOnly(2026, 10, 7);
        var latest = earliest.AddDays(2);
        static string Short(DateOnly date) => date.ToString("MMM d", CultureInfo.CurrentCulture);

        Assert.Equal($"{Short(earliest)} – {Short(latest)}", InjuryDisplay.ExpectedReturn(new ExpectedReturn(earliest, latest)));
        Assert.Equal(Short(earliest), InjuryDisplay.ExpectedReturn(new ExpectedReturn(earliest, earliest)));
    }

    /// <summary>
    /// A session on a day the managed team plays with a dressed player who cannot play, as the
    /// user would find it after an injury.
    /// </summary>
    private static GameSession SessionWhereContinueIsBlocked()
    {
        var manager = new GameManager();
        GameTestData.StartSession(manager);
        for (var day = 0; day < MaximumDays; day++)
        {
            if (manager.GetSnapshot().PlayersToReplace.Count > 0)
            {
                return new GameSession(manager, "Injuries");
            }

            manager.AdvanceDay();
        }

        throw new InvalidOperationException("The managed team never dressed a player who could not play.");
    }

    private static IEnumerable<LineupSlotViewModel> AllDressedSlots(LineupEditorViewModel lineup) =>
        lineup.ForwardLines.SelectMany(line => new[] { line.LeftWing, line.Centre, line.RightWing })
            .Concat(lineup.DefencePairs.SelectMany(pair => new[] { pair.LeftDefence, pair.RightDefence }))
            .Append(lineup.StartingGoalie)
            .Append(lineup.BackupGoalie);
}