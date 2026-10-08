namespace HockeySim.Domain;

/// <summary>
/// An injury a player has suffered. It heals over league days, whether or not the player's team
/// plays: a player hurt on one date with seven days' recovery is healthy again a week later.
/// </summary>
public sealed record Injury
{
    /// <param name="date">The date of the match in which it happened.</param>
    /// <param name="recoveryDays">Within the injury's range in the <see cref="InjuryCatalogue"/>.</param>
    public Injury(InjuryType type, DateOnly date, int recoveryDays)
    {
        if (!InjuryCatalogue.For(type).AllowsRecoveryDays(recoveryDays))
        {
            throw new ArgumentOutOfRangeException(
                nameof(recoveryDays),
                "The recovery time must be within the injury's range in the catalogue.");
        }

        Type = type;
        Date = date;
        RecoveryDays = recoveryDays;
    }

    public InjuryType Type { get; }

    public DateOnly Date { get; }

    public int RecoveryDays { get; }

    public InjuryDefinition Definition => InjuryCatalogue.For(Type);

    /// <summary>The first date on which the injury has healed.</summary>
    public DateOnly ReturnDate => Date.AddDays(RecoveryDays);

    public bool IsActiveOn(DateOnly date) => date >= Date && date < ReturnDate;

    /// <summary>
    /// The staff's estimate of the return date: a quarter of the recovery time either side of it,
    /// kept within the injury's range in the catalogue. The estimate is worked out from the injury
    /// alone, so it never consumes the game's random state and is the same after a reload.
    /// </summary>
    public ExpectedReturn ExpectedReturn
    {
        get
        {
            var margin = RecoveryDays / 4.0;
            var earliestDays = Math.Max(Definition.MinimumRecoveryDays, (int)Math.Floor(RecoveryDays - margin));
            var latestDays = Math.Min(Definition.MaximumRecoveryDays, (int)Math.Ceiling(RecoveryDays + margin));
            return new ExpectedReturn(Date.AddDays(earliestDays), Date.AddDays(latestDays));
        }
    }
}