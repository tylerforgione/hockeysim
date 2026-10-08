using HockeySim.Desktop.Lines;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.NewGame;
using HockeySim.Simulation.Randomness;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class LinesPageViewModelTests
{
    [Fact]
    public void LoadsTheManagedLineupWithEverySkaterForSkaterSlotsAndGoaliesForGoalieSlots()
    {
        var session = GameTestData.StartSession();
        var lines = new LinesPageViewModel(session);
        var lineup = session.ManagedTeam.Lineup;

        Assert.Equal(4, lines.Lineup.ForwardLines.Count);
        Assert.Equal(3, lines.Lineup.DefencePairs.Count);
        Assert.Equal(lineup.ForwardLines[0].CentreId, lines.Lineup.ForwardLines[0].Centre.SelectedPlayer?.Id);
        Assert.Equal(lineup.StartingGoalieId, lines.Lineup.StartingGoalie.SelectedPlayer?.Id);
        Assert.Equal(20, lines.Lineup.ForwardLines[0].Centre.Options.Count);
        Assert.Equal(20, lines.Lineup.ForwardLines[0].LeftWing.Options.Count);
        Assert.Equal(20, lines.Lineup.DefencePairs[0].LeftDefence.Options.Count);
        Assert.DoesNotContain(lines.Lineup.ForwardLines[0].Centre.Options, option => option.Position == Position.Goalie);
        Assert.Equal(3, lines.Lineup.StartingGoalie.Options.Count);
        Assert.All(lines.Lineup.StartingGoalie.Options, option => Assert.Equal(Position.Goalie, option.Position));
        Assert.Equal(
            session.ManagedTeam.ScratchedPlayerIds.Select(id => id.Value).Order(),
            lines.Lineup.Scratches.Select(player => player.Id.Value).Order());
        Assert.False(lines.HasChanges);
        Assert.False(lines.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void ChoosingADressedPlayerSwapsTheTwoSlots()
    {
        var lines = new LinesPageViewModel(GameTestData.StartSession());
        var firstLeftWing = lines.Lineup.ForwardLines[0].LeftWing.SelectedPlayer;
        var fourthRightWing = lines.Lineup.ForwardLines[3].RightWing.SelectedPlayer;

        lines.Lineup.ForwardLines[0].LeftWing.SelectedPlayer = fourthRightWing;

        Assert.Equal(fourthRightWing, lines.Lineup.ForwardLines[0].LeftWing.SelectedPlayer);
        Assert.Equal(firstLeftWing, lines.Lineup.ForwardLines[3].RightWing.SelectedPlayer);
        Assert.True(lines.HasChanges);
        Assert.True(lines.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void ADefencemanCanBeSavedAtForwardAndAForwardOnDefence()
    {
        var session = GameTestData.StartSession();
        var lines = new LinesPageViewModel(session);
        var defenceman = lines.Lineup.DefencePairs[0].LeftDefence.SelectedPlayer!;
        var centre = lines.Lineup.ForwardLines[0].Centre.SelectedPlayer!;

        // Choosing a dressed player swaps them, so the centre moves to the defenceman's place.
        lines.Lineup.ForwardLines[0].Centre.SelectedPlayer = defenceman;
        lines.SaveCommand.Execute(null);

        Assert.False(lines.HasError);
        Assert.Equal(defenceman.Id, session.ManagedTeam.Lineup.ForwardLines[0].CentreId);
        Assert.Equal(centre.Id, session.ManagedTeam.Lineup.DefencePairs[0].LeftDefenceId);
    }

    [Fact]
    public void SlotsWarnOnlyWhenAPlayerIsOutOfPosition()
    {
        var session = GameTestData.StartSession();
        var lines = new LinesPageViewModel(session);
        var line = lines.Lineup.ForwardLines[0];
        var pair = lines.Lineup.DefencePairs[0];
        var options = line.LeftWing.Options;
        PlayerOptionViewModel Skater(Position position, Handedness handedness) =>
            options.First(option => option.Position == position
                && session.ManagedTeam.Roster.Single(player => player.Id == option.Id).Biography.Handedness == handedness);

        line.LeftWing.SelectedPlayer = Skater(Position.Wing, Handedness.Left);
        Assert.Null(line.LeftWing.FitNote);
        Assert.False(line.LeftWing.HasFitNote);

        // Handedness is left to the manager, so an off-hand wing gets no warning.
        line.LeftWing.SelectedPlayer = Skater(Position.Wing, Handedness.Right);
        Assert.Null(line.LeftWing.FitNote);

        line.LeftWing.SelectedPlayer = Skater(Position.Centre, Handedness.Left);
        Assert.Equal("Out of position", line.LeftWing.FitNote);

        line.LeftWing.SelectedPlayer = Skater(Position.Defence, Handedness.Right);
        Assert.Equal("Defenceman at forward", line.LeftWing.FitNote);
        Assert.True(line.LeftWing.HasFitNote);

        pair.RightDefence.SelectedPlayer = Skater(Position.Wing, Handedness.Right);
        Assert.Equal("Forward on defence", pair.RightDefence.FitNote);

        line.Centre.SelectedPlayer = Skater(Position.Centre, Handedness.Right);
        Assert.Null(line.Centre.FitNote);
        Assert.Null(lines.Lineup.StartingGoalie.FitNote);
    }

    [Fact]
    public void PlayerChoicesShowNaturalPosition()
    {
        var session = GameTestData.StartSession();
        var lines = new LinesPageViewModel(session);
        var player = session.ManagedTeam.Roster.First(player => player.Position == Position.Defence);

        var option = lines.Lineup.ForwardLines[0].LeftWing.Options.Single(option => option.Id == player.Id);

        Assert.Equal("D", option.PositionAbbreviation);
    }

    [Fact]
    public void ChoosingAScratchedPlayerMovesTheReplacedPlayerToScratches()
    {
        var lines = new LinesPageViewModel(GameTestData.StartSession());
        var scratchedCentre = lines.Lineup.Scratches.Single(player => player.PositionAbbreviation == "C");
        var replacedCentre = lines.Lineup.ForwardLines[1].Centre.SelectedPlayer!;

        lines.Lineup.ForwardLines[1].Centre.SelectedPlayer = scratchedCentre;

        Assert.Contains(replacedCentre, lines.Lineup.Scratches);
        Assert.DoesNotContain(scratchedCentre, lines.Lineup.Scratches);
        Assert.Equal(3, lines.Lineup.Scratches.Count);
    }

    [Fact]
    public void SavingAppliesTheLineupThroughManagement()
    {
        var session = GameTestData.StartSession();
        var lines = new LinesPageViewModel(session);
        var scratchedGoalie = lines.Lineup.Scratches.Single(player => player.PositionAbbreviation == "G");
        lines.Lineup.StartingGoalie.SelectedPlayer = scratchedGoalie;

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
        var original = lines.Lineup.DefencePairs[0].LeftDefence.SelectedPlayer!.Id;
        lines.Lineup.DefencePairs[0].LeftDefence.SelectedPlayer = lines.Lineup.DefencePairs[2].RightDefence.SelectedPlayer;

        lines.RevertCommand.Execute(null);

        Assert.Equal(original, lines.Lineup.DefencePairs[0].LeftDefence.SelectedPlayer?.Id);
        Assert.False(lines.HasChanges);
    }

    [Fact]
    public void RejectedLineupShowsTheValidationFailureAndKeepsTheEdits()
    {
        var gameManager = new GameManager();
        var session = GameTestData.StartSession(gameManager);
        var lines = new LinesPageViewModel(session);
        lines.Lineup.ForwardLines[0].LeftWing.SelectedPlayer = lines.Lineup.ForwardLines[1].LeftWing.SelectedPlayer;

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
        var moved = lines.Lineup.ForwardLines[2].RightWing.SelectedPlayer;
        lines.Lineup.ForwardLines[0].RightWing.SelectedPlayer = moved;

        session.MarkInboxMessageRead(session.Snapshot.Inbox[0].Id);
        lines.Refresh();

        Assert.Equal(moved, lines.Lineup.ForwardLines[0].RightWing.SelectedPlayer);
        Assert.True(lines.HasChanges);
    }

    [Fact]
    public void LoadsEveryUnitAndExtraAttackerFromTheManagedLineup()
    {
        var session = GameTestData.StartSession();
        var lines = new LinesPageViewModel(session);
        var lineup = session.ManagedTeam.Lineup;

        var units = lines.Lineup.PowerPlay.Concat(lines.Lineup.PenaltyKill).Concat(lines.Lineup.OtherSituationUnits)
            .SelectMany(group => group.Units)
            .ToList();

        Assert.Equal(
            lineup.SpecialSituationUnits.Select(unit => string.Join(",", unit.PlayerIds)),
            units.Select(unit => string.Join(",", unit.Slots.Select(slot => slot.SelectedPlayer!.Id))));
        Assert.Equal(["5 ON 4", "5 ON 3", "4 ON 3"], lines.Lineup.PowerPlay.Select(group => group.Title));
        Assert.Equal(["4 ON 5", "3 ON 5", "3 ON 4"], lines.Lineup.PenaltyKill.Select(group => group.Title));
        Assert.Equal(["4 ON 4", "3 ON 3"], lines.Lineup.OtherSituationUnits.Select(group => group.Title));
        Assert.Equal(lineup.ExtraAttackerIds, lines.Lineup.ExtraAttackers.Select(slot => slot.SelectedPlayer!.Id));
    }

    [Fact]
    public void UnitsShowForwardsInFrontOfDefenceAndOfferEverySkater()
    {
        var lines = new LinesPageViewModel(GameTestData.StartSession());
        var powerPlay = lines.Lineup.PowerPlay[0].Units[0];
        var penaltyKill = lines.Lineup.PenaltyKill[1].Units[0];

        Assert.Equal(["LW", "C", "RW"], powerPlay.Forwards.Select(slot => slot.Label));
        Assert.Equal(["LD", "RD"], powerPlay.Defence.Select(slot => slot.Label));
        Assert.Equal(["C"], penaltyKill.Forwards.Select(slot => slot.Label));
        Assert.Equal(["LD", "RD"], penaltyKill.Defence.Select(slot => slot.Label));
        Assert.Equal(["C", "W", "D"], lines.Lineup.OtherSituationUnits[1].Units[0].Slots.Select(slot => slot.Label));
        Assert.Equal(20, powerPlay.Slots[0].Options.Count);
        Assert.DoesNotContain(powerPlay.Slots[0].Options, option => option.Position == Position.Goalie);
    }

    [Fact]
    public void AnySkaterCanBeSavedInAnyUnitSlot()
    {
        var session = GameTestData.StartSession();
        var lines = new LinesPageViewModel(session);
        var unit = lines.Lineup.PowerPlay[0].Units[0];
        var defenceAtCentre = lines.Lineup.DefencePairs[2].RightDefence.SelectedPlayer!;
        var extraAttacker = lines.Lineup.DefencePairs[1].LeftDefence.SelectedPlayer!;

        unit.Forwards[1].SelectedPlayer = defenceAtCentre;
        lines.Lineup.ExtraAttackers[1].SelectedPlayer = extraAttacker;
        Assert.True(lines.HasChanges);
        lines.SaveCommand.Execute(null);

        var saved = session.ManagedTeam.Lineup;
        Assert.False(lines.HasError);
        Assert.Equal(defenceAtCentre.Id, saved.UnitsFor(SpecialSituation.PowerPlay5On4)[0].PlayerIds[1]);
        Assert.Equal(extraAttacker.Id, saved.ExtraAttackerIds[1]);
        Assert.False(lines.HasChanges);
    }

    [Fact]
    public void ChoosingAPlayerAlreadyInTheUnitSwapsTheTwoSlots()
    {
        var lines = new LinesPageViewModel(GameTestData.StartSession());
        var unit = lines.Lineup.PenaltyKill[0].Units[0];
        var centre = unit.Slots[0].SelectedPlayer;
        var defence = unit.Slots[3].SelectedPlayer;

        unit.Slots[0].SelectedPlayer = defence;

        Assert.Equal(defence, unit.Slots[0].SelectedPlayer);
        Assert.Equal(centre, unit.Slots[3].SelectedPlayer);
        Assert.True(lines.HasChanges);
    }

    [Fact]
    public void ChoosingAPlayerFromAnotherUnitDoesNotChangeThatUnit()
    {
        var lines = new LinesPageViewModel(GameTestData.StartSession());
        var first = lines.Lineup.PowerPlay[0].Units[0];
        var second = lines.Lineup.PowerPlay[0].Units[1];
        var secondUnitPlayers = second.Slots.Select(slot => slot.SelectedPlayer).ToList();

        first.Slots[0].SelectedPlayer = second.Slots[0].SelectedPlayer;

        Assert.Equal(secondUnitPlayers, second.Slots.Select(slot => slot.SelectedPlayer));
        Assert.Equal(second.Slots[0].SelectedPlayer, first.Slots[0].SelectedPlayer);
    }

    [Fact]
    public void DressingAScratchedPlayerGivesThemTheReplacedPlayersUnitSlots()
    {
        var session = GameTestData.StartSession();
        var lines = new LinesPageViewModel(session);
        var scratchedCentre = lines.Lineup.Scratches.Single(player => player.PositionAbbreviation == "C");
        var replacedCentre = lines.Lineup.ForwardLines[0].Centre.SelectedPlayer!;
        var unitSlots = AllUnitSlots(lines.Lineup).ToList();
        var replacedSlots = unitSlots.Where(slot => slot.SelectedPlayer == replacedCentre).ToList();
        Assert.NotEmpty(replacedSlots);

        lines.Lineup.ForwardLines[0].Centre.SelectedPlayer = scratchedCentre;

        Assert.All(replacedSlots, slot => Assert.Equal(scratchedCentre, slot.SelectedPlayer));
        Assert.DoesNotContain(unitSlots, slot => slot.SelectedPlayer == replacedCentre);
        Assert.True(replacedCentre.IsScratched);
        Assert.False(scratchedCentre.IsScratched);

        lines.SaveCommand.Execute(null);

        Assert.False(lines.HasError);
        Assert.Contains(replacedCentre.Id, session.ManagedTeam.ScratchedPlayerIds);
        Assert.DoesNotContain(session.ManagedTeam.Lineup.SpecialSituationUnits, unit => unit.PlayerIds.Contains(replacedCentre.Id));
    }

    [Fact]
    public void AScratchedSkaterInAUnitIsNotSaved()
    {
        var session = GameTestData.StartSession();
        var lines = new LinesPageViewModel(session);
        var before = session.ManagedTeam.Lineup;
        var scratchedDefence = lines.Lineup.Scratches.Single(player => player.PositionAbbreviation == "D");

        lines.Lineup.PenaltyKill[0].Units[2].Defence[0].SelectedPlayer = scratchedDefence;
        lines.SaveCommand.Execute(null);

        Assert.True(lines.HasError);
        Assert.Contains("is scratched", lines.ErrorMessage);
        Assert.True(lines.HasChanges);
        Assert.Same(before, session.ManagedTeam.Lineup);
    }

    [Fact]
    public void OtherTeamsLineupsAreReadOnlyAndKeepTheManagedEdits()
    {
        var session = GameTestData.StartSession();
        var lines = new LinesPageViewModel(session);
        var other = lines.Teams.First(team => !team.IsManaged);
        var moved = lines.Lineup.ForwardLines[2].RightWing.SelectedPlayer;
        lines.Lineup.ForwardLines[0].RightWing.SelectedPlayer = moved;

        lines.SelectTeam(other.Id);

        var otherLineup = session.GetTeam(other.Id).Lineup;
        Assert.False(lines.IsEditable);
        Assert.False(lines.Lineup.IsEditable);
        Assert.All(AllUnitSlots(lines.Lineup).Append(lines.Lineup.ForwardLines[0].Centre), slot => Assert.False(slot.IsEditable));
        Assert.Equal(otherLineup.ForwardLines[0].CentreId, lines.Lineup.ForwardLines[0].Centre.SelectedPlayer!.Id);
        Assert.Equal(otherLineup.SpecialSituationUnits[0].PlayerIds, lines.Lineup.PowerPlay[0].Units[0].Slots.Select(slot => slot.SelectedPlayer!.Id));
        Assert.Equal(otherLineup.ExtraAttackerIds, lines.Lineup.ExtraAttackers.Select(slot => slot.SelectedPlayer!.Id));
        Assert.Contains("Read-only", lines.OwnershipNote);
        Assert.Contains(other.Name, lines.Subtitle);

        lines.SelectTeam(session.Snapshot.ManagedTeamId);

        Assert.True(lines.IsEditable);
        Assert.Equal(moved, lines.Lineup.ForwardLines[0].RightWing.SelectedPlayer);
        Assert.True(lines.HasChanges);
    }

    [Fact]
    public void TabsChooseWhichUnitsAreShown()
    {
        var lines = new LinesPageViewModel(GameTestData.StartSession());
        Assert.True(lines.IsEvenStrengthTab);

        lines.SelectTabCommand.Execute(LinesTab.PenaltyKill);

        Assert.True(lines.IsPenaltyKillTab);
        Assert.False(lines.IsEvenStrengthTab);
        Assert.False(lines.IsPowerPlayTab);
        Assert.False(lines.IsOtherTab);
    }

    private static IEnumerable<LineupSlotViewModel> AllUnitSlots(LineupEditorViewModel lineup) =>
        lineup.PowerPlay.Concat(lineup.PenaltyKill).Concat(lineup.OtherSituationUnits)
            .SelectMany(group => group.Units)
            .SelectMany(unit => unit.Slots)
            .Concat(lineup.ExtraAttackers);
}