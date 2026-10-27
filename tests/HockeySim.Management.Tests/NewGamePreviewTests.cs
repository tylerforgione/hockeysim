using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.NewGame;
using HockeySim.Simulation.Randomness;

using Xunit;

namespace HockeySim.Management.Tests;

public sealed class NewGamePreviewTests
{
    private const string Team = "Halifax Mariners";
    private const int Year = 2026;
    private const int OpeningMonth = 10;

    private static NewGamePreviewSnapshot Preview(ulong seed) =>
        new GameManager().PreviewNewGame(Year, new RandomState(seed));

    private static GameSnapshot Start(ulong seed, string managerName = NewGameCommand.DefaultManagerName) =>
        new GameManager().StartNewGame(new NewGameCommand(Year, new RandomState(seed), Team, managerName));

    private static double Mean(IEnumerable<PlayerSnapshot> players) => players.Average(player => (double)player.Overall);

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static (double Forwards, double Defence, double Goalie) Strength(TeamSnapshot team)
    {
        var byId = team.Roster.ToDictionary(player => player.Id);
        var forwards = team.Lineup.ForwardLines
            .SelectMany(line => new[] { line.LeftWingId, line.CentreId, line.RightWingId })
            .Select(id => byId[id]);
        var defence = team.Lineup.DefencePairs
            .SelectMany(pair => new[] { pair.LeftDefenceId, pair.RightDefenceId })
            .Select(id => byId[id]);
        return (Mean(forwards), Mean(defence), byId[team.Lineup.StartingGoalieId].Overall);
    }

    [Fact]
    public void PreviewDescribesTheWorldStartNewGameBuilds()
    {
        var preview = Preview(777);
        var started = Start(777);

        Assert.Equal(started.League.SeasonYear, preview.SeasonYear);
        Assert.Equal(started.League.Teams.Select(team => team.Name), preview.Teams.Select(team => team.Name));
        Assert.Equal(
            started.League.Conferences.Select(conference => conference.Name),
            preview.Conferences.Select(conference => conference.Name));
        Assert.Equal(
            started.League.Conferences.SelectMany(c => c.Divisions).Select(d => d.Name),
            preview.Conferences.SelectMany(c => c.Divisions).Select(d => d.Name));

        foreach (var conference in preview.Conferences)
        {
            foreach (var division in conference.Divisions)
            {
                Assert.All(division.Teams, team =>
                {
                    Assert.Equal(division.Name, team.DivisionName);
                    Assert.Equal(conference.Name, team.ConferenceName);
                });
            }
        }

        foreach (var team in preview.Teams)
        {
            var actual = started.League.Teams.Single(candidate => candidate.Name == team.Name);
            Assert.Equal(actual.Colours, team.Colours);
            var byId = actual.Roster.ToDictionary(player => player.Id);
            var starter = byId[actual.Lineup.StartingGoalieId];
            var goalie = team.KeyPlayers[^1];
            Assert.Equal(
                (starter.FirstName, starter.LastName, starter.Number, starter.Overall),
                (goalie.FirstName, goalie.LastName, goalie.Number, goalie.Overall));
        }
    }

    [Fact]
    public void PreviewIsEqualForTheSameSeedAndDiffersForAnother()
    {
        static string Describe(NewGamePreviewSnapshot preview) =>
            string.Join(
                "|",
                preview.Teams.Select(team => $"{team.Name}:{team.Overall}:{team.AverageAge}:" +
                    string.Join(",", team.KeyPlayers.Select(player => $"{player.LastName}{player.Number}"))));

        Assert.Equal(Describe(Preview(5)), Describe(Preview(5)));
        Assert.NotEqual(Describe(Preview(5)), Describe(Preview(6)));
    }

    [Fact]
    public void PreviewWithNoGameLeavesTheManagerWithoutAGame()
    {
        var manager = new GameManager();

        manager.PreviewNewGame(Year, new RandomState(1));

        Assert.Throws<InvalidOperationException>(() => manager.GetSnapshot());
    }

    [Fact]
    public void PreviewDoesNotChangeAGameInProgress()
    {
        var withPreview = new GameManager();
        var without = new GameManager();
        var command = new NewGameCommand(Year, new RandomState(42), Team, "Pat");
        var before = withPreview.StartNewGame(command);
        without.StartNewGame(command);

        withPreview.PreviewNewGame(Year + 1, new RandomState(99));

        var after = withPreview.GetSnapshot();
        Assert.Equal(before.RandomState, after.RandomState);
        Assert.Equal(before.ManagedTeamId, after.ManagedTeamId);
        Assert.Equal(before.ManagerName, after.ManagerName);
        Assert.Equal(before.League.SeasonYear, after.League.SeasonYear);
        Assert.Equal(before.Inbox.Count, after.Inbox.Count);

        var advancedWith = withPreview.AdvanceDay();
        var advancedWithout = without.AdvanceDay();
        Assert.Equal(advancedWithout.RandomState, advancedWith.RandomState);
        Assert.Equal(advancedWithout.Season.CurrentDate, advancedWith.Season.CurrentDate);
    }

