namespace HockeySim.Domain;

/// <summary>
/// A kind of injury in the <see cref="InjuryCatalogue"/>, which gives its body part, recovery
/// time, and whether a player can play through it.
/// </summary>
public enum InjuryType
{
    Concussion,
    BrokenNose,
    SeparatedShoulder,
    BruisedShoulder,
    BrokenHand,
    BrokenFinger,
    BruisedRibs,
    BackSpasms,
    GroinStrain,
    TightGroin,
    SprainedKnee,
    BruisedKnee,
    HighAnkleSprain,
    SprainedAnkle,
    BrokenFoot,
    BruisedFoot,
}