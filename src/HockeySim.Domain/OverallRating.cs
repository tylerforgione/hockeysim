using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// Summarises a player's ratings as one 0-100 value for their position: a weighted mean of the
/// ratings that matter to the position, rounded to the nearest whole number.
/// </summary>
/// <remarks>
/// Weights are whole percentages that total 100 for each position, so a player rated the same
/// in every weighted rating has that value as their overall. Ratings a position does not use
/// (a wing's faceoffs, a goalie's skater ratings) carry no weight. Durability is hidden from the
/// user and never carries weight, so the overall reveals nothing about it. The weights are
/// documented in <c>docs/areas/players-and-lineups.md</c> under "Player ratings"; keep both in step.
/// </remarks>
public static class OverallRating
{
    private static readonly ReadOnlyDictionary<Position, ReadOnlyDictionary<Rating, int>> Weights =
        new(new Dictionary<Position, ReadOnlyDictionary<Rating, int>>
        {
            [Position.Centre] = CreateWeights(new()
            {
                [Rating.Skating] = 14,
                [Rating.ShotPower] = 6,
                [Rating.ShotAccuracy] = 10,
                [Rating.PuckControl] = 11,
                [Rating.Passing] = 11,
                [Rating.OffensiveAwareness] = 13,
                [Rating.DefensiveAwareness] = 9,
                [Rating.Checking] = 3,
                [Rating.ShotBlocking] = 2,
                [Rating.StickChecking] = 5,
                [Rating.Faceoffs] = 8,
                [Rating.Discipline] = 2,
                [Rating.Stamina] = 4,
                [Rating.Toughness] = 2,
            }),
            [Position.Wing] = CreateWeights(new()
            {
                [Rating.Skating] = 15,
                [Rating.ShotPower] = 9,
                [Rating.ShotAccuracy] = 13,
                [Rating.PuckControl] = 11,
                [Rating.Passing] = 9,
                [Rating.OffensiveAwareness] = 14,
                [Rating.DefensiveAwareness] = 7,
                [Rating.Checking] = 4,
                [Rating.ShotBlocking] = 2,
                [Rating.StickChecking] = 5,
                [Rating.Discipline] = 2,
                [Rating.Stamina] = 5,
                [Rating.Toughness] = 4,
            }),
            [Position.Defence] = CreateWeights(new()
            {
                [Rating.Skating] = 14,
                [Rating.ShotPower] = 6,
                [Rating.ShotAccuracy] = 3,
                [Rating.PuckControl] = 6,
                [Rating.Passing] = 9,
                [Rating.OffensiveAwareness] = 6,
                [Rating.DefensiveAwareness] = 17,
                [Rating.Checking] = 9,
                [Rating.ShotBlocking] = 10,
                [Rating.StickChecking] = 9,
                [Rating.Discipline] = 3,
                [Rating.Stamina] = 5,
                [Rating.Toughness] = 3,
            }),
            [Position.Goalie] = CreateWeights(new()
            {
                [Rating.GoalieReflex] = 40,
                [Rating.GoaliePositioning] = 35,
                [Rating.GoalieReboundControl] = 20,
                [Rating.Stamina] = 5,
            }),
        });

    /// <summary>
    /// The percentage weight of every rating for a position. Every defined rating is present;
    /// ratings the position does not use have a weight of zero.
    /// </summary>
    public static IReadOnlyDictionary<Rating, int> GetWeights(Position position) =>
        Weights.TryGetValue(position, out var weights)
            ? weights
            : throw new ArgumentOutOfRangeException(nameof(position), position, "Player position must be defined.");

    /// <summary>
    /// Calculates the overall rating for a player in <paramref name="position"/> with
    /// <paramref name="ratings"/>, which must hold every weighted rating.
    /// </summary>
    public static RatingScore Calculate(Position position, IReadOnlyDictionary<Rating, RatingScore> ratings)
    {
        ArgumentNullException.ThrowIfNull(ratings);

        var weightedTotal = GetWeights(position)
            .Where(weight => weight.Value > 0)
            .Sum(weight => weight.Value * ratings[weight.Key].Value);

        return new RatingScore((int)Math.Round(weightedTotal / 100.0, MidpointRounding.AwayFromZero));
    }

    private static ReadOnlyDictionary<Rating, int> CreateWeights(Dictionary<Rating, int> positionWeights)
    {
        // Fail at type initialization rather than produce an overall off the 0-100 scale.
        if (positionWeights.Values.Sum() != 100 || positionWeights.ContainsKey(Rating.Durability))
        {
            throw new InvalidOperationException(
                "Overall rating weights must total 100 and must not include durability.");
        }

        return new ReadOnlyDictionary<Rating, int>(
            Enum.GetValues<Rating>().ToDictionary(
                rating => rating,
                rating => positionWeights.GetValueOrDefault(rating)));
    }
}