using HockeySim.Domain;

using Xunit;

namespace HockeySim.Domain.Tests;

public sealed class InjuryTests
{
    private static readonly DateOnly MatchDate = new(2026, 10, 1);
    private static readonly PlayerId Player = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Fact]
    public void TheCatalogueDefinesEveryInjuryTypeOnce()
    {
        Assert.Equal(Enum.GetValues<InjuryType>(), InjuryCatalogue.All.Select(definition => definition.Type));
        Assert.All(Enum.GetValues<InjuryType>(), type => Assert.Equal(type, InjuryCatalogue.For(type).Type));
    }

    [Fact]
    public void EveryInjuryHealsInAPositiveRangeOfDays()
    {
        Assert.All(InjuryCatalogue.All, definition =>
        {
            Assert.InRange(definition.MinimumRecoveryDays, 1, definition.MaximumRecoveryDays);
            Assert.True(Enum.IsDefined(definition.BodyPart));
        });
    }

    [Fact]
    public void OnlyInjuriesThatCanBePlayedThroughReduceRatings()
    {
        Assert.All(InjuryCatalogue.All, definition =>
        {
            if (definition.CanPlayThrough)
            {
                Assert.NotEmpty(definition.RatingReductions);
                Assert.All(definition.RatingReductions.Values, points => Assert.True(points > 0));
            }
            else
            {
                Assert.Empty(definition.RatingReductions);
            }

            Assert.DoesNotContain(Rating.Durability, definition.RatingReductions.Keys);
        });
    }

    [Fact]
    public void TheCatalogueHasBothKindsOfInjuryForSkatersAndPlayThroughInjuriesForGoalies()
    {
        Assert.Contains(InjuryCatalogue.All, definition => !definition.CanPlayThrough);
        Assert.Contains(InjuryCatalogue.All, definition =>
            definition.CanPlayThrough && definition.RatingReductions.ContainsKey(Rating.GoalieReflex));
        Assert.Contains(InjuryCatalogue.For(InjuryType.Concussion).BodyPart, new[] { BodyPart.Head });
    }

    [Theory]
    [InlineData(InjuryType.Concussion, 2)]
    [InlineData(InjuryType.Concussion, 31)]
    [InlineData(InjuryType.BruisedFoot, 0)]
    public void ARecoveryTimeOutsideTheInjurysRangeIsRejected(InjuryType type, int days)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Injury(type, MatchDate, days));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MatchInjury(1, TimeSpan.FromMinutes(5), new TeamId(Guid.NewGuid()), Player, type, days));
    }

    [Fact]
    public void AnInjuryHealsAfterItsRecoveryDaysWhetherOrNotMatchesArePlayed()
    {
        var injury = new Injury(InjuryType.Concussion, MatchDate, recoveryDays: 7);

        Assert.True(injury.IsActiveOn(MatchDate));
        Assert.True(injury.IsActiveOn(MatchDate.AddDays(6)));
        Assert.False(injury.IsActiveOn(MatchDate.AddDays(7)));
        Assert.Equal(MatchDate.AddDays(7), injury.ReturnDate);
    }

    [Fact]
    public void TheExpectedReturnHoldsTheReturnDateWithinTheInjurysRange()
    {
        foreach (var definition in InjuryCatalogue.All)
        {
            for (var days = definition.MinimumRecoveryDays; days <= definition.MaximumRecoveryDays; days++)
            {
                var injury = new Injury(definition.Type, MatchDate, days);
                var expected = injury.ExpectedReturn;

                Assert.InRange(injury.ReturnDate, expected.Earliest, expected.Latest);
                Assert.True(expected.Earliest >= MatchDate.AddDays(definition.MinimumRecoveryDays));
                Assert.True(expected.Latest <= MatchDate.AddDays(definition.MaximumRecoveryDays));
            }
        }
    }

    [Fact]
    public void TheExpectedReturnIsAQuarterOfTheRecoveryTimeEitherSide()
    {
        var expected = new Injury(InjuryType.Concussion, MatchDate, recoveryDays: 20).ExpectedReturn;

        Assert.Equal(new ExpectedReturn(MatchDate.AddDays(15), MatchDate.AddDays(25)), expected);
    }

    [Fact]
    public void APlayerCannotPlayWithAnInjuryThatCannotBePlayedThroughUntilItHeals()
    {
        var health = PlayerHealth.Healthy(Player).Add(new Injury(InjuryType.SprainedKnee, MatchDate, 10));

        Assert.False(health.CanPlayOn(MatchDate.AddDays(1)));
        Assert.False(health.CanPlayOn(MatchDate.AddDays(9)));
        Assert.True(health.CanPlayOn(MatchDate.AddDays(10)));
        Assert.Empty(health.InjuriesOn(MatchDate.AddDays(10)));
    }

    [Fact]
    public void APlayerPlayingThroughInjuriesHasEachInjurysReductionsUntilItHeals()
    {
        var health = PlayerHealth.Healthy(Player)
            .Add(new Injury(InjuryType.SprainedAnkle, MatchDate, 4))
            .Add(new Injury(InjuryType.BruisedFoot, MatchDate.AddDays(2), 8));

        Assert.True(health.CanPlayOn(MatchDate.AddDays(3)));
        Assert.Equal(5 + 4, health.RatingReductionsOn(MatchDate.AddDays(3))[Rating.Skating]);
        Assert.Equal(4, health.RatingReductionsOn(MatchDate.AddDays(4))[Rating.Skating]);
        Assert.Empty(health.RatingReductionsOn(MatchDate.AddDays(10)));
    }

    [Fact]
    public void WearAccumulatesByBodyPartAndNeverRecovers()
    {
        var health = PlayerHealth.Healthy(Player)
            .AddWear(BodyPart.Shoulder, 3)
            .AddWear(BodyPart.Shoulder, 2)
            .AddWear(BodyPart.Foot, 1)
            .Add(new Injury(InjuryType.BruisedShoulder, MatchDate, 3));

        Assert.Equal(5, health.Wear(BodyPart.Shoulder));
        Assert.Equal(1, health.Wear(BodyPart.Foot));
        Assert.Equal(0, health.Wear(BodyPart.Head));
        Assert.Throws<ArgumentOutOfRangeException>(() => health.AddWear(BodyPart.Shoulder, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => health.AddWear(BodyPart.Shoulder, -1));
    }

    [Fact]
    public void AMatchsHealthChangesMustBeInTimeOrderWithOneWearEntryPerBodyPart()
    {
        var team = new TeamId(Guid.NewGuid());
        var late = new MatchInjury(2, TimeSpan.FromMinutes(1), team, Player, InjuryType.BruisedFoot, 3);
        var early = new MatchInjury(1, TimeSpan.FromMinutes(19), team, Player, InjuryType.BruisedKnee, 3);

        Assert.Throws<ArgumentException>(() => new MatchHealthChanges([late, early], []));
        Assert.Throws<ArgumentException>(() => new MatchHealthChanges(
            [],
            [new WearGain(Player, BodyPart.Knee, 1), new WearGain(Player, BodyPart.Knee, 2)]));
        Assert.Equal(2, new MatchHealthChanges([early, late], []).Injuries.Count);
    }
}