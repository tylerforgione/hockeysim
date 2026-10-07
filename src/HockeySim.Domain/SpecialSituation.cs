namespace HockeySim.Domain;

/// <summary>
/// A manpower situation other than five-on-five, named from the team's own side: a power play
/// has more skaters than the opponent and a penalty kill has fewer.
/// </summary>
public enum SpecialSituation
{
    PowerPlay5On4,
    PowerPlay5On3,
    PowerPlay4On3,
    PenaltyKill4On5,
    PenaltyKill3On5,
    PenaltyKill3On4,
    FourOnFour,

    /// <summary>Three-on-three, played in regular-season overtime.</summary>
    ThreeOnThree,
}