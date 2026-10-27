namespace HockeySim.Domain;

/// <summary>
/// A skater's time on the ice kept separately for each <see cref="StrengthSituation"/>, from the
/// skater's team's side, in whole seconds.
/// </summary>
public sealed record SituationalTimeOnIce
{
    public SituationalTimeOnIce(TimeSpan fiveOnFive, TimeSpan powerPlay, TimeSpan penaltyKill, TimeSpan other)
    {
        FiveOnFive = Validated(fiveOnFive, nameof(fiveOnFive));
        PowerPlay = Validated(powerPlay, nameof(powerPlay));
        PenaltyKill = Validated(penaltyKill, nameof(penaltyKill));
        Other = Validated(other, nameof(other));
    }

    public static SituationalTimeOnIce None { get; } = new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);

    public TimeSpan FiveOnFive { get; }

    public TimeSpan PowerPlay { get; }

    public TimeSpan PenaltyKill { get; }

    public TimeSpan Other { get; }

    /// <summary>Every situation together.</summary>
    public TimeSpan All => FiveOnFive + PowerPlay + PenaltyKill + Other;

    public TimeSpan this[StrengthSituation situation] => situation switch
    {
        StrengthSituation.FiveOnFive => FiveOnFive,
        StrengthSituation.PowerPlay => PowerPlay,
        StrengthSituation.PenaltyKill => PenaltyKill,
        StrengthSituation.Other => Other,
        _ => throw new ArgumentOutOfRangeException(nameof(situation), situation, "The strength situation is not recognised."),
    };

    public SituationalTimeOnIce Add(SituationalTimeOnIce other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new(
            FiveOnFive + other.FiveOnFive,
            PowerPlay + other.PowerPlay,
            PenaltyKill + other.PenaltyKill,
            Other + other.Other);
    }

    private static TimeSpan Validated(TimeSpan time, string name)
    {
        if (time < TimeSpan.Zero || time.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new ArgumentOutOfRangeException(name, time, "Time on ice must be a non-negative whole number of seconds.");
        }

        return time;
    }
}