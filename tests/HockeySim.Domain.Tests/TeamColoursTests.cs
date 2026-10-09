using Xunit;

namespace HockeySim.Domain.Tests;

public sealed class TeamColoursTests
{
    [Theory]
    [InlineData("#0B2545", 0x0B, 0x25, 0x45)]
    [InlineData("#ffffff", 0xFF, 0xFF, 0xFF)]
    [InlineData("#000000", 0x00, 0x00, 0x00)]
    public void AColourReadsFromHex(string hex, byte red, byte green, byte blue)
    {
        Assert.Equal(new Colour(red, green, blue), Colour.FromHex(hex));
    }

    [Fact]
    public void AColourWritesAsUppercaseHex()
    {
        Assert.Equal("#0B2545", Colour.FromHex("#0b2545").ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("0B2545")]
    [InlineData("#0B254")]
    [InlineData("#0B25456")]
    [InlineData("#0B25G5")]
    [InlineData("#-B2545")]
    [InlineData("navy")]
    public void AColourRejectsTextThatIsNotRrggbb(string hex)
    {
        Assert.Throws<ArgumentException>(() => Colour.FromHex(hex));
    }

    [Fact]
    public void ATeamsPrimaryAndSecondaryColoursMustDiffer()
    {
        var navy = Colour.FromHex("#0B2545");

        Assert.Throws<ArgumentException>(() => new TeamColours(navy, navy));
    }
}