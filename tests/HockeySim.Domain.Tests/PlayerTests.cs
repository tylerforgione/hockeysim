using HockeySim.Domain;

using Xunit;

namespace HockeySim.Domain.Tests;

public sealed class PlayerTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void RatingScoreRejectsValuesOutsideItsRange(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RatingScore(value));
    }

    [Fact]
    public void PlayerRequiresEveryDefinedRating()
    {
        var ratings = CreateRatings();
        ratings.Remove(Rating.Skating);

        Assert.Throws<ArgumentException>(() => CreatePlayer(ratings));
    }

    [Fact]
    public void PlayerProtectsRatingsFromCallerMutation()
    {
        var inputRatings = CreateRatings();
        var player = CreatePlayer(inputRatings);
        inputRatings[Rating.Skating] = new RatingScore(99);

        Assert.Equal(50, player.GetRating(Rating.Skating).Value);

        var exposedRatings = Assert.IsAssignableFrom<IDictionary<Rating, RatingScore>>(player.Ratings);
        Assert.Throws<NotSupportedException>(
            () => exposedRatings[Rating.Skating] = new RatingScore(99));
    }

    [Fact]
    public void ForwardLineRejectsAPlayerInTheWrongPosition()
    {
        var wing = CreatePlayer(CreateRatings(), Position.Wing, 1);
        var centre = CreatePlayer(CreateRatings(), Position.Centre, 2);
        var goalie = CreatePlayer(CreateRatings(), Position.Goalie, 3);

        Assert.Throws<ArgumentException>(() => new ForwardLine(wing, centre, goalie));
    }

    private static Player CreatePlayer(
        IReadOnlyDictionary<Rating, RatingScore> ratings,
        Position position = Position.Centre,
        int number = 12) =>
        new(
            new PlayerId(Guid.Parse($"00000000-0000-0000-0000-{number:D12}")),
            "Test",
            "Player",
            position,
            24,
            number,
            ratings);

    private static Dictionary<Rating, RatingScore> CreateRatings() =>
        Enum.GetValues<Rating>().ToDictionary(rating => rating, _ => new RatingScore(50));
}