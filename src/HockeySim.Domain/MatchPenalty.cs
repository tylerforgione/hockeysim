namespace HockeySim.Domain;

/// <summary>
/// One penalty in a completed match's penalty summary, recorded when play stopped for it. A fight
/// is a fighting major to each fighter.
/// </summary>
public sealed record MatchPenalty
{
    /// <param name="period">One to three for regulation, then each overtime period in turn.</param>
    /// <param name="timeInPeriod">Elapsed time in the period, in whole seconds.</param>
    /// <param name="teamId">The penalized team.</param>
    public MatchPenalty(
        int period,
        TimeSpan timeInPeriod,
        TeamId teamId,
        PlayerId playerId,
        Infraction infraction,
        PenaltyKind kind)
    {
        MatchClock.ThrowIfInvalid(period, timeInPeriod);

        if (!Enum.IsDefined(infraction))
        {
            throw new ArgumentOutOfRangeException(nameof(infraction), "The infraction is not recognised.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "The penalty kind is not recognised.");
        }

        Period = period;
        TimeInPeriod = timeInPeriod;
        TeamId = teamId;
        PlayerId = playerId;
        Infraction = infraction;
        Kind = kind;
    }

    public int Period { get; }

    public TimeSpan TimeInPeriod { get; }

    public TeamId TeamId { get; }

    public PlayerId PlayerId { get; }

    public Infraction Infraction { get; }

    public PenaltyKind Kind { get; }

    /// <summary>The penalty minutes charged to the player.</summary>
    public int Minutes => MinutesFor(Kind);

    /// <summary>
    /// The penalty minutes a kind of penalty charges: a game misconduct is recorded as ten, and a
    /// penalty shot carries none.
    /// </summary>
    public static int MinutesFor(PenaltyKind kind) => kind switch
    {
        PenaltyKind.Minor => 2,
        PenaltyKind.DoubleMinor => 4,
        PenaltyKind.Major => 5,
        PenaltyKind.Misconduct => 10,
        PenaltyKind.GameMisconduct => 10,
        PenaltyKind.PenaltyShot => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), "The penalty kind must be defined."),
    };
}