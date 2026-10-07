namespace HockeySim.Domain;

/// <summary>
/// A player's identifying details. Age is not stored: it follows from the birth date and the date
/// it is asked for, so players age as the season passes.
/// </summary>
/// <remarks>
/// Nationality is the country the player represents. It is usually, but not always, the birth
/// country.
/// </remarks>
public sealed record PlayerBiography
{
    public PlayerBiography(
        DateOnly birthDate,
        Birthplace birthplace,
        Country nationality,
        Handedness handedness,
        Height height,
        Weight weight)
    {
        ArgumentNullException.ThrowIfNull(birthplace);

        if (!Enum.IsDefined(nationality))
        {
            throw new ArgumentOutOfRangeException(nameof(nationality), "The nationality must be defined.");
        }

        if (!Enum.IsDefined(handedness))
        {
            throw new ArgumentOutOfRangeException(nameof(handedness), "The handedness must be defined.");
        }

        // The structs' constructors validate their range; only an uninitialised default slips past.
        if (height == default)
        {
            throw new ArgumentException("A player's height is required.", nameof(height));
        }

        if (weight == default)
        {
            throw new ArgumentException("A player's weight is required.", nameof(weight));
        }

        BirthDate = birthDate;
        Birthplace = birthplace;
        Nationality = nationality;
        Handedness = handedness;
        Height = height;
        Weight = weight;
    }

    public DateOnly BirthDate { get; }

    public Birthplace Birthplace { get; }

    public Country Nationality { get; }

    /// <summary>The hand a skater shoots or a goalie catches with.</summary>
    public Handedness Handedness { get; }

    public Height Height { get; }

    public Weight Weight { get; }

    /// <summary>
    /// The player's age in completed years on a date. A player born on 29 February turns a year
    /// older on 28 February in other years.
    /// </summary>
    public int AgeOn(DateOnly date)
    {
        if (date < BirthDate)
        {
            throw new ArgumentOutOfRangeException(nameof(date), "A player has no age before their birth date.");
        }

        var age = date.Year - BirthDate.Year;

        // AddYears moves 29 February to 28 February in a common year, which sets the leap-day rule.
        return date < BirthDate.AddYears(age) ? age - 1 : age;
    }
}