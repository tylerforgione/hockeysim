namespace HockeySim.Domain;

/// <summary>
/// An injury suffered in a completed match: when it happened, to whom, what caused it, and how many
/// league days it takes to heal.
/// </summary>
public sealed record MatchInjury
{
    /// <param name="period">One to three for regulation, then each overtime period in turn.</param>
    /// <param name="timeInPeriod">Elapsed time in the period, in whole seconds.</param>
    /// <param name="teamId">The injured player's team.</param>
    /// <param name="recoveryDays">Within the injury's range in the <see cref="InjuryCatalogue"/>.</param>
    public MatchInjury(
        int period,
        TimeSpan timeInPeriod,
        TeamId teamId,
        PlayerId playerId,
        InjuryType type,
        InjuryCause cause,
        int recoveryDays)
    {
        MatchClock.ThrowIfInvalid(period, timeInPeriod);

        if (!Enum.IsDefined(cause))
        {
            throw new ArgumentOutOfRangeException(nameof(cause), "The injury cause is not recognised.");
        }

        if (!InjuryCatalogue.For(type).AllowsRecoveryDays(recoveryDays))
        {
            throw new ArgumentOutOfRangeException(
                nameof(recoveryDays),
                "The recovery time must be within the injury's range in the catalogue.");
        }

        Period = period;
        TimeInPeriod = timeInPeriod;
        TeamId = teamId;
        PlayerId = playerId;
        Type = type;
        Cause = cause;
        RecoveryDays = recoveryDays;
    }

    public int Period { get; }

    public TimeSpan TimeInPeriod { get; }

    public TeamId TeamId { get; }

    public PlayerId PlayerId { get; }

    public InjuryType Type { get; }

    public InjuryCause Cause { get; }

    public int RecoveryDays { get; }

    public InjuryDefinition Definition => InjuryCatalogue.For(Type);
}