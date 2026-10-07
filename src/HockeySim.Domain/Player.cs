using System.Collections.ObjectModel;

namespace HockeySim.Domain;

public sealed class Player
{
    /// <summary>The youngest age, on a season's opening day, at which a player can be rostered.</summary>
    public const int MinimumAge = 16;

    /// <summary>The oldest age, on a season's opening day, at which a player can be rostered.</summary>
    public const int MaximumAge = 60;

    private readonly ReadOnlyDictionary<Rating, RatingScore> _ratings;

    public Player(
        PlayerId id,
        string firstName,
        string lastName,
        Position position,
        PlayerBiography biography,
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

        ArgumentNullException.ThrowIfNull(biography);

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
        Biography = biography;
        Number = number;
        _ratings = new ReadOnlyDictionary<Rating, RatingScore>(ratingValues);
        Overall = OverallRating.Calculate(position, _ratings);
    }

    public PlayerId Id { get; }

    public string FirstName { get; }

    public string LastName { get; }

    public Position Position { get; }

    /// <summary>Birth date, birthplace, nationality, handedness, height, and weight.</summary>
    public PlayerBiography Biography { get; }

    public int Number { get; }

    public IReadOnlyDictionary<Rating, RatingScore> Ratings => _ratings;

    /// <summary>The ratings summarised for the player's position; see <see cref="OverallRating"/>.</summary>
    public RatingScore Overall { get; }

    public RatingScore GetRating(Rating rating) => _ratings[rating];

    /// <summary>The player's age in completed years on a date; see <see cref="PlayerBiography.AgeOn"/>.</summary>
    public int AgeOn(DateOnly date) => Biography.AgeOn(date);
}