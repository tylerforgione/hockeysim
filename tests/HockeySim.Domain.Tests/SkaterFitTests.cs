using HockeySim.Domain;

using Xunit;

namespace HockeySim.Domain.Tests;

public sealed class SkaterFitTests
{
    [Theory]
    [InlineData(Position.Centre, SkaterRole.Centre, PositionFit.Natural)]
    [InlineData(Position.Wing, SkaterRole.Wing, PositionFit.Natural)]
    [InlineData(Position.Defence, SkaterRole.Defence, PositionFit.Natural)]
    [InlineData(Position.Centre, SkaterRole.Wing, PositionFit.OtherForwardPosition)]
    [InlineData(Position.Wing, SkaterRole.Centre, PositionFit.OtherForwardPosition)]
    [InlineData(Position.Centre, SkaterRole.Defence, PositionFit.AcrossForwardsAndDefence)]
    [InlineData(Position.Wing, SkaterRole.Defence, PositionFit.AcrossForwardsAndDefence)]
    [InlineData(Position.Defence, SkaterRole.Centre, PositionFit.AcrossForwardsAndDefence)]
    [InlineData(Position.Defence, SkaterRole.Wing, PositionFit.AcrossForwardsAndDefence)]
    public void PositionFitComparesNaturalPositionWithRole(Position position, SkaterRole role, PositionFit expected)
    {
        Assert.Equal(expected, SkaterFit.For(position, role));
    }

    [Fact]
    public void NoSkaterRoleSuitsAGoalie()
    {
        Assert.Throws<ArgumentException>(() => SkaterFit.For(Position.Goalie, SkaterRole.Wing));
    }

    [Theory]
    [InlineData(Handedness.Left, SkaterSide.Left, false)]
    [InlineData(Handedness.Right, SkaterSide.Right, false)]
    [InlineData(Handedness.Left, SkaterSide.Right, true)]
    [InlineData(Handedness.Right, SkaterSide.Left, true)]
    public void ASkaterIsOffHandOnTheSideOppositeTheirShot(Handedness handedness, SkaterSide side, bool expected)
    {
        Assert.Equal(expected, SkaterFit.IsOffHand(handedness, side));
    }

    [Fact]
    public void UnitSlotsHaveSidesOnlyForPairedWingsAndDefence()
    {
        Assert.Equal(
            [SkaterSide.Left, null, SkaterSide.Right, SkaterSide.Left, SkaterSide.Right],
            SpecialSituationFormat.For(SpecialSituation.PowerPlay5On4).Sides);
        Assert.Equal(
            [null, null, SkaterSide.Left, SkaterSide.Right],
            SpecialSituationFormat.For(SpecialSituation.FourOnFour).Sides);
        Assert.Equal(
            [null, null, null],
            SpecialSituationFormat.For(SpecialSituation.ThreeOnThree).Sides);
        Assert.All(SpecialSituationFormat.All, format => Assert.Equal(format.Roles.Count, format.Sides.Count));
    }
}