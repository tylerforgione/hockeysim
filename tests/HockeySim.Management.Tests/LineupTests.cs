using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Lineups;
using HockeySim.Management.NewGame;

using Xunit;

namespace HockeySim.Management.Tests;

public sealed class LineupTests
{
    private const string ManagedTeamName = "Halifax Mariners";

    [Fact]
    public void SetLineupAppliesAssignmentsOnlyToTheManagedTeam()
    {
        var manager = new GameManager();
        var before = StartGame(manager);
        var managedBefore = GetManagedTeam(before);
        var otherBefore = before.League.Teams.First(team => team.Id != before.ManagedTeamId);
        var originalManagedLineup = CreateLineupFingerprint(managedBefore.Lineup);
        var originalOtherLineup = CreateLineupFingerprint(otherBefore.Lineup);
        var command = CreateEditedLineup(managedBefore);

        var after = manager.SetLineup(command);

        var managedAfter = GetManagedTeam(after);
        var otherAfter = after.League.Teams.Single(team => team.Id == otherBefore.Id);
        AssertCommandApplied(command, managedAfter.Lineup);
        Assert.NotEqual(originalManagedLineup, CreateLineupFingerprint(managedAfter.Lineup));
        Assert.Equal(originalManagedLineup, CreateLineupFingerprint(managedBefore.Lineup));
        Assert.Equal(originalOtherLineup, CreateLineupFingerprint(otherAfter.Lineup));
        Assert.Equal(before.RandomState, after.RandomState);

        var rosterById = managedAfter.Roster.ToDictionary(player => player.Id);
        Assert.Equal(20, managedAfter.Lineup.DressedPlayerIds.Count);
        Assert.Equal(20, managedAfter.Lineup.DressedPlayerIds.Distinct().Count());
        Assert.Equal(3, managedAfter.ScratchedPlayerIds.Count);
        Assert.Empty(managedAfter.Lineup.DressedPlayerIds.Intersect(managedAfter.ScratchedPlayerIds));
        Assert.Equal(4, managedAfter.Lineup.DressedPlayerIds.Count(id => rosterById[id].Position == Position.Centre));
        Assert.Equal(8, managedAfter.Lineup.DressedPlayerIds.Count(id => rosterById[id].Position == Position.Wing));
        Assert.Equal(6, managedAfter.Lineup.DressedPlayerIds.Count(id => rosterById[id].Position == Position.Defence));
        Assert.Equal(2, managedAfter.Lineup.DressedPlayerIds.Count(id => rosterById[id].Position == Position.Goalie));
        Assert.Equal(1, managedAfter.ScratchedPlayerIds.Count(id => rosterById[id].Position == Position.Centre));
        Assert.Equal(1, managedAfter.ScratchedPlayerIds.Count(id => rosterById[id].Position == Position.Defence));
        Assert.Equal(1, managedAfter.ScratchedPlayerIds.Count(id => rosterById[id].Position == Position.Goalie));
    }

    [Fact]
    public void SetLineupCanDressTheScratchedGoalieAndScratchAPreviouslyDressedGoalie()
    {
        var manager = new GameManager();
        var before = StartGame(manager);
        var managedBefore = GetManagedTeam(before);
        var scratchedGoalieId = managedBefore.ScratchedPlayerIds.Single(
            id => managedBefore.Roster.Single(player => player.Id == id).Position == Position.Goalie);
        var previousBackupId = managedBefore.Lineup.BackupGoalieId;

        var after = manager.SetLineup(CreateEditedLineup(managedBefore));
        var managedAfter = GetManagedTeam(after);

        Assert.Equal(scratchedGoalieId, managedAfter.Lineup.StartingGoalieId);
        Assert.Contains(previousBackupId, managedAfter.ScratchedPlayerIds);
        Assert.DoesNotContain(scratchedGoalieId, managedAfter.ScratchedPlayerIds);
    }

