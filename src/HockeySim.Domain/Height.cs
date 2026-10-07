namespace HockeySim.Domain;

/// <summary>
/// A player's height in whole inches, the unit hockey rosters list it in.
/// </summary>
public readonly record struct Height
{
    public const int MinimumInches = 60;
    public const int MaximumInches = 84;

    public Height(int inches)
    {
        if (inches is < MinimumInches or > MaximumInches)
        {
            throw new ArgumentOutOfRangeException(
                nameof(inches),
                $"A player's height must be between {MinimumInches} and {MaximumInches} inches.");
        }

        Inches = inches;
    }

    public int Inches { get; }
}