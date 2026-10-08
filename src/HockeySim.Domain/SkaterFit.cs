namespace HockeySim.Domain;

/// <summary>
/// Judges how well a skater suits a lineup slot. Any skater may fill any skater slot; the match
/// engine plays a skater out of position or on their off-hand side a little below their ratings.
/// </summary>
public static class SkaterFit
{
    /// <exception cref="ArgumentException">The position is a goalie's, which no skater role suits.</exception>
    public static PositionFit For(Position position, SkaterRole role)
    {
        var natural = position switch
        {
            Position.Centre => SkaterRole.Centre,
            Position.Wing => SkaterRole.Wing,
            Position.Defence => SkaterRole.Defence,
            _ => throw new ArgumentException("Only skaters can fill a skater role.", nameof(position)),
        };

        if (natural == role)
        {
            return PositionFit.Natural;
        }

        return natural != SkaterRole.Defence && role != SkaterRole.Defence
            ? PositionFit.OtherForwardPosition
            : PositionFit.AcrossForwardsAndDefence;
    }

    /// <summary>
    /// Whether a skater plays on their off-hand side: a left shot on the right, or a right shot on
    /// the left, which puts their backhand toward the boards.
    /// </summary>
    public static bool IsOffHand(Handedness handedness, SkaterSide side) =>
        (handedness == Handedness.Left) != (side == SkaterSide.Left);
}