    [Fact]
    public void TeamStrengthWeightsForwardsDefenceAndGoalieFortyThirtyThirty()
    {
        var preview = Preview(2024);
        var started = Start(2024);

        foreach (var team in preview.Teams)
        {
            var (forwards, defence, goalie) = Strength(started.League.Teams.Single(t => t.Name == team.Name));

            Assert.Equal(Round(forwards), team.Forwards);
            Assert.Equal(Round(defence), team.Defence);
            Assert.Equal(Round(goalie), team.Goalies);
            Assert.Equal(Round((0.4 * forwards) + (0.3 * defence) + (0.3 * goalie)), team.Overall);
        }
    }

    [Fact]
    public void OutlookSplitsTheLeagueIntoQuartersByRank()
    {
        var teams = Preview(31).Teams;

        foreach (var outlook in Enum.GetValues<TeamOutlook>())
        {
            Assert.Equal(8, teams.Count(team => team.Outlook == outlook));
        }

        // Rounded overall is monotonic with the unrounded ranking, so each bucket sits at or
        // below the one before it.
        foreach (var outlook in Enum.GetValues<TeamOutlook>().Skip(1))
        {
            var better = teams.Where(team => team.Outlook < outlook).Min(team => team.Overall);
            var worse = teams.Where(team => team.Outlook == outlook).Max(team => team.Overall);
            Assert.True(better >= worse);
        }
    }

    [Fact]
    public void KeyPlayersAreTheBestCentreWingTwoDefenceAndStartingGoalie()
    {
        var preview = Preview(99);
        var started = Start(99);

        foreach (var team in preview.Teams)
        {
            var roster = started.League.Teams.Single(t => t.Name == team.Name).Roster;
            Assert.Equal(
                [Position.Centre, Position.Wing, Position.Defence, Position.Defence, Position.Goalie],
                team.KeyPlayers.Select(player => player.Position));

            int Best(Position position, int rank) =>
                roster.Where(player => player.Position == position)
                    .OrderByDescending(player => player.Overall).ElementAt(rank).Overall;

            Assert.Equal(Best(Position.Centre, 0), team.KeyPlayers[0].Overall);
            Assert.Equal(Best(Position.Wing, 0), team.KeyPlayers[1].Overall);
            Assert.Equal(Best(Position.Defence, 0), team.KeyPlayers[2].Overall);
            Assert.Equal(Best(Position.Defence, 1), team.KeyPlayers[3].Overall);
            Assert.All(team.KeyPlayers, key => Assert.Contains(
                roster,
                player => player.Number == key.Number && player.LastName == key.LastName));
        }
    }

    [Fact]
    public void AverageAgeIsTheRosterMeanOnOpeningDayToOneDecimal()
    {
        var preview = Preview(3);
        var started = Start(3);
        var openingDay = new DateOnly(Year, OpeningMonth, 1);

        foreach (var team in preview.Teams)
        {
            var roster = started.League.Teams.Single(t => t.Name == team.Name).Roster;
            var mean = roster.Average(player => (double)player.Biography.AgeOn(openingDay));
            Assert.Equal(Math.Round(mean, 1, MidpointRounding.AwayFromZero), team.AverageAge);
        }
    }

    [Fact]
    public void ManagerNameIsStoredTrimmedOnTheSnapshot()
    {
        Assert.Equal("Pat Jones", Start(1, "  Pat Jones  ").ManagerName);
    }

    [Fact]
    public void ManagerNameDefaultsWhenOmitted()
    {
        var snapshot = new GameManager().StartNewGame(new NewGameCommand(Year, new RandomState(1), Team));

        Assert.Equal(NewGameCommand.DefaultManagerName, snapshot.ManagerName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankManagerNameIsRejectedWithoutCreatingAGame(string name)
    {
        var manager = new GameManager();

        Assert.Throws<ArgumentException>(
            () => manager.StartNewGame(new NewGameCommand(Year, new RandomState(1), Team, name)));
        Assert.Throws<InvalidOperationException>(() => manager.GetSnapshot());
    }

    [Fact]
    public void TooLongManagerNameIsRejectedButTheMaximumIsAccepted()
    {
        var manager = new GameManager();
        var tooLong = new string('a', NewGameCommand.MaximumManagerNameLength + 1);
        var longest = new string('a', NewGameCommand.MaximumManagerNameLength);

        Assert.Throws<ArgumentException>(
            () => manager.StartNewGame(new NewGameCommand(Year, new RandomState(1), Team, tooLong)));
        Assert.Throws<InvalidOperationException>(() => manager.GetSnapshot());
        Assert.Equal(
            longest,
            manager.StartNewGame(new NewGameCommand(Year, new RandomState(1), Team, $" {longest} ")).ManagerName);
    }

    [Fact]
    public void ManagerNameIsKeptWhenTheManagedTeamChanges()
    {
        var manager = new GameManager();
        var started = manager.StartNewGame(new NewGameCommand(Year, new RandomState(1), Team, "Pat"));
        var other = started.League.Teams.First(team => team.Id != started.ManagedTeamId);

        Assert.Equal("Pat", manager.SelectManagedTeam(other.Id).ManagerName);
    }

    [Fact]
    public void WithFreshSeedUsesTheGivenManagerNameAndAnUnpredictableState()
    {
        var first = NewGameCommand.WithFreshSeed(Year, Team, "Pat");
        var second = NewGameCommand.WithFreshSeed(Year, Team);

        Assert.Equal("Pat", first.ManagerName);
        Assert.Equal(NewGameCommand.DefaultManagerName, second.ManagerName);
        Assert.NotEqual(first.RandomState, second.RandomState);
    }
}