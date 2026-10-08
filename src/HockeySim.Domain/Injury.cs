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
}