using System.Collections.ObjectModel;

namespace HockeySim.Domain;

public sealed class Player
{
    private readonly ReadOnlyDictionary<Rating, RatingScore> _ratings;

    public Player(
        PlayerId id,
        string firstName,
        string lastName,
        Position position,
        int age,
        int number,
        IReadOnlyDictionary<Rating, RatingScore> ratings)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("A player identity cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        if (!Enum.IsDefined(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Player position must be defined.");
        }

        if (age is < 16 or > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(age), "Player age must be between 16 and 60.");
        }

        if (number is < 1 or > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "Player number must be between 1 and 99.");
        }

        ArgumentNullException.ThrowIfNull(ratings);
        var ratingValues = new Dictionary<Rating, RatingScore>(ratings);
        var requiredRatings = Enum.GetValues<Rating>();
        if (ratingValues.Count != requiredRatings.Length
            || requiredRatings.Any(rating => !ratingValues.ContainsKey(rating)))
        {
            throw new ArgumentException("Every defined player rating must have a value.", nameof(ratings));
        }

        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Position = position;
        Age = age;
        Number = number;
        _ratings = new ReadOnlyDictionary<Rating, RatingScore>(ratingValues);
    }

    public PlayerId Id { get; }

    public string FirstName { get; }

    public string LastName { get; }

    public Position Position { get; }

    public int Age { get; }

    public int Number { get; }

    public IReadOnlyDictionary<Rating, RatingScore> Ratings => _ratings;

    public RatingScore GetRating(Rating rating) => _ratings[rating];
}