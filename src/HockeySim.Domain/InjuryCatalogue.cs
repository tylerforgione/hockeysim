using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// Every injury a player can suffer, by body part. Recovery times are in league days; since a
/// team plays about every other day, an injury costs about half as many matches. Injuries a player
/// can play through take a few rating points off the abilities they affect, including the
/// goaltending ratings for the injuries goalies also suffer. Which match events cause which
/// injuries, and how often, is the match engine's business.
/// </summary>
public static class InjuryCatalogue
{
    private static readonly ReadOnlyDictionary<InjuryType, InjuryDefinition> Definitions = new(
        new InjuryDefinition[]
        {
            new(InjuryType.Concussion, BodyPart.Head, 3, 30, canPlayThrough: false),
            new(InjuryType.BrokenNose, BodyPart.Face, 5, 14, canPlayThrough: true, Reductions(
                (Rating.Checking, 3), (Rating.Toughness, 5))),
            new(InjuryType.SeparatedShoulder, BodyPart.Shoulder, 7, 40, canPlayThrough: false),
            new(InjuryType.BruisedShoulder, BodyPart.Shoulder, 3, 10, canPlayThrough: true, Reductions(
                (Rating.Checking, 4), (Rating.ShotPower, 3))),
            new(InjuryType.BrokenHand, BodyPart.Hand, 14, 42, canPlayThrough: false),
            new(InjuryType.BrokenFinger, BodyPart.Hand, 10, 28, canPlayThrough: true, Reductions(
                (Rating.PuckControl, 4), (Rating.ShotAccuracy, 4), (Rating.Passing, 3), (Rating.Faceoffs, 3))),
            new(InjuryType.BruisedRibs, BodyPart.Ribs, 5, 14, canPlayThrough: true, Reductions(
                (Rating.Skating, 2), (Rating.ShotPower, 3), (Rating.Checking, 4), (Rating.Stamina, 4))),
            new(InjuryType.BackSpasms, BodyPart.Back, 2, 8, canPlayThrough: true, Reductions(
                (Rating.Skating, 2), (Rating.ShotPower, 3), (Rating.Stamina, 3),
                (Rating.GoalieReflex, 3), (Rating.GoaliePositioning, 2))),
            new(InjuryType.GroinStrain, BodyPart.Groin, 3, 21, canPlayThrough: false),
            new(InjuryType.TightGroin, BodyPart.Groin, 3, 10, canPlayThrough: true, Reductions(
                (Rating.Skating, 3), (Rating.Stamina, 3), (Rating.GoalieReflex, 4), (Rating.GoaliePositioning, 3))),
            new(InjuryType.SprainedKnee, BodyPart.Knee, 5, 30, canPlayThrough: false),
            new(InjuryType.BruisedKnee, BodyPart.Knee, 3, 10, canPlayThrough: true, Reductions(
                (Rating.Skating, 3), (Rating.GoaliePositioning, 3))),
            new(InjuryType.HighAnkleSprain, BodyPart.Ankle, 10, 42, canPlayThrough: false),
            new(InjuryType.SprainedAnkle, BodyPart.Ankle, 4, 12, canPlayThrough: true, Reductions(
                (Rating.Skating, 5), (Rating.GoaliePositioning, 3))),
            new(InjuryType.BrokenFoot, BodyPart.Foot, 7, 35, canPlayThrough: false),
            new(InjuryType.BruisedFoot, BodyPart.Foot, 2, 8, canPlayThrough: true, Reductions(
                (Rating.Skating, 4))),
        }.ToDictionary(definition => definition.Type));

    /// <summary>Every injury, in <see cref="InjuryType"/> order.</summary>
    public static IReadOnlyList<InjuryDefinition> All { get; } =
        Enum.GetValues<InjuryType>().Select(type => Definitions[type]).ToList().AsReadOnly();

    public static InjuryDefinition For(InjuryType type) =>
        Definitions.TryGetValue(type, out var definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(type), "The injury type is not recognised.");

    private static Dictionary<Rating, int> Reductions(params (Rating Rating, int Points)[] reductions) =>
        reductions.ToDictionary(reduction => reduction.Rating, reduction => reduction.Points);
}