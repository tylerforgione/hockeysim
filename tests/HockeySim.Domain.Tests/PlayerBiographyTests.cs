using HockeySim.Domain;

using Xunit;

namespace HockeySim.Domain.Tests;

public sealed class PlayerBiographyTests
{
    [Theory]
    [InlineData("2026-03-14", 24)]
    [InlineData("2026-03-15", 25)]
    [InlineData("2026-12-31", 25)]
    [InlineData("2001-03-15", 0)]
    public void AgeCountsCompletedYearsAndTurnsOverOnTheBirthday(string date, int expectedAge)
    {
        var player = LineupTests.CreatePlayer(Position.Centre, 1);

        Assert.Equal(expectedAge, player.AgeOn(DateOnly.Parse(date)));
    }

    [Theory]
    [InlineData("2027-02-27", 22)]
    [InlineData("2027-02-28", 23)]
    [InlineData("2028-02-28", 23)]
    [InlineData("2028-02-29", 24)]
    public void ALeapDayBirthdayFallsOn28FebruaryInCommonYears(string date, int expectedAge)
    {
        var biography = TestBiography.Create(new DateOnly(2004, 2, 29));

        Assert.Equal(expectedAge, biography.AgeOn(DateOnly.Parse(date)));
    }

    [Fact]
    public void APlayerHasNoAgeBeforeTheirBirthDate()
    {
        var biography = TestBiography.Create(new DateOnly(2001, 3, 15));

        Assert.Throws<ArgumentOutOfRangeException>(() => biography.AgeOn(new DateOnly(2001, 3, 14)));
    }

    [Theory]
    [InlineData(Country.Canada, "Ontario")]
    [InlineData(Country.UnitedStates, "Minnesota")]
    [InlineData(Country.Sweden, null)]
    [InlineData(Country.Czechia, null)]
    public void OnlyCanadaAndTheUnitedStatesNameAStateOrProvince(Country country, string? region)
    {
        var birthplace = new Birthplace("Somewhere", region, country);

        Assert.Equal(region, birthplace.Region);
    }

    [Theory]
    [InlineData(Country.Canada, null)]
    [InlineData(Country.UnitedStates, " ")]
    [InlineData(Country.Sweden, "Uppland")]
    public void ABirthRegionIsRequiredExactlyWhereTheCountryUsesThem(Country country, string? region)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Birthplace("Somewhere", region, country));
    }

    [Fact]
    public void ABirthplaceNeedsACityAndADefinedCountry()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Birthplace(" ", null, Country.Finland));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Birthplace("Somewhere", null, (Country)99));
    }

    [Theory]
    [InlineData(59)]
    [InlineData(85)]
    public void HeightRejectsValuesOutsideItsRange(int inches)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Height(inches));
    }

    [Theory]
    [InlineData(119)]
    [InlineData(321)]
    public void WeightRejectsValuesOutsideItsRange(int pounds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Weight(pounds));
    }

    [Fact]
    public void HeightAndWeightAcceptTheirEdges()
    {
        Assert.Equal(60, new Height(60).Inches);
        Assert.Equal(84, new Height(84).Inches);
        Assert.Equal(120, new Weight(120).Pounds);
        Assert.Equal(320, new Weight(320).Pounds);
    }

    [Fact]
    public void ABiographyRejectsMissingOrUndefinedDetails()
    {
        var birthDate = new DateOnly(2001, 3, 15);
        var birthplace = new Birthplace("Toronto", "Ontario", Country.Canada);
        var height = new Height(73);
        var weight = new Weight(195);

        Assert.Throws<ArgumentNullException>(() =>
            new PlayerBiography(birthDate, null!, Country.Canada, Handedness.Left, height, weight));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PlayerBiography(birthDate, birthplace, (Country)99, Handedness.Left, height, weight));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PlayerBiography(birthDate, birthplace, Country.Canada, (Handedness)2, height, weight));
        Assert.Throws<ArgumentException>(() =>
            new PlayerBiography(birthDate, birthplace, Country.Canada, Handedness.Left, default, weight));
        Assert.Throws<ArgumentException>(() =>
            new PlayerBiography(birthDate, birthplace, Country.Canada, Handedness.Left, height, default));
    }

    [Fact]
    public void NationalityMayDifferFromTheBirthCountry()
    {
        var biography = new PlayerBiography(
            new DateOnly(2001, 3, 15),
            new Birthplace("Buffalo", "New York", Country.UnitedStates),
            Country.Canada,
            Handedness.Right,
            new Height(74),
            new Weight(201));

        Assert.Equal(Country.UnitedStates, biography.Birthplace.Country);
        Assert.Equal(Country.Canada, biography.Nationality);
    }
}