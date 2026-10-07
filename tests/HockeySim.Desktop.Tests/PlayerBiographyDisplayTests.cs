using HockeySim.Desktop.Players;
using HockeySim.Desktop.Roster;
using HockeySim.Domain;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class PlayerBiographyDisplayTests
{
    [Theory]
    [InlineData(72, "6' 0\"")]
    [InlineData(73, "6' 1\"")]
    [InlineData(71, "5' 11\"")]
    public void HeightIsShownInFeetAndInches(int inches, string expected)
    {
        Assert.Equal(expected, PlayerDisplay.FormatHeight(new Height(inches)));
    }

    [Fact]
    public void WeightIsShownInPounds()
    {
        Assert.Equal("195 lb", PlayerDisplay.FormatWeight(new Weight(195)));
    }

    [Fact]
    public void ABirthplaceShowsItsRegionOnlyWhereTheCountryHasOne()
    {
        Assert.Equal(
            "Toronto, Ontario, Canada",
            PlayerDisplay.FormatBirthplace(new Birthplace("Toronto", "Ontario", Country.Canada)));
        Assert.Equal(
            "Malmö, Sweden",
            PlayerDisplay.FormatBirthplace(new Birthplace("Malmö", null, Country.Sweden)));
    }

    [Fact]
    public void EveryCountryHasADistinctThreeLetterCodeAndAName()
    {
        var countries = Enum.GetValues<Country>();
        var codes = countries.Select(PlayerDisplay.CountryCode).ToList();

        Assert.All(codes, code => Assert.Matches("^[A-Z]{3}$", code));
        Assert.Equal(countries.Length, codes.Distinct().Count());
        Assert.Equal(countries.Length, countries.Select(PlayerDisplay.CountryName).Distinct().Count());
        Assert.Equal("SUI", PlayerDisplay.CountryCode(Country.Switzerland));
    }

    [Fact]
    public void RosterRowsShowNationalityAndHandedness()
    {
        var session = GameTestData.StartSession();
        var team = session.ManagedTeam;
        var roster = new TeamRosterViewModel(session, team.Id);

        Assert.All(roster.Skaters.Concat(roster.Goalies), row =>
        {
            var biography = row.Player.Biography;
            Assert.Equal(PlayerDisplay.CountryCode(biography.Nationality), row.Nationality);
            Assert.Equal(PlayerDisplay.CountryName(biography.Nationality), row.NationalityName);
            Assert.Equal(biography.Handedness == Handedness.Left ? "L" : "R", row.Handedness);
            Assert.Equal(row.Player.Age, row.Age);
        });
    }

    [Fact]
    public void TheProfileShowsTheBiographyAndWhetherThePlayerShootsOrCatches()
    {
        var session = GameTestData.StartSession();
        var team = session.ManagedTeam;
        var skater = team.Roster.First(player => player.Position != Position.Goalie);
        var goalie = team.Roster.First(player => player.Position == Position.Goalie);

        var skaterDetail = new PlayerDetailViewModel(skater, team, session.GetSeasonTotals(skater.Id), 2026);
        var goalieDetail = new PlayerDetailViewModel(goalie, team, session.GetSeasonTotals(goalie.Id), 2026);

        Assert.Equal("SHOOTS", skaterDetail.HandednessLabel);
        Assert.Equal("CATCHES", goalieDetail.HandednessLabel);
        Assert.Equal(PlayerDisplay.HandednessName(skater.Biography.Handedness), skaterDetail.Handedness);
        Assert.Equal(PlayerDisplay.FormatHeight(skater.Biography.Height), skaterDetail.Height);
        Assert.Equal($"{skater.Biography.Weight.Pounds} lb", skaterDetail.Weight);
        Assert.Equal(PlayerDisplay.FormatBirthDate(skater.Biography.BirthDate), skaterDetail.BirthDate);
        Assert.StartsWith(skater.Biography.Birthplace.City, skaterDetail.Birthplace);
        Assert.EndsWith(PlayerDisplay.CountryName(skater.Biography.Birthplace.Country), skaterDetail.Birthplace);
        Assert.Equal(PlayerDisplay.CountryName(skater.Biography.Nationality), skaterDetail.Nationality);
    }
}