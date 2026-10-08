namespace HockeySim.Domain;

/// <summary>
/// A player's weight in whole pounds, the unit hockey rosters list it in.
/// </summary>
public readonly record struct Weight
{
    public const int MinimumPounds = 120;
    public const int MaximumPounds = 320;

    public Weight(int pounds)
    {
        if (pounds is < MinimumPounds or > MaximumPounds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pounds),
                $"A player's weight must be between {MinimumPounds} and {MaximumPounds} pounds.");
        }

        Pounds = pounds;
    }

    public int Pounds { get; }
}