using HockeySim.Domain;

using Xunit;

namespace HockeySim.Domain.Tests;

public sealed class OverallRatingTests
{
    [Theory]
    [MemberData(nameof(Positions))]
    public void EveryPositionWeighsEveryRatingWithATotalOfOneHundred(Position position)
    {
        var weights = OverallRating.GetWeights(position);

        Assert.Equal(Enum.GetValues<Rating>().Order(), weights.Keys.Order());
        Assert.All(weights.Values, weight => Assert.True(weight >= 0));
        Assert.Equal(100, weights.Values.Sum());
    }

    [Theory]
    [MemberData(nameof(Positions))]
    public void DurabilityNeverAffectsTheOverall(Position position)
    {
        var fragile = CreateRatings(70);
        fragile[Rating.Durability] = new RatingScore(0);
        var durable = CreateRatings(70);
        durable[Rating.Durability] = new RatingScore(100);

        Assert.Equal(0, OverallRating.GetWeights(position)[Rating.Durability]);
        Assert.Equal(OverallRating.Calculate(position, fragile), OverallRating.Calculate(position, durable));
    }

    [Theory]
    [MemberData(nameof(PositionsAndUniformValues))]
    public void APlayerRatedTheSameEverywhereHasThatOverall(Position position, int value)
    {
        Assert.Equal(new RatingScore(value), OverallRating.Calculate(position, CreateRatings(value)));
    }

    [Theory]
    [InlineData(Position.Centre, Rating.Faceoffs, 8)]
    [InlineData(Position.Wing, Rating.Faceoffs, 0)]
    [InlineData(Position.Wing, Rating.ShotAccuracy, 13)]
    [InlineData(Position.Defence, Rating.DefensiveAwareness, 17)]
    [InlineData(Position.Defence, Rating.ShotBlocking, 10)]
    [InlineData(Position.Goalie, Rating.GoalieReflex, 40)]
    [InlineData(Position.Goalie, Rating.Stamina, 5)]
    [InlineData(Position.Goalie, Rating.Skating, 0)]
    public void ASingleMaximumRatingContributesItsDocumentedWeight(Position position, Rating rating, int weight)
    {
        var ratings = CreateRatings(0);
        ratings[rating] = new RatingScore(100);

        Assert.Equal(new RatingScore(weight), OverallRating.Calculate(position, ratings));
    }

    [Fact]
    public void AGoaliesSkaterRatingsDoNotAffectTheirOverall()
    {
        var ratings = CreateRatings(0);
        foreach (var rating in new[] { Rating.GoalieReflex, Rating.GoaliePositioning, Rating.GoalieReboundControl, Rating.Stamina })
        {
            ratings[rating] = new RatingScore(80);
        }

        Assert.Equal(new RatingScore(80), OverallRating.Calculate(Position.Goalie, ratings));
    }

    [Fact]
    public void AHalfPointOverallRoundsUp()
    {
        // Discipline carries 2% for a centre: 0.02 * 25 lifts an even 50 to exactly 50.5.
        var ratings = CreateRatings(50);
        ratings[Rating.Discipline] = new RatingScore(75);

        Assert.Equal(new RatingScore(51), OverallRating.Calculate(Position.Centre, ratings));
    }

    [Fact]
    public void APlayersOverallIsCalculatedForTheirPosition()
    {
        var ratings = CreateRatings(60);
        ratings[Rating.Faceoffs] = new RatingScore(100);
        ratings[Rating.GoalieReflex] = new RatingScore(100);

        var centre = CreatePlayer(Position.Centre, ratings);
        var wing = CreatePlayer(Position.Wing, ratings);
        var goalie = CreatePlayer(Position.Goalie, ratings);

        Assert.Equal(OverallRating.Calculate(Position.Centre, ratings), centre.Overall);
        Assert.Equal(new RatingScore(63), centre.Overall);
        Assert.Equal(new RatingScore(60), wing.Overall);
        Assert.Equal(new RatingScore(76), goalie.Overall);
    }

    [Fact]
    public void AnUndefinedPositionHasNoWeights()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OverallRating.GetWeights((Position)99));
    }

    public static TheoryData<Position> Positions() => new(Enum.GetValues<Position>());

    public static TheoryData<Position, int> PositionsAndUniformValues()
    {
        var data = new TheoryData<Position, int>();
        foreach (var position in Enum.GetValues<Position>())
        {
            data.Add(position, 0);
            data.Add(position, 67);
            data.Add(position, 100);
        }

        return data;
    }

    private static Dictionary<Rating, RatingScore> CreateRatings(int value) =>
        Enum.GetValues<Rating>().ToDictionary(rating => rating, _ => new RatingScore(value));

    private static Player CreatePlayer(Position position, IReadOnlyDictionary<Rating, RatingScore> ratings) =>
        new(new PlayerId(Guid.Parse("00000000-0000-0000-0000-000000000001")), "Test", "Player", position, TestBiography.Create(), 12, ratings);
}