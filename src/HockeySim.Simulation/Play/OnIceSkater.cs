using HockeySim.Domain;

namespace HockeySim.Simulation.Play;

/// <summary>
/// A skater on the ice, the role they play there, and the side of the ice for a wing or defence
/// slot that has one. A skater out of position or on their off-hand side plays a little below
/// their ratings in the slot; see <see cref="SkaterFit"/> and the penalties in
/// <see cref="MatchTuning"/>.
/// </summary>
internal readonly struct OnIceSkater
{
    public OnIceSkater(SkaterState skater, SkaterRole role, SkaterSide? side)
        : this(skater, role, side, SkaterFit.For(skater.Player.Position, role))
    {
    }

    private OnIceSkater(SkaterState skater, SkaterRole role, SkaterSide? side, PositionFit fit)
    {
        Skater = skater;
        Role = role;
        Side = side;

        var positionPenalty = fit switch
        {
            PositionFit.OtherForwardPosition => MatchTuning.OtherForwardPositionPenalty,
            PositionFit.AcrossForwardsAndDefence => MatchTuning.AcrossForwardsAndDefencePenalty,
            _ => 0,
        };
        var offHand = side is { } onSide && SkaterFit.IsOffHand(skater.Player.Biography.Handedness, onSide);
        var offHandWing = offHand && role == SkaterRole.Wing;
        var offHandDefence = offHand && role == SkaterRole.Defence;

        Offence = skater.Offence - positionPenalty
            - (offHandDefence ? MatchTuning.OffHandDefenceOffencePenalty : 0);
        Defence = skater.Defence - positionPenalty
            - (offHandWing ? MatchTuning.OffHandWingDefencePenalty : 0)
            - (offHandDefence ? MatchTuning.OffHandDefenceDefencePenalty : 0);
        Faceoffs = skater.Faceoffs - positionPenalty;
        Finishing = skater.Finishing + (offHandWing ? MatchTuning.OffHandWingFinishingBonus : 0);
        PuckProtection = skater.PuckProtection - (offHandWing ? MatchTuning.OffHandWingPuckProtectionPenalty : 0);
    }

    /// <summary>
    /// An extra attacker, who joins the forwards with no side. No skater is out of position there,
    /// whatever their natural position.
    /// </summary>
    public static OnIceSkater ExtraAttacker(SkaterState skater) =>
        new(skater, SkaterRole.Wing, side: null, PositionFit.Natural);

    public SkaterState Skater { get; }

    public SkaterRole Role { get; }

    public SkaterSide? Side { get; }

    public bool IsDefence => Role == SkaterRole.Defence;

    /// <summary>The skater's offence in this slot.</summary>
    public double Offence { get; }

    /// <summary>The skater's defence in this slot.</summary>
    public double Defence { get; }

    /// <summary>The skater's faceoff rating in this slot; only the centre slot takes faceoffs.</summary>
    public double Faceoffs { get; }

    /// <summary>The skater's finishing in this slot.</summary>
    public double Finishing { get; }

    /// <summary>The skater's puck protection in this slot.</summary>
    public double PuckProtection { get; }
}