    [Fact]
    public void SetLineupRejectsAPlayerOutsideTheManagedRosterWithoutChangingState()
    {
        var manager = new GameManager();
        var before = StartGame(manager);
        var managedBefore = GetManagedTeam(before);
        var foreignTeam = before.League.Teams.First(team => team.Id != before.ManagedTeamId);
        var foreignWingId = foreignTeam.Roster.First(player => player.Position == Position.Wing).Id;
        var command = CreateCommand(managedBefore.Lineup);
        command = command with
        {
            ForwardLines = command.ForwardLines
                .Select((line, index) => index == 0 ? line with { LeftWingId = foreignWingId } : line)
                .ToList(),
        };

        AssertRejectedWithoutChangingState(manager, command, before);
    }

    [Fact]
    public void SetLineupRejectsAnOutOfPositionAssignmentWithoutChangingState()
    {
        var manager = new GameManager();
        var before = StartGame(manager);
        var managedBefore = GetManagedTeam(before);
        var centreId = managedBefore.Roster.First(player => player.Position == Position.Centre).Id;
        var command = CreateCommand(managedBefore.Lineup);
        command = command with
        {
            ForwardLines = command.ForwardLines
                .Select((line, index) => index == 0 ? line with { LeftWingId = centreId } : line)
                .ToList(),
        };

        AssertRejectedWithoutChangingState(manager, command, before);
    }

    [Fact]
    public void SetLineupRejectsADuplicateSkaterWithoutChangingState()
    {
        var manager = new GameManager();
        var before = StartGame(manager);
        var managedBefore = GetManagedTeam(before);
        var command = CreateCommand(managedBefore.Lineup);
        var duplicateWingId = command.ForwardLines[0].LeftWingId;
        command = command with
        {
            ForwardLines = command.ForwardLines
                .Select((line, index) => index == 1 ? line with { LeftWingId = duplicateWingId } : line)
                .ToList(),
        };

        AssertRejectedWithoutChangingState(manager, command, before);
    }

    [Fact]
    public void SetLineupRequiresDistinctStartingAndBackupGoalies()
    {
        var manager = new GameManager();
        var before = StartGame(manager);
        var command = CreateCommand(GetManagedTeam(before).Lineup);
        command = command with { BackupGoalieId = command.StartingGoalieId };

        AssertRejectedWithoutChangingState(manager, command, before);
    }

    [Fact]
    public void LineupSnapshotCollectionsCannotBeMutated()
    {
        var manager = new GameManager();
        var managedTeam = GetManagedTeam(StartGame(manager));
        var forwardLines = Assert.IsAssignableFrom<IList<ForwardLineSnapshot>>(
            managedTeam.Lineup.ForwardLines);
        var defencePairs = Assert.IsAssignableFrom<IList<DefencePairSnapshot>>(
            managedTeam.Lineup.DefencePairs);
        var dressedPlayerIds = Assert.IsAssignableFrom<IList<PlayerId>>(
            managedTeam.Lineup.DressedPlayerIds);
        var scratchedPlayerIds = Assert.IsAssignableFrom<IList<PlayerId>>(
            managedTeam.ScratchedPlayerIds);

        Assert.Throws<NotSupportedException>(() => forwardLines.Clear());
        Assert.Throws<NotSupportedException>(() => defencePairs.Clear());
        Assert.Throws<NotSupportedException>(() => dressedPlayerIds.Clear());
        Assert.Throws<NotSupportedException>(() => scratchedPlayerIds.Clear());
        Assert.Equal(20, manager.GetSnapshot().League.Teams.Single(
            team => team.Id == managedTeam.Id).Lineup.DressedPlayerIds.Count);
    }

    private static GameSnapshot StartGame(GameManager manager) =>
        manager.StartNewGame(new NewGameCommand(2026, new RandomState(12345), ManagedTeamName));

    private static TeamSnapshot GetManagedTeam(GameSnapshot snapshot) =>
        snapshot.League.Teams.Single(team => team.Id == snapshot.ManagedTeamId);

