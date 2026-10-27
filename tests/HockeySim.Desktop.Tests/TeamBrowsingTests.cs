using HockeySim.Desktop.Players;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Teams;
using HockeySim.Domain;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class TeamBrowsingTests
{
    [Fact]
    public void TeamsPageListsEveryTeamAndStartsOnTheManagedTeam()
    {
        var session = GameTestData.StartSession();
        var teams = new TeamsPageViewModel(session);

        Assert.Equal(32, teams.Teams.Count);
        Assert.Equal(GameTestData.ManagedTeamName, teams.SelectedTeam.Name);
        Assert.True(teams.IsSelectedTeamManaged);
        Assert.Single(teams.Teams, team => team.IsManaged);
    }

    [Fact]
    public void SelectingAnotherTeamShowsItsFullRosterReadOnly()
    {
        var session = GameTestData.StartSession();
        var teams = new TeamsPageViewModel(session);
        var other = session.Snapshot.League.Teams.First(team => team.Id != session.Snapshot.ManagedTeamId);

        teams.SelectTeam(other.Id);

        Assert.Equal(other.Name, teams.SelectedTeam.Name);
        Assert.False(teams.IsSelectedTeamManaged);
        Assert.Contains("Read-only", teams.OwnershipNote);
        Assert.Equal(20, teams.Roster.Skaters.Count);
        Assert.Equal(3, teams.Roster.Goalies.Count);
        Assert.Equal(
            other.Roster.Select(player => player.Id.Value).Order(),
            teams.Roster.Skaters.Concat(teams.Roster.Goalies).Select(row => row.Player.Id.Value).Order());
        Assert.Contains(other.Roster, player => player.Id == teams.Roster.SelectedStrip?.PlayerId);
    }

    [Fact]
    public void PreviousAndNextWrapAroundTheLeague()
    {
        var teams = new TeamsPageViewModel(GameTestData.StartSession());
        teams.SelectTeam(teams.Teams[0].Id);

        teams.ShowPreviousTeamCommand.Execute(null);
        Assert.Equal(teams.Teams[^1], teams.SelectedTeam);

        teams.ShowNextTeamCommand.Execute(null);
        Assert.Equal(teams.Teams[0], teams.SelectedTeam);
    }

    [Fact]
    public void RosterRowsDescribeLineupRoles()
    {
        var session = GameTestData.StartSession();
        var team = session.ManagedTeam;
        var roster = new TeamRosterViewModel(session, team.Id);
        var rows = roster.Skaters.Concat(roster.Goalies).ToDictionary(row => row.Player.Id);

        Assert.Equal("F1 C", rows[team.Lineup.ForwardLines[0].CentreId].LineupRole);
        Assert.Equal("F2 LW", rows[team.Lineup.ForwardLines[1].LeftWingId].LineupRole);
        Assert.Equal("D3 RD", rows[team.Lineup.DefencePairs[2].RightDefenceId].LineupRole);
        Assert.Equal("Starter", rows[team.Lineup.StartingGoalieId].LineupRole);
        Assert.Equal("Backup", rows[team.Lineup.BackupGoalieId].LineupRole);
        Assert.All(team.ScratchedPlayerIds, id => Assert.True(rows[id].IsScratched));
    }

    [Fact]
    public void SelectingAGoalieClearsTheSkaterSelection()
    {
        var session = GameTestData.StartSession();
        var roster = new TeamRosterViewModel(session, session.ManagedTeam.Id);

        roster.SelectedGoalie = roster.Goalies[1];

        Assert.Null(roster.SelectedSkater);
        Assert.Equal(roster.Goalies[1].Player.Id, roster.SelectedStrip?.PlayerId);
    }
}