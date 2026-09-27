namespace HockeySim.Domain;

public readonly record struct RatingScore
{
    public RatingScore(int value)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "A rating must be between 0 and 100.");
        }

        Value = value;
    }

    public int Value { get; }
}