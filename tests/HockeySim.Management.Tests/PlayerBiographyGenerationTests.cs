using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.NewGame;
using HockeySim.Simulation.Randomness;

using Xunit;

namespace HockeySim.Management.Tests;

/// <summary>
/// Checks that generated biographies resemble an NHL population. Each league has 736 players, so
/// shares and averages are stable for a fixed seed; the bands are wide enough that retuning the
/// generator need not break them.
/// </summary>
public sealed class PlayerBiographyGenerationTests
{
    private const int SeasonYear = 2026;
    private static readonly DateOnly OpeningDay = new(SeasonYear, 10, 1);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheNationalityMixApproximatesTheNhl(ulong seed)
    {
        var players = StartGame(seed).Players;

        var canada = Share(players, player => player.Biography.Nationality == Country.Canada);
        var unitedStates = Share(players, player => player.Biography.Nationality == Country.UnitedStates);
        var sweden = Share(players, player => player.Biography.Nationality == Country.Sweden);

        Assert.InRange(canada, 0.33, 0.49);
        Assert.InRange(unitedStates, 0.20, 0.34);
        Assert.InRange(sweden, 0.04, 0.13);
        Assert.True(players.Select(player => player.Biography.Nationality).Distinct().Count() >= 8);
    }

    [Fact]
    public void NamesFitTheNationality()
    {
        // These nations share no family names, so a name drawn for the wrong nationality would
        // show up in two sets. Neighbouring nations (Norway and Denmark, Germany and Austria) do
        // share some names and are left out.
        Country[] distinctNations =
        [
            Country.Canada, Country.UnitedStates, Country.Sweden, Country.Finland,
            Country.Russia, Country.Czechia, Country.Slovakia, Country.Latvia,
        ];
        var players = Seeds().SelectMany(seed => StartGame(seed).Players).ToList();
        var lastNames = distinctNations.ToDictionary(
            country => country,
            country => players
                .Where(player => player.Biography.Nationality == country)
                .Select(player => player.LastName)
                .ToHashSet());

        foreach (var country in distinctNations)
        {
            Assert.NotEmpty(lastNames[country]);
            foreach (var other in distinctNations.Where(other => other != country))
            {
                Assert.Empty(lastNames[country].Intersect(lastNames[other]));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void MostPlayersAreBornInTheCountryTheyRepresentAndSomeAbroad(ulong seed)
    {
        var players = StartGame(seed).Players;

        var bornAtHome = Share(players, player => player.Biography.Birthplace.Country == player.Biography.Nationality);

        Assert.InRange(bornAtHome, 0.88, 0.99);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void BirthplacesNameAStateOrProvinceOnlyInCanadaAndTheUnitedStates(ulong seed)
    {
        var birthplaces = StartGame(seed).Players.Select(player => player.Biography.Birthplace).ToList();

        Assert.All(birthplaces, birthplace =>
        {
            Assert.False(string.IsNullOrWhiteSpace(birthplace.City));
            Assert.Equal(
                birthplace.Country is Country.Canada or Country.UnitedStates,
                birthplace.Region is not null);
        });
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void MostPlayersShootOrCatchLeftAndGoaliesMostOfAll(ulong seed)
    {
        var players = StartGame(seed).Players;

        foreach (var position in Enum.GetValues<Position>())
        {
            var atPosition = players.Where(player => player.Position == position).ToList();
            Assert.Contains(atPosition, player => player.Biography.Handedness == Handedness.Right);
        }

        var skatersLeft = Share(
            players.Where(player => player.Position != Position.Goalie),
            player => player.Biography.Handedness == Handedness.Left);
        var goaliesLeft = Share(
            players.Where(player => player.Position == Position.Goalie),
            player => player.Biography.Handedness == Handedness.Left);

        Assert.InRange(skatersLeft, 0.5, 0.75);
        Assert.InRange(goaliesLeft, 0.75, 0.98);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void DefenceAndGoaliesAreTallerAndWeightFollowsHeight(ulong seed)
    {
        var players = StartGame(seed).Players;

        var centres = AverageHeight(players, Position.Centre);
        Assert.True(AverageHeight(players, Position.Defence) > centres + 0.5);
        Assert.True(AverageHeight(players, Position.Goalie) > centres + 1);
        Assert.InRange(players.Average(player => player.Biography.Height.Inches), 71, 76);
        Assert.InRange(players.Average(player => player.Biography.Weight.Pounds), 185, 215);

        var medianHeight = players.Select(player => player.Biography.Height.Inches).Order().ElementAt(players.Count / 2);
        var tallWeight = players.Where(player => player.Biography.Height.Inches > medianHeight).Average(Pounds);
        var shortWeight = players.Where(player => player.Biography.Height.Inches < medianHeight).Average(Pounds);
        Assert.True(tallWeight > shortWeight + 10, $"Taller {tallWeight:F1} lb, shorter {shortWeight:F1} lb.");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void AgesOnOpeningDaySpreadAroundTheMidTwenties(ulong seed)
    {
        var snapshot = StartGame(seed);

        Assert.Equal(OpeningDay, snapshot.Game.Season.CurrentDate);
        Assert.All(snapshot.Players, player =>
        {
            Assert.InRange(player.Age, 18, 40);
            Assert.Equal(player.Age, Age(player.Biography.BirthDate, OpeningDay));
        });
        Assert.InRange(snapshot.Players.Average(player => player.Age), 24, 30);
        Assert.Contains(snapshot.Players, player => player.Age <= 20);
        Assert.Contains(snapshot.Players, player => player.Age >= 35);
    }

    [Fact]
    public void PlayersAgeAsTheSeasonPasses()
    {
        var manager = new GameManager();
        var opening = manager.StartNewGame(new NewGameCommand(SeasonYear, new RandomState(42), "Halifax Mariners"));
        var birthdayPlayer = opening.League.Teams
            .SelectMany(team => team.Roster)
            .First(player => player.Biography.BirthDate is { Month: 10, Day: > 2 and <= 12 });
        var birthday = new DateOnly(SeasonYear, 10, birthdayPlayer.Biography.BirthDate.Day);

        var dayBefore = AdvanceTo(manager, birthday.AddDays(-1));
        var onBirthday = AdvanceTo(manager, birthday);

        Assert.Equal(birthdayPlayer.Age, Find(dayBefore, birthdayPlayer.Id).Age);
        Assert.Equal(birthdayPlayer.Age + 1, Find(onBirthday, birthdayPlayer.Id).Age);
        Assert.Equal(birthdayPlayer.Biography, Find(onBirthday, birthdayPlayer.Id).Biography);
    }

    [Fact]
    public void TheSameSeedGeneratesTheSameBiographies()
    {
        var first = StartGame(42).Players;
        var second = StartGame(42).Players;

        Assert.Equal(first.Select(player => player.Biography), second.Select(player => player.Biography));
        Assert.NotEqual(first.Select(player => player.Biography), StartGame(7).Players.Select(player => player.Biography));
    }

    public static TheoryData<ulong> Seeds() => new(7UL, 42UL, 2026UL);

    private static (GameSnapshot Game, List<PlayerSnapshot> Players) StartGame(ulong seed)
    {
        // Ages are generated for opening day, so a player can still be 17 in the preseason.
        var manager = new GameManager();
        manager.StartNewGame(new NewGameCommand(SeasonYear, new RandomState(seed), "Halifax Mariners"));
        var game = manager.PlayPreseason();
        return (game, game.League.Teams.SelectMany(team => team.Roster).ToList());
    }

    private static GameSnapshot AdvanceTo(GameManager manager, DateOnly date)
    {
        var snapshot = manager.AdvanceDayReplacingInjured();
        while (snapshot.Season.CurrentDate < date)
        {
            snapshot = manager.AdvanceDayReplacingInjured();
        }

        return snapshot;
    }

    private static PlayerSnapshot Find(GameSnapshot snapshot, PlayerId playerId) =>
        snapshot.League.Teams.SelectMany(team => team.Roster).Single(player => player.Id == playerId);

    private static int Age(DateOnly birthDate, DateOnly date) =>
        date.Year - birthDate.Year - (date < birthDate.AddYears(date.Year - birthDate.Year) ? 1 : 0);

    private static double Share(IEnumerable<PlayerSnapshot> players, Func<PlayerSnapshot, bool> predicate)
    {
        var list = players.ToList();
        return list.Count(predicate) / (double)list.Count;
    }

    private static double AverageHeight(IEnumerable<PlayerSnapshot> players, Position position) =>
        players.Where(player => player.Position == position).Average(player => player.Biography.Height.Inches);

    private static double Pounds(PlayerSnapshot player) => player.Biography.Weight.Pounds;
}