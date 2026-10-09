using HockeySim.Domain;

namespace HockeySim.Simulation.Play;

/// <summary>
/// How often the play injures players, which body parts it strikes, and which injuries follow.
/// Calibrated with the rest of <see cref="MatchTuning"/> so a generated league's season loses
/// about as many matches to injury as the NHL loses to injuries in games; see
/// docs/areas/match-engine.md.
/// </summary>
/// <remarks>
/// Every contact (a hit, a blocked shot, a fight) strikes a body part, adds wear to it, and may
/// injure it; a strain adds no wear unless it injures. The chance of an injury is the cause's base
/// chance, shifted by durability from <see cref="MatchTuning.ReferenceRating"/> and raised by the
/// body part's wear.
/// </remarks>
internal static class InjuryTuning
{
    /// <summary>
    /// The chance that one contact, or one strain moment, injures a reference player with no wear, for
    /// a struck part whose injuries' chances add up to one.
    /// </summary>
    public static double BaseChance(InjuryCause cause) => cause switch
    {
        InjuryCause.Hit => 0.016,
        InjuryCause.Collision => 0.005,
        InjuryCause.BlockedShot => 0.014,
        InjuryCause.Fight => 0.15,
        _ => 0.024,
    };

    /// <summary>Change in log-odds of injury per durability point; more durable players are hurt less.</summary>
    public const double DurabilitySensitivity = 0.03;

    /// <summary>Each point of wear on the struck body part raises the injury chance by this share.</summary>
    public const double WearRiskPerPoint = 0.02;

    /// <summary>Wear a contact adds to the struck body part; a fight is two.</summary>
    public static int ContactWear(InjuryCause cause) => cause == InjuryCause.Fight ? 2 : 1;

    /// <summary>
    /// A recovery time is the shortest of this many draws from the injury's range, so most
    /// injuries heal toward the short end.
    /// </summary>
    public const int RecoveryDayDraws = 3;

    /// <summary>An injury adds this much wear to its body part, plus a point for every few recovery days.</summary>
    public const int InjuryWear = 5;

    public const int RecoveryDaysPerInjuryWearPoint = 3;

    /// <summary>
    /// Moments of strain per player per second played, for a skater on the ice or a goalie in net.
    /// Each may injure, at the strain base chance, and more often when the skater is tired.
    /// </summary>
    public const double StrainMomentsPerPlayerSecond = 0.0005;

    /// <summary>An exhausted skater strains this much more often than a rested one, beyond one.</summary>
    public const double ExhaustedStrainIncrease = 1.0;

    /// <summary>
    /// The body parts each cause strikes, by weight, and the injuries a struck part can suffer, each
    /// with its chance relative to the cause's <see cref="BaseChance"/>: a part's chances add up to
    /// how likely it is to be injured at all. Injuries a player can play through are scaled by
    /// <see cref="PlayThroughChance"/>. Every injury's body
    /// part matches its catalogue entry.
    /// </summary>
    public static IReadOnlyList<StruckPart> PartsStruck(InjuryCause cause) => cause switch
    {
        InjuryCause.Hit => HitParts,
        InjuryCause.Collision => CollisionParts,
        InjuryCause.BlockedShot => BlockedShotParts,
        InjuryCause.Fight => FightParts,
        _ => StrainParts,
    };

    /// <summary>
    /// How much rarer an injury a player can play through is than its share of contacts to the part
    /// would make it. Calibrated so a team suffers about one every five matches.
    /// </summary>
    private const double PlayThroughChance = 0.22;

    private static readonly StruckPart[] HitParts =
    [
        new(BodyPart.Head, 1.5, [(InjuryType.Concussion, 1)]),
        new(BodyPart.Face, 0.8, [(InjuryType.BrokenNose, PlayThroughChance)]),
        new(BodyPart.Shoulder, 3, [(InjuryType.SeparatedShoulder, 0.4), (InjuryType.BruisedShoulder, 0.6 * PlayThroughChance)]),
        new(BodyPart.Ribs, 2, [(InjuryType.BruisedRibs, PlayThroughChance)]),
        new(BodyPart.Back, 0.6, [(InjuryType.BackSpasms, PlayThroughChance)]),
        new(BodyPart.Knee, 1.2, [(InjuryType.SprainedKnee, 0.5), (InjuryType.BruisedKnee, 0.5 * PlayThroughChance)]),
        new(BodyPart.Ankle, 0.6, [(InjuryType.HighAnkleSprain, 0.5), (InjuryType.SprainedAnkle, 0.5 * PlayThroughChance)]),
    ];

    private static readonly StruckPart[] CollisionParts =
    [
        new(BodyPart.Head, 0.5, [(InjuryType.Concussion, 1)]),
        new(BodyPart.Shoulder, 2, [(InjuryType.SeparatedShoulder, 0.33), (InjuryType.BruisedShoulder, 0.67 * PlayThroughChance)]),
        new(BodyPart.Ribs, 1, [(InjuryType.BruisedRibs, PlayThroughChance)]),
        new(BodyPart.Knee, 1.5, [(InjuryType.SprainedKnee, 0.5), (InjuryType.BruisedKnee, 0.5 * PlayThroughChance)]),
    ];

    private static readonly StruckPart[] BlockedShotParts =
    [
        new(BodyPart.Foot, 3, [(InjuryType.BrokenFoot, 0.4), (InjuryType.BruisedFoot, 0.6 * PlayThroughChance)]),
        new(BodyPart.Hand, 1.5, [(InjuryType.BrokenHand, 0.4), (InjuryType.BrokenFinger, 0.6 * PlayThroughChance)]),
        new(BodyPart.Ankle, 0.7, [(InjuryType.SprainedAnkle, PlayThroughChance)]),
        new(BodyPart.Knee, 1, [(InjuryType.BruisedKnee, PlayThroughChance)]),
        new(BodyPart.Face, 0.3, [(InjuryType.BrokenNose, PlayThroughChance)]),
    ];

    private static readonly StruckPart[] FightParts =
    [
        new(BodyPart.Hand, 3, [(InjuryType.BrokenHand, 0.25), (InjuryType.BrokenFinger, 0.75 * PlayThroughChance)]),
        new(BodyPart.Face, 2, [(InjuryType.BrokenNose, PlayThroughChance)]),
        new(BodyPart.Head, 1, [(InjuryType.Concussion, 1)]),
    ];

    private static readonly StruckPart[] StrainParts =
    [
        new(BodyPart.Groin, 3, [(InjuryType.GroinStrain, 0.5), (InjuryType.TightGroin, 0.5 * PlayThroughChance)]),
        new(BodyPart.Back, 1.5, [(InjuryType.BackSpasms, PlayThroughChance)]),
        new(BodyPart.Knee, 0.5, [(InjuryType.SprainedKnee, 1)]),
        new(BodyPart.Ankle, 0.5, [(InjuryType.HighAnkleSprain, 0.5), (InjuryType.SprainedAnkle, 0.5 * PlayThroughChance)]),
    ];

    /// <summary>A body part a cause can strike, and the injuries it can suffer there.</summary>
    internal sealed record StruckPart(BodyPart BodyPart, double Weight, (InjuryType Type, double Chance)[] Injuries)
    {
        /// <summary>How likely the part is to be injured, relative to the cause's base chance.</summary>
        public double Chance => Injuries.Sum(injury => injury.Chance);
    }
}