using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Teams;
using HockeySim.Domain;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class TeamBrowsingTests
{
    [Fact]
    public void TeamsPageListsEveryTeamByConferenceAndDivisionInLeagueOrder()
    {
        var session = GameTestData.StartSession();
        var teams = new TeamsPageViewModel(session, _ => { });

        var rows = teams.Conferences.SelectMany(conference => conference.Divisions).SelectMany(division => division.Teams).ToList();
        Assert.Equal(32, rows.Count);
        Assert.Equal(session.Snapshot.League.Teams.Select(team => team.Id), rows.Select(row => row.Id));
        Assert.Equal(session.Snapshot.League.Conferences.Select(conference => conference.Name), teams.Conferences.Select(conference => conference.Name));
        Assert.Equal(
            session.Snapshot.League.Conferences.SelectMany(conference => conference.Divisions).Select(division => division.Name),
            teams.Conferences.SelectMany(conference => conference.Divisions).Select(division => division.Name));
        Assert.Equal("32 teams", teams.Subtitle);
        var managed = Assert.Single(rows, row => row.IsManaged);
        Assert.Equal(GameTestData.ManagedTeamName, managed.Name);
    }

    [Fact]
    public async Task TeamsPageRowsShowTheRecordAndPointsAndFollowADay()
    {
        var session = GameTestData.StartSession();
        var teams = new TeamsPageViewModel(session, _ => { });
        Assert.All(teams.Conferences.SelectMany(conference => conference.Divisions).SelectMany(division => division.Teams), row =>
        {
            Assert.Equal("0-0-0", row.Record);
            Assert.Equal(0, row.Points);
        });

        await session.AdvanceDayAsync();
        teams.Refresh();

        var rows = teams.Conferences.SelectMany(conference => conference.Divisions).SelectMany(division => division.Teams).ToDictionary(row => row.Id);
        var records = session.Snapshot.Season.Standings.Conferences.SelectMany(conference => conference.Divisions).SelectMany(division => division.Teams).Select(entry => entry.Record);
        Assert.All(records, record =>
        {
            Assert.Equal($"{record.Wins}-{record.RegulationLosses}-{record.OvertimeLosses + record.ShootoutLosses}", rows[record.TeamId].Record);
            Assert.Equal(record.Points, rows[record.TeamId].Points);
        });
    }

    [Fact]
    public void ChoosingATeamOnTheTeamsPageOpensIt()
    {
        var session = GameTestData.StartSession();
        var opened = new List<TeamId>();
        var teams = new TeamsPageViewModel(session, opened.Add);
        var row = teams.Conferences[1].Divisions[1].Teams[2];

        row.OpenTeamCommand.Execute(null);

        Assert.Equal([row.Id], opened);
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