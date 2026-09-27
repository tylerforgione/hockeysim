using HockeySim.Desktop.Lines;
using HockeySim.Management.GameManagement;
using HockeySim.Management.NewGame;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class LinesPageViewModelTests
{
    [Fact]
    public void LoadsTheManagedLineupWithPositionEligibleChoices()
    {
        var session = GameTestData.StartSession();
        var lines = new LinesPageViewModel(session);
        var lineup = session.ManagedTeam.Lineup;

        Assert.Equal(4, lines.ForwardLines.Count);
        Assert.Equal(3, lines.DefencePairs.Count);
        Assert.Equal(lineup.ForwardLines[0].CentreId, lines.ForwardLines[0].Centre.SelectedPlayer?.Id);
        Assert.Equal(lineup.StartingGoalieId, lines.StartingGoalie?.SelectedPlayer?.Id);
        Assert.Equal(5, lines.ForwardLines[0].Centre.Options.Count);
        Assert.Equal(8, lines.ForwardLines[0].LeftWing.Options.Count);
        Assert.Equal(7, lines.DefencePairs[0].LeftDefence.Options.Count);
        Assert.Equal(3, lines.StartingGoalie!.Options.Count);
        Assert.Equal(
            session.ManagedTeam.ScratchedPlayerIds.Select(id => id.Value).Order(),
            lines.Scratches.Select(player => player.Id.Value).Order());
        Assert.False(lines.HasChanges);
        Assert.False(lines.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void ChoosingADressedPlayerSwapsTheTwoSlots()
    {
        var lines = new LinesPageViewModel(GameTestData.StartSession());
        var firstLeftWing = lines.ForwardLines[0].LeftWing.SelectedPlayer;
        var fourthRightWing = lines.ForwardLines[3].RightWing.SelectedPlayer;

        lines.ForwardLines[0].LeftWing.SelectedPlayer = fourthRightWing;

        Assert.Equal(fourthRightWing, lines.ForwardLines[0].LeftWing.SelectedPlayer);
        Assert.Equal(firstLeftWing, lines.ForwardLines[3].RightWing.SelectedPlayer);
        Assert.True(lines.HasChanges);
        Assert.True(lines.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void ChoosingAScratchedPlayerMovesTheReplacedPlayerToScratches()
    {
        var lines = new LinesPageViewModel(GameTestData.StartSession());
        var scratchedCentre = lines.Scratches.Single(player => player.PositionAbbreviation == "C");
        var replacedCentre = lines.ForwardLines[1].Centre.SelectedPlayer!;

        lines.ForwardLines[1].Centre.SelectedPlayer = scratchedCentre;

        Assert.Contains(replacedCentre, lines.Scratches);
        Assert.DoesNotContain(scratchedCentre, lines.Scratches);
        Assert.Equal(3, lines.Scratches.Count);
    }

    [Fact]
    public void SavingAppliesTheLineupThroughManagement()
    {
        var session = GameTestData.StartSession();
        var lines = new LinesPageViewModel(session);
        var scratchedGoalie = lines.Scratches.Single(player => player.PositionAbbreviation == "G");
        lines.StartingGoalie!.SelectedPlayer = scratchedGoalie;

        lines.SaveCommand.Execute(null);

        Assert.Equal(scratchedGoalie.Id, session.ManagedTeam.Lineup.StartingGoalieId);
        Assert.False(lines.HasChanges);
        Assert.False(lines.HasError);
        Assert.Equal("Lineup saved.", lines.StatusMessage);
    }

    [Fact]
    public void RevertingRestoresTheSavedLineup()
    {
        var session = GameTestData.StartSession();
        var lines = new LinesPageViewModel(session);
        var original = lines.DefencePairs[0].LeftDefence.SelectedPlayer!.Id;
        lines.DefencePairs[0].LeftDefence.SelectedPlayer = lines.DefencePairs[2].RightDefence.SelectedPlayer;

        lines.RevertCommand.Execute(null);

        Assert.Equal(original, lines.DefencePairs[0].LeftDefence.SelectedPlayer?.Id);
        Assert.False(lines.HasChanges);
    }

    [Fact]
    public void RejectedLineupShowsTheValidationFailureAndKeepsTheEdits()
    {
        var gameManager = new GameManager();
        var session = GameTestData.StartSession(gameManager);
        var lines = new LinesPageViewModel(session);
        lines.ForwardLines[0].LeftWing.SelectedPlayer = lines.ForwardLines[1].LeftWing.SelectedPlayer;

        // Replacing the world behind the editor makes every selected player foreign to the roster.
        gameManager.StartNewGame(new NewGameCommand(2026, new RandomState(1), GameTestData.ManagedTeamName));
        lines.SaveCommand.Execute(null);

        Assert.True(lines.HasError);
        Assert.Contains("does not belong to the managed team's roster", lines.ErrorMessage);
        Assert.True(lines.HasChanges);
    }

    [Fact]
    public void UnrelatedGameChangesKeepUnsavedEdits()
    {
        var session = GameTestData.StartSession();
        var lines = new LinesPageViewModel(session);
        var moved = lines.ForwardLines[2].RightWing.SelectedPlayer;
        lines.ForwardLines[0].RightWing.SelectedPlayer = moved;

        session.MarkInboxMessageRead(session.Snapshot.Inbox[0].Id);
        lines.Refresh();

        Assert.Equal(moved, lines.ForwardLines[0].RightWing.SelectedPlayer);
        Assert.True(lines.HasChanges);
    }
}