    private static SetLineupCommand CreateEditedLineup(TeamSnapshot team)
    {
        var centres = team.Roster.Where(player => player.Position == Position.Centre).ToList();
        var wings = team.Roster.Where(player => player.Position == Position.Wing).ToList();
        var defence = team.Roster.Where(player => player.Position == Position.Defence).ToList();
        var goalies = team.Roster.Where(player => player.Position == Position.Goalie).ToList();
        var forwardLines = Enumerable.Range(0, 4)
            .Select(index => new ForwardLineSelection(
                wings[(index * 2) + 1].Id,
                centres[index + 1].Id,
                wings[index * 2].Id))
            .ToList();
        var defencePairs = Enumerable.Range(0, 3)
            .Select(index => new DefencePairSelection(
                defence[(index * 2) + 1].Id,
                defence[(index * 2) + 2].Id))
            .ToList();

        return new SetLineupCommand(forwardLines, defencePairs, goalies[2].Id, goalies[0].Id);
    }

    private static SetLineupCommand CreateCommand(LineupSnapshot lineup) =>
        new(
            lineup.ForwardLines.Select(line => new ForwardLineSelection(
                line.LeftWingId,
                line.CentreId,
                line.RightWingId)).ToList(),
            lineup.DefencePairs.Select(pair => new DefencePairSelection(
                pair.LeftDefenceId,
                pair.RightDefenceId)).ToList(),
            lineup.StartingGoalieId,
            lineup.BackupGoalieId);

    private static void AssertCommandApplied(SetLineupCommand command, LineupSnapshot lineup)
    {
        Assert.Equal(command.ForwardLines.Count, lineup.ForwardLines.Count);
        for (var index = 0; index < command.ForwardLines.Count; index++)
        {
            Assert.Equal(command.ForwardLines[index].LeftWingId, lineup.ForwardLines[index].LeftWingId);
            Assert.Equal(command.ForwardLines[index].CentreId, lineup.ForwardLines[index].CentreId);
            Assert.Equal(command.ForwardLines[index].RightWingId, lineup.ForwardLines[index].RightWingId);
        }

        Assert.Equal(command.DefencePairs.Count, lineup.DefencePairs.Count);
        for (var index = 0; index < command.DefencePairs.Count; index++)
        {
            Assert.Equal(command.DefencePairs[index].LeftDefenceId, lineup.DefencePairs[index].LeftDefenceId);
            Assert.Equal(command.DefencePairs[index].RightDefenceId, lineup.DefencePairs[index].RightDefenceId);
        }

        Assert.Equal(command.StartingGoalieId, lineup.StartingGoalieId);
        Assert.Equal(command.BackupGoalieId, lineup.BackupGoalieId);
    }

    private static void AssertRejectedWithoutChangingState(
        GameManager manager,
        SetLineupCommand command,
        GameSnapshot before)
    {
        var fingerprint = CreateWorldLineupFingerprint(before);

        Assert.Throws<ArgumentException>(() => manager.SetLineup(command));

        Assert.Equal(fingerprint, CreateWorldLineupFingerprint(manager.GetSnapshot()));
    }

    private static string CreateWorldLineupFingerprint(GameSnapshot snapshot) =>
        string.Join(
            '|',
            snapshot.League.Teams.Select(team => $"{team.Id}:{CreateLineupFingerprint(team.Lineup)}"));

    private static string CreateLineupFingerprint(LineupSnapshot lineup) =>
        string.Join(
            ',',
            lineup.ForwardLines.Select(line => $"{line.LeftWingId}:{line.CentreId}:{line.RightWingId}"))
        + "|"
        + string.Join(
            ',',
            lineup.DefencePairs.Select(pair => $"{pair.LeftDefenceId}:{pair.RightDefenceId}"))
        + $"|{lineup.StartingGoalieId}:{lineup.BackupGoalieId}";
}