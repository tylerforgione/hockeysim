using HockeySim.Domain;
using HockeySim.Management.NewGame;

using Xunit;

namespace HockeySim.Management.Tests;

public sealed class NewGameTests
{
    private const string InitialManagedTeam = "Halifax Mariners";

    [Fact]
    public void StartNewGameCreatesAValidInspectableWorld()
    {
        var snapshot = StartGame(12345);

        Assert.Equal(2026, snapshot.League.SeasonYear);
        Assert.Equal(2, snapshot.League.Conferences.Count);
        Assert.All(snapshot.League.Conferences, conference =>
        {
            Assert.Equal(2, conference.Divisions.Count);
            Assert.All(conference.Divisions, division => Assert.Equal(8, division.Teams.Count));
        });
        Assert.Equal(32, snapshot.League.Teams.Count);
        Assert.Equal(32, snapshot.League.Teams.Select(team => team.Id).Distinct().Count());

        Assert.All(snapshot.League.Teams, AssertValidTeam);
        Assert.Equal(
            InitialManagedTeam,
            snapshot.League.Teams.Single(team => team.Id == snapshot.ManagedTeamId).Name);
    }

    [Fact]
    public void EquivalentInputsCreateEquivalentWorldsAndRandomState()
    {
        var first = StartGame(987654321);
        var second = StartGame(987654321);

        Assert.Equal(CreateFingerprint(first), CreateFingerprint(second));
        Assert.Equal(first.RandomState, second.RandomState);
    }

    [Fact]
    public void DifferentRandomStateCreatesDifferentWorldData()
    {
        var first = StartGame(1);
        var second = StartGame(2);

        Assert.NotEqual(CreateFingerprint(first), CreateFingerprint(second));
    }

    [Fact]
    public void SnapshotsDoNotExposeMutableWorldCollections()
    {
        var manager = new GameManager();
        var snapshot = manager.StartNewGame(CreateCommand(42));
        var teams = Assert.IsAssignableFrom<IList<TeamSnapshot>>(snapshot.League.Teams);
        var roster = Assert.IsAssignableFrom<IList<PlayerSnapshot>>(snapshot.League.Teams[0].Roster);
        var ratings = Assert.IsAssignableFrom<IDictionary<Rating, int>>(
            snapshot.League.Teams[0].Roster[0].Ratings);

        Assert.Throws<NotSupportedException>(() => teams.Clear());
        Assert.Throws<NotSupportedException>(() => roster.Clear());
        Assert.Throws<NotSupportedException>(() => ratings[Rating.Skating] = 100);
        Assert.Equal(32, manager.GetSnapshot().League.Teams.Count);
    }

    [Fact]
    public void SelectManagedTeamChangesOnlyTheControlledSelection()
    {
        var manager = new GameManager();
        var initial = manager.StartNewGame(CreateCommand(84));
        var newTeam = initial.League.Teams.Single(team => team.Name == "Seattle Evergreens");

        var updated = manager.SelectManagedTeam(newTeam.Id);

        Assert.Equal(newTeam.Id, updated.ManagedTeamId);
        Assert.Equal(CreateWorldFingerprint(initial), CreateWorldFingerprint(updated));
        Assert.Equal(initial.RandomState, updated.RandomState);
    }

    [Fact]
    public void InvalidTeamSelectionDoesNotReplaceTheCurrentGame()
    {
        var manager = new GameManager();
        var initial = manager.StartNewGame(CreateCommand(99));

        Assert.Throws<ArgumentException>(() => manager.StartNewGame(
            new NewGameCommand(2026, new RandomState(100), "Unknown Team")));
        Assert.Equal(initial.ManagedTeamId, manager.GetSnapshot().ManagedTeamId);
    }

    private static GameSnapshot StartGame(ulong seed) =>
        new GameManager().StartNewGame(CreateCommand(seed));

    private static NewGameCommand CreateCommand(ulong seed) =>
        new(2026, new RandomState(seed), InitialManagedTeam);

    private static void AssertValidTeam(TeamSnapshot team)
    {
        Assert.False(string.IsNullOrWhiteSpace(team.Name));
        Assert.Equal(23, team.Roster.Count);
        Assert.Equal(23, team.Roster.Select(player => player.Id).Distinct().Count());
        Assert.Equal(23, team.Roster.Select(player => player.Number).Distinct().Count());
        Assert.Equal(5, team.Roster.Count(player => player.Position == Position.Centre));
        Assert.Equal(9, team.Roster.Count(player => player.Position == Position.Wing));
        Assert.Equal(7, team.Roster.Count(player => player.Position == Position.Defence));
        Assert.Equal(2, team.Roster.Count(player => player.Position == Position.Goalie));

        Assert.All(team.Roster, player =>
        {
            Assert.False(string.IsNullOrWhiteSpace(player.FirstName));
            Assert.False(string.IsNullOrWhiteSpace(player.LastName));
            Assert.InRange(player.Age, 18, 35);
            Assert.Equal(Enum.GetValues<Rating>().Length, player.Ratings.Count);
            Assert.All(player.Ratings.Values, rating => Assert.InRange(rating, 40, 90));
        });

        Assert.Equal(4, team.Lineup.ForwardLines.Count);
        Assert.Equal(3, team.Lineup.DefencePairs.Count);
        Assert.Equal(20, team.Lineup.DressedPlayerIds.Count);
        Assert.Equal(20, team.Lineup.DressedPlayerIds.Distinct().Count());

        var rosterById = team.Roster.ToDictionary(player => player.Id);
        Assert.All(team.Lineup.ForwardLines, line =>
        {
            Assert.Equal(Position.Wing, rosterById[line.LeftWingId].Position);
            Assert.Equal(Position.Centre, rosterById[line.CentreId].Position);
            Assert.Equal(Position.Wing, rosterById[line.RightWingId].Position);
        });
        Assert.All(team.Lineup.DefencePairs, pair =>
        {
            Assert.Equal(Position.Defence, rosterById[pair.LeftDefenceId].Position);
            Assert.Equal(Position.Defence, rosterById[pair.RightDefenceId].Position);
        });
        Assert.Equal(Position.Goalie, rosterById[team.Lineup.StartingGoalieId].Position);
        Assert.Equal(Position.Goalie, rosterById[team.Lineup.BackupGoalieId].Position);
    }

    private static string CreateFingerprint(GameSnapshot snapshot) =>
        $"{CreateWorldFingerprint(snapshot)}|{snapshot.ManagedTeamId}|{snapshot.RandomState.Value}";

    private static string CreateWorldFingerprint(GameSnapshot snapshot) =>
        string.Join(
            '|',
            snapshot.League.Teams.SelectMany(team =>
                team.Roster.Select(player =>
                    $"{team.Id}:{team.Name}:{player.Id}:{player.FirstName}:{player.LastName}:" +
                    $"{player.Position}:{player.Age}:{player.Number}:" +
                    string.Join(',', player.Ratings.OrderBy(pair => pair.Key)))));
}