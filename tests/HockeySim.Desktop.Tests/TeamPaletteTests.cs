using Avalonia.Media;

using HockeySim.Desktop.Theme;
using HockeySim.Domain;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class TeamPaletteTests
{
    [Theory]
    [InlineData("#0B2545")]
    [InlineData("#000000")]
    [InlineData("#C8102E")]
    public void TextOnADarkPrimaryIsLight(string primary)
    {
        Assert.Equal(Colors.White, TeamPalette.ReadableTextOn(Color.Parse(primary)));
    }

    [Theory]
    [InlineData("#F2B705")]
    [InlineData("#FFFFFF")]
    public void TextOnALightPrimaryIsDark(string primary)
    {
        Assert.Equal(Color.Parse("#0D1014"), TeamPalette.ReadableTextOn(Color.Parse(primary)));
    }

    [Fact]
    public void ChartsUseEachTeamsPrimaryWhenItShowsOnTheDarkPanels()
    {
        var detroit = Colours("#C8102E", "#F2F2F2");
        var seattle = Colours("#0B4F3C", "#99D9C1");

        Assert.Equal((detroit.Primary, seattle.Primary), TeamPalette.ChartColours(detroit, seattle));
    }

    [Fact]
    public void ChartsUseTheSecondaryForAPrimaryTooDarkToSee()
    {
        var pittsburgh = Colours("#111111", "#FFC72C");
        var toronto = Colours("#00205B", "#A7B1BC");

        Assert.Equal((pittsburgh.Secondary, toronto.Secondary), TeamPalette.ChartColours(pittsburgh, toronto));
    }

    [Fact]
    public void TheHomeTeamSwitchesColourRatherThanLookLikeTheAwayTeam()
    {
        var pittsburgh = Colours("#111111", "#FFC72C");
        var nashville = Colours("#F2B705", "#041E42");
        var atlanta = Colours("#7A1915", "#F5A623");

        Assert.Equal((pittsburgh.Secondary, atlanta.Primary), TeamPalette.ChartColours(pittsburgh, atlanta));
        // Nashville's navy secondary cannot be seen, so its gold stays even beside Pittsburgh's.
        Assert.Equal((pittsburgh.Secondary, nashville.Primary), TeamPalette.ChartColours(pittsburgh, nashville));
    }

    private static TeamColours Colours(string primary, string secondary) => new(Colour.FromHex(primary), Colour.FromHex(secondary));
}