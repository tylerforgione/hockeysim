using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Inbox;
using HockeySim.Management.NewGame;

using Xunit;

namespace HockeySim.Management.Tests;

public sealed class InboxTests
{
    private const string ManagedTeamName = "Ottawa Owls";

    [Fact]
    public void NewGameDeliversUnreadWelcomeMessagesWithTheOwnerFirst()
    {
        var snapshot = StartGame(new GameManager());

        Assert.Equal(
            [
                InboxSenderRole.Owner,
                InboxSenderRole.AssistantGeneralManager,
                InboxSenderRole.HeadScout,
                InboxSenderRole.Captain,
            ],
            snapshot.Inbox.Select(message => message.SenderRole));
        Assert.All(snapshot.Inbox, message => Assert.False(message.IsRead));
        Assert.Equal(snapshot.Inbox.Count, snapshot.Inbox.Select(message => message.Id).Distinct().Count());
        Assert.Contains(ManagedTeamName, snapshot.Inbox[0].Subject);
    }

    [Fact]
    public void WelcomeMessagesDescribeTheManagedTeamsActualPlayers()
    {
        var snapshot = StartGame(new GameManager());
        var team = snapshot.League.Teams.Single(team => team.Id == snapshot.ManagedTeamId);
        var rosterById = team.Roster.ToDictionary(player => player.Id);
        var lineupNote = snapshot.Inbox.Single(message => message.SenderRole == InboxSenderRole.AssistantGeneralManager);
        var scoutingReport = snapshot.Inbox.Single(message => message.SenderRole == InboxSenderRole.HeadScout);
        var captainMessage = snapshot.Inbox.Single(message => message.SenderRole == InboxSenderRole.Captain);
        var bestSkater = team.Roster
            .Where(player => player.Position != Position.Goalie)
            .MaxBy(player => player.Ratings[Rating.Skating])!;

        Assert.Contains(FullName(rosterById[team.Lineup.StartingGoalieId]), lineupNote.Body);
        Assert.All(team.ScratchedPlayerIds, id => Assert.Contains(FullName(rosterById[id]), lineupNote.Body));
        Assert.Contains($": {bestSkater.Ratings[Rating.Skating]}", scoutingReport.Body);
        Assert.Contains(team.Roster, player => FullName(player) == captainMessage.SenderName);
    }

    [Fact]
    public void MarkingAMessageReadChangesOnlyThatMessageAndKeepsEarlierSnapshotsUnchanged()
    {
        var manager = new GameManager();
        var before = StartGame(manager);
        var target = before.Inbox[1];

        var after = manager.MarkInboxMessageRead(target.Id);

        Assert.True(after.Inbox.Single(message => message.Id == target.Id).IsRead);
        Assert.Single(after.Inbox, message => message.IsRead);
        Assert.All(before.Inbox, message => Assert.False(message.IsRead));
        Assert.Equal(before.RandomState, after.RandomState);
    }

    [Fact]
    public void MarkingAnUnknownMessageReadIsRejected()
    {
        var manager = new GameManager();
        StartGame(manager);

        Assert.Throws<ArgumentException>(() => manager.MarkInboxMessageRead(new InboxMessageId(999)));
    }

    [Fact]
    public void MarkingAMessageReadRequiresAGame()
    {
        Assert.Throws<InvalidOperationException>(
            () => new GameManager().MarkInboxMessageRead(new InboxMessageId(1)));
    }

    [Fact]
    public void StartingAnotherGameReplacesThePreviousInbox()
    {
        var manager = new GameManager();
        var first = StartGame(manager);
        manager.MarkInboxMessageRead(first.Inbox[0].Id);

        var second = StartGame(manager);

        Assert.Equal(first.Inbox.Count, second.Inbox.Count);
        Assert.All(second.Inbox, message => Assert.False(message.IsRead));
    }

    [Fact]
    public void SelectingAnotherTeamReplacesTheWelcomeMessagesWithOnesForThatTeam()
    {
        var manager = new GameManager();
        var initial = StartGame(manager);
        manager.MarkInboxMessageRead(initial.Inbox[0].Id);
        var newTeam = initial.League.Teams.Single(team => team.Name == "Seattle Evergreens");
        var rosterById = newTeam.Roster.ToDictionary(player => player.Id);

        var updated = manager.SelectManagedTeam(newTeam.Id);

        var lineupNote = updated.Inbox.Single(message => message.SenderRole == InboxSenderRole.AssistantGeneralManager);
        var captainMessage = updated.Inbox.Single(message => message.SenderRole == InboxSenderRole.Captain);
        Assert.Equal(initial.Inbox.Count, updated.Inbox.Count);
        Assert.All(updated.Inbox, message => Assert.False(message.IsRead));
        Assert.Contains(newTeam.Name, updated.Inbox[0].Subject);
        Assert.DoesNotContain(ManagedTeamName, updated.Inbox[0].Subject);
        Assert.Contains(FullName(rosterById[newTeam.Lineup.StartingGoalieId]), lineupNote.Body);
        Assert.Contains(newTeam.Roster, player => FullName(player) == captainMessage.SenderName);
    }

    [Fact]
    public void ReselectingTheManagedTeamKeepsTheCurrentInbox()
    {
        var manager = new GameManager();
        var initial = StartGame(manager);
        manager.MarkInboxMessageRead(initial.Inbox[0].Id);

        var updated = manager.SelectManagedTeam(initial.ManagedTeamId);

        Assert.True(updated.Inbox[0].IsRead);
    }

    [Fact]
    public void InboxSnapshotCannotBeMutated()
    {
        var inbox = Assert.IsAssignableFrom<IList<InboxMessageSnapshot>>(StartGame(new GameManager()).Inbox);

        Assert.Throws<NotSupportedException>(() => inbox.Clear());
    }

    private static GameSnapshot StartGame(GameManager manager) =>
        manager.StartNewGame(new NewGameCommand(2026, new RandomState(24680), ManagedTeamName));

    private static string FullName(PlayerSnapshot player) => $"{player.FirstName} {player.LastName}";
}