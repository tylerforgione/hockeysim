using Avalonia.Media;

using HockeySim.Desktop.Theme;

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
}