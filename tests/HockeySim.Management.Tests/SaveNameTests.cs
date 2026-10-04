using HockeySim.Management.Saves;

using Xunit;

namespace HockeySim.Management.Tests;

public sealed class SaveNameTests
{
    [Theory]
    [InlineData("Halifax Dynasty", "Halifax Dynasty")]
    [InlineData("  Season 2 (rebuild)  ", "Season 2 (rebuild)")]
    [InlineData("Montréal & Co., v1.2!", "Montréal & Co., v1.2!")]
    [InlineData("O'Reilly_Era-3", "O'Reilly_Era-3")]
    public void AcceptsPortableNamesWithoutSurroundingSpaces(string text, string expected)
    {
        Assert.True(SaveName.TryParse(text, out var name, out var error));
        Assert.Null(error);
        Assert.Equal(expected, name.Value);
    }

    [Theory]
    [InlineData("", "Enter a name")]
    [InlineData("   ", "Enter a name")]
    [InlineData(null, "Enter a name")]
    [InlineData("Season/2", "can use letters")]
    [InlineData("What?", "can use letters")]
    [InlineData("a:b", "can use letters")]
    [InlineData("Dynasty.", "full stop")]
    [InlineData(".hidden", "full stop")]
    [InlineData("con", "reserved")]
    [InlineData("LPT1", "reserved")]
    public void RejectsNamesThatAreNotSafeAsFileNamesWithAReason(string? text, string reason)
    {
        Assert.False(SaveName.TryParse(text, out var name, out var error));
        Assert.Null(name);
        Assert.Contains(reason, error, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => SaveName.Parse(text!));
    }

    [Fact]
    public void RejectsNamesLongerThanTheMaximum()
    {
        Assert.True(SaveName.TryParse(new string('a', SaveName.MaximumLength), out _, out _));
        Assert.False(SaveName.TryParse(new string('a', SaveName.MaximumLength + 1), out _, out var error));
        Assert.Contains($"{SaveName.MaximumLength} characters", error, StringComparison.Ordinal);
    }

    [Fact]
    public void NamesDifferingOnlyInCaseAreTheSameSave()
    {
        var name = SaveName.Parse("Dynasty");

        Assert.Equal(name, SaveName.Parse("DYNASTY"));
        Assert.Equal(name.GetHashCode(), SaveName.Parse("dynasty").GetHashCode());
        Assert.NotEqual(name, SaveName.Parse("Dynasty 2"));
    }

    [Fact]
    public void DecomposedAccentsAreReadAsTheSameName()
    {
        // macOS can report "é" in a file name as "e" followed by a combining accent.
        Assert.Equal("Montréal", SaveName.Parse("Montréal").Value);
    }
}