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
        Assert.Equal(other.Name, teams.Roster.SelectedPlayer?.TeamName);
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
    public void PlayerDetailShowsIdentityAgePositionAndEveryRating()
    {
        var session = GameTestData.StartSession();
        var team = session.ManagedTeam;
        var goalie = team.Roster.First(player => player.Position == Position.Goalie);

        var detail = new PlayerDetailViewModel(goalie, team, session.GetSeasonTotals(goalie.Id), 2026);

        Assert.Equal($"{goalie.FirstName} {goalie.LastName}", detail.FullName);
        Assert.Equal($"#{goalie.Number}", detail.Number);
        Assert.Equal("Goalie", detail.Position);
        Assert.Equal(goalie.Age, detail.Age);
        Assert.Equal(team.Name, detail.TeamName);
        Assert.Equal("GOALTENDING", detail.RatingGroups[0].Name);
        var ratings = detail.RatingGroups.SelectMany(group => group.Ratings).ToList();
        Assert.Equal(Enum.GetValues<Rating>().Length, ratings.Count);
        Assert.Equal(goalie.Ratings[Rating.GoalieReflex], ratings.Single(rating => rating.Name == "Reflexes").Value);
    }

    [Fact]
    public void SelectingAGoalieClearsTheSkaterSelection()
    {
        var session = GameTestData.StartSession();
        var roster = new TeamRosterViewModel(session, session.ManagedTeam.Id);

        roster.SelectedGoalie = roster.Goalies[1];

        Assert.Null(roster.SelectedSkater);
        Assert.Equal(roster.Goalies[1].Player.Id, roster.SelectedPlayer?.Id);
    }
}