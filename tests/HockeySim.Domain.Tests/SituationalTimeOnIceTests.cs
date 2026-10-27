using HockeySim.Domain;

using Xunit;

namespace HockeySim.Domain.Tests;

public sealed class SituationalTimeOnIceTests
{
    private static readonly SituationalTimeOnIce Sample = new(
        TimeSpan.FromSeconds(600), TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(5));

    [Fact]
    public void AllIsTheSumOfEverySituation()
    {
        Assert.Equal(TimeSpan.FromSeconds(740), Sample.All);
        Assert.Equal(TimeSpan.Zero, SituationalTimeOnIce.None.All);
    }

    [Fact]
    public void TheIndexerReturnsTheTimeForEachSituation()
    {
        Assert.Equal(Sample.FiveOnFive, Sample[StrengthSituation.FiveOnFive]);
        Assert.Equal(Sample.PowerPlay, Sample[StrengthSituation.PowerPlay]);
        Assert.Equal(Sample.PenaltyKill, Sample[StrengthSituation.PenaltyKill]);
        Assert.Equal(Sample.Other, Sample[StrengthSituation.Other]);
        Assert.Throws<ArgumentOutOfRangeException>(() => Sample[(StrengthSituation)99]);
    }

    [Fact]
    public void AddSumsEachSituation()
    {
        var total = Sample.Add(Sample);

        Assert.Equal(TimeSpan.FromSeconds(1200), total.FiveOnFive);
        Assert.Equal(TimeSpan.FromSeconds(180), total.PowerPlay);
        Assert.Equal(TimeSpan.FromSeconds(90), total.PenaltyKill);
        Assert.Equal(TimeSpan.FromSeconds(10), total.Other);
        Assert.Throws<ArgumentNullException>(() => Sample.Add(null!));
    }

    [Fact]
    public void TimeMustBeWholeSecondsAndNotNegative()
    {
        var zero = TimeSpan.Zero;

        Assert.Throws<ArgumentOutOfRangeException>(() => new SituationalTimeOnIce(TimeSpan.FromSeconds(-1), zero, zero, zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SituationalTimeOnIce(zero, TimeSpan.FromMilliseconds(1500), zero, zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SituationalTimeOnIce(zero, zero, TimeSpan.FromSeconds(-5), zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SituationalTimeOnIce(zero, zero, zero, TimeSpan.FromTicks(1)));
    }
}