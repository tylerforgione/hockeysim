namespace HockeySim.Domain;

/// <summary>
/// How a skater's natural <see cref="Position"/> suits the <see cref="SkaterRole"/> they fill.
/// </summary>
public enum PositionFit
{
    /// <summary>The role is the skater's natural position.</summary>
    Natural,

    /// <summary>A centre on the wing or a winger at centre.</summary>
    OtherForwardPosition,

    /// <summary>A forward on defence or a defenceman at forward.</summary>
    AcrossForwardsAndDefence,
}