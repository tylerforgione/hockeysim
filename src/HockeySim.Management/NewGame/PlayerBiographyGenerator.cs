using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Management.NewGame;

/// <summary>
/// Generates a new player's name and biography to resemble an NHL population.
/// </summary>
/// <remarks>
/// Nationality follows <see cref="PlayerOriginData"/>'s weights, and the name always fits the
/// nationality. Most players are born in the country they represent; a few are born abroad.
/// Ages on opening day cluster in the mid-twenties. Handedness, height, and weight depend on
/// position: most players shoot and catch left, defence and goalies are taller, and weight
/// follows height. The values are approximations, not calibrated league data.
/// </remarks>
internal static class PlayerBiographyGenerator
{
    private const double BornAbroadChance = 0.06;

    // Relative weights of each age from 18 to 40 on opening day.
    private const int YoungestAge = 18;
    private static readonly double[] AgeWeights =
    [
        1, 2, 4, 6, 8, 9, 9, 9, 9, 8, 8, 7, 6, 5, 4, 3, 2, 2, 1, 1, 0.5, 0.5, 0.5,
    ];

    // Four draws of 0-3 inches give a bell-shaped spread of twelve inches around the base plus six.
    private const int HeightDrawCount = 4;
    private const int HeightDrawMaximum = 3;
    private const int HeightDrawMean = HeightDrawCount * HeightDrawMaximum / 2;

    // Pounds per inch above or below the position's average height, plus a little independent build.
    private const int PoundsPerInch = 5;
    private const int BuildDrawCount = 4;
    private const int BuildDrawMaximum = 6;
    private const int BuildDrawMean = BuildDrawCount * BuildDrawMaximum / 2;

    private static readonly IReadOnlyDictionary<Position, PositionBuild> Builds =
        new Dictionary<Position, PositionBuild>
        {
            [Position.Centre] = new(LeftHandedShare: 0.62, ShortestInches: 67, AverageWeightPounds: 195),
            [Position.Wing] = new(LeftHandedShare: 0.58, ShortestInches: 67, AverageWeightPounds: 196),
            [Position.Defence] = new(LeftHandedShare: 0.65, ShortestInches: 68, AverageWeightPounds: 205),
            [Position.Goalie] = new(LeftHandedShare: 0.90, ShortestInches: 69, AverageWeightPounds: 200),
        };

    public static GeneratedIdentity Create(Position position, DateOnly openingDay, ControlledRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var nationality = DrawCountry(random, excluded: null);
        var origin = PlayerOriginData.For(nationality);
        var firstName = origin.FirstNames[random.NextInt(0, origin.FirstNames.Count)];
        var lastName = origin.LastNames[random.NextInt(0, origin.LastNames.Count)];

        var birthCountry = random.Chance(BornAbroadChance) ? DrawCountry(random, excluded: nationality) : nationality;
        var birthplaces = PlayerOriginData.For(birthCountry).Birthplaces;
        var place = birthplaces[random.NextInt(0, birthplaces.Count)];

        var build = Builds[position];
        var handedness = random.Chance(build.LeftHandedShare) ? Handedness.Left : Handedness.Right;
        var heightInches = build.ShortestInches + Sum(random, HeightDrawCount, HeightDrawMaximum);
        var averageInches = build.ShortestInches + HeightDrawMean;
        var weightPounds = build.AverageWeightPounds
            + (PoundsPerInch * (heightInches - averageInches))
            + Sum(random, BuildDrawCount, BuildDrawMaximum) - BuildDrawMean;

        var biography = new PlayerBiography(
            DrawBirthDate(openingDay, random),
            new Birthplace(place.City, place.Region, birthCountry),
            nationality,
            handedness,
            new Height(heightInches),
            new Weight(weightPounds));
        return new GeneratedIdentity(firstName, lastName, biography);
    }

    private static Country DrawCountry(ControlledRandom random, Country? excluded)
    {
        var candidates = PlayerOriginData.Countries.Where(origin => origin.Country != excluded).ToList();
        return candidates[random.NextWeightedIndex(candidates.Select(origin => (double)origin.Weight).ToList())].Country;
    }

    /// <summary>
    /// Draws an age on opening day, then a birthday anywhere in the year before the player turns
    /// one year older, so the age on opening day is exactly the one drawn.
    /// </summary>
    private static DateOnly DrawBirthDate(DateOnly openingDay, ControlledRandom random)
    {
        var age = YoungestAge + random.NextWeightedIndex(AgeWeights);
        return openingDay.AddYears(-age).AddDays(-random.NextInt(0, 365));
    }

    private static int Sum(ControlledRandom random, int drawCount, int drawMaximum)
    {
        var total = 0;
        for (var draw = 0; draw < drawCount; draw++)
        {
            total += random.NextInt(0, drawMaximum + 1);
        }

        return total;
    }

    private sealed record PositionBuild(double LeftHandedShare, int ShortestInches, int AverageWeightPounds);
}

internal sealed record GeneratedIdentity(string FirstName, string LastName, PlayerBiography Biography);