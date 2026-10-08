namespace HockeySim.Domain;

/// <summary>
/// <see cref="ShotTotals"/> kept separately for each <see cref="StrengthSituation"/>, from one
/// team's side.
/// </summary>
public sealed record SituationalShotTotals
{
    public SituationalShotTotals(ShotTotals fiveOnFive, ShotTotals powerPlay, ShotTotals penaltyKill, ShotTotals other)
    {
        ArgumentNullException.ThrowIfNull(fiveOnFive);
        ArgumentNullException.ThrowIfNull(powerPlay);
        ArgumentNullException.ThrowIfNull(penaltyKill);
        ArgumentNullException.ThrowIfNull(other);

        FiveOnFive = fiveOnFive;
        PowerPlay = powerPlay;
        PenaltyKill = penaltyKill;
        Other = other;
    }

    public static SituationalShotTotals None { get; } =
        new(ShotTotals.None, ShotTotals.None, ShotTotals.None, ShotTotals.None);

    public ShotTotals FiveOnFive { get; }

    public ShotTotals PowerPlay { get; }

    public ShotTotals PenaltyKill { get; }

    public ShotTotals Other { get; }

    /// <summary>Every situation together.</summary>
    public ShotTotals All => FiveOnFive.Add(PowerPlay).Add(PenaltyKill).Add(Other);

    public ShotTotals this[StrengthSituation situation] => situation switch
    {
        StrengthSituation.FiveOnFive => FiveOnFive,
        StrengthSituation.PowerPlay => PowerPlay,
        StrengthSituation.PenaltyKill => PenaltyKill,
        StrengthSituation.Other => Other,
        _ => throw new ArgumentOutOfRangeException(nameof(situation), situation, "The strength situation is not recognised."),
    };

    /// <summary>
    /// The same play seen from the opponent's side: for and against swapped, and this side's power
    /// play the opponent's penalty kill.
    /// </summary>
    public SituationalShotTotals Reverse() =>
        new(FiveOnFive.Reverse(), PenaltyKill.Reverse(), PowerPlay.Reverse(), Other.Reverse());

    public SituationalShotTotals Add(SituationalShotTotals other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new(
            FiveOnFive.Add(other.FiveOnFive),
            PowerPlay.Add(other.PowerPlay),
            PenaltyKill.Add(other.PenaltyKill),
            Other.Add(other.Other));
    }

    internal bool Matches(SituationalShotTotals other) =>
        Enum.GetValues<StrengthSituation>().All(situation => this[situation].Matches(other[situation]));

    internal bool IsWithin(SituationalShotTotals bound) =>
        Enum.GetValues<StrengthSituation>().All(situation => this[situation].IsWithin(bound[situation]));
}