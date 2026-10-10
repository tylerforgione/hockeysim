using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.NewGame;
using HockeySim.Simulation.Randomness;

using Xunit;

namespace HockeySim.Management.Tests;

public sealed class NewGameTests
{
    private const string InitialManagedTeam = "Halifax Mariners";

    [Fact]
    public void NewGameOptionsExposeEveryAvailableTeamWithoutMutableState()
    {
        var options = new GameManager().GetNewGameOptions();
        var teamNames = Assert.IsAssignableFrom<IList<string>>(options.TeamNames);
        var conferences = Assert.IsAssignableFrom<IList<NewGameConferenceSnapshot>>(
            options.Conferences);

        Assert.Equal(32, teamNames.Count);
        Assert.Equal(32, teamNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(InitialManagedTeam, teamNames);
        Assert.Contains("Seattle Evergreens", teamNames);
        Assert.Equal(2, conferences.Count);
        Assert.All(conferences, conference =>
        {
            Assert.Equal(2, conference.Divisions.Count);
            Assert.All(conference.Divisions, division => Assert.Equal(8, division.TeamNames.Count));
        });
        Assert.Throws<NotSupportedException>(() => teamNames.Clear());
        Assert.Throws<NotSupportedException>(() => conferences.Clear());
    }

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
    public void EveryTeamHasItsOwnColoursWithReadableTextOnTheSecondary()
    {
        var teams = StartGame(12345).League.Teams;

        Assert.Equal(32, teams.Select(team => team.Colours).Distinct().Count());
        Assert.Equal(
            new TeamColours(Colour.FromHex("#0B2545"), Colour.FromHex("#3FA7A3")),
            teams.Single(team => team.Name == InitialManagedTeam).Colours);
        // Desktop writes primary-coloured text on the secondary colour; 4.5:1 is WCAG's body-text minimum.
        Assert.All(teams, team => Assert.True(
            ContrastRatio(team.Colours.Primary, team.Colours.Secondary) >= 4.5,
            $"{team.Name}'s colours are too close for text."));
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

    // Durability is hidden from the user, so snapshots carry every other rating.
    private static readonly IEnumerable<Rating> VisibleRatings =
        Enum.GetValues<Rating>().Where(rating => rating != Rating.Durability).Order().ToList();

    private static NewGameCommand CreateCommand(ulong seed) =>
        new(2026, new RandomState(seed), InitialManagedTeam);

    [Fact]
    public void GeneratedLinesAndPairsPutPlayersOnTheirNaturalSideWhereTheyCan()
    {
        var snapshot = new GameManager().StartNewGame(new NewGameCommand(2026, new RandomState(12345), InitialManagedTeam));

        // A left and a right shot together always play on their forehand sides; a pair that
        // shoots the same way has one player off-hand.
        foreach (var team in snapshot.League.Teams)
        {
            var handedness = team.Roster.ToDictionary(player => player.Id, player => player.Biography.Handedness);
            var sides = team.Lineup.ForwardLines.Select(line => (line.LeftWingId, line.RightWingId))
                .Concat(team.Lineup.DefencePairs.Select(pair => (pair.LeftDefenceId, pair.RightDefenceId)));
            Assert.All(sides, pair => Assert.False(
                handedness[pair.Item1] == Handedness.Right && handedness[pair.Item2] == Handedness.Left));
        }
    }

    private static double ContrastRatio(Colour first, Colour second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static double RelativeLuminance(Colour colour)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linear(colour.Red)) + (0.7152 * Linear(colour.Green)) + (0.0722 * Linear(colour.Blue));
    }

    private static void AssertValidTeam(TeamSnapshot team)
    {
        Assert.False(string.IsNullOrWhiteSpace(team.Name));
        Assert.Equal(23, team.Roster.Count);
        Assert.Equal(23, team.Roster.Select(player => player.Id).Distinct().Count());
        Assert.Equal(23, team.Roster.Select(player => player.Number).Distinct().Count());
        Assert.Equal(5, team.Roster.Count(player => player.Position == Position.Centre));
        Assert.Equal(8, team.Roster.Count(player => player.Position == Position.Wing));
        Assert.Equal(7, team.Roster.Count(player => player.Position == Position.Defence));
        Assert.Equal(3, team.Roster.Count(player => player.Position == Position.Goalie));

        Assert.All(team.Roster, player =>
        {
            Assert.False(string.IsNullOrWhiteSpace(player.FirstName));
            Assert.False(string.IsNullOrWhiteSpace(player.LastName));
            Assert.Equal(VisibleRatings, player.Ratings.Keys.Order());
            Assert.All(player.Ratings.Values, rating => Assert.InRange(rating, 0, 100));
            Assert.Equal(
                OverallRating.Calculate(
                    player.Position,
                    player.Ratings.ToDictionary(rating => rating.Key, rating => new RatingScore(rating.Value))).Value,
                player.Overall);
        });

        Assert.Equal(4, team.Lineup.ForwardLines.Count);
        Assert.Equal(3, team.Lineup.DefencePairs.Count);
        Assert.Equal(20, team.Lineup.DressedPlayerIds.Count);
        Assert.Equal(20, team.Lineup.DressedPlayerIds.Distinct().Count());
        Assert.Equal(3, team.ScratchedPlayerIds.Count);
        Assert.Empty(team.Lineup.DressedPlayerIds.Intersect(team.ScratchedPlayerIds));
        Assert.Equal(
            team.Roster.Select(player => player.Id).ToHashSet(),
            team.Lineup.DressedPlayerIds.Concat(team.ScratchedPlayerIds).ToHashSet());

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
        Assert.Equal(1, team.ScratchedPlayerIds.Count(id => rosterById[id].Position == Position.Centre));
        Assert.Equal(1, team.ScratchedPlayerIds.Count(id => rosterById[id].Position == Position.Defence));
        Assert.Equal(1, team.ScratchedPlayerIds.Count(id => rosterById[id].Position == Position.Goalie));
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