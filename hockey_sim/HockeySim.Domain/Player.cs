namespace HockeySim.Domain;

public class Player
{
    // player identification values
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public Position Position { get; set; }
    public int Age { get; set; }

    // player ratings stored in map of rating type -> value
    public Dictionary<Rating, int> Ratings { get; set; } = new Dictionary<Rating, int>();

    public int GetRating(Rating rating) => Ratings.TryGetValue(rating, out var val) ? val : 50;

    public int SetRating(Rating rating, int value) => Ratings[rating] = Math.Clamp(value, 0, 100);

    // contract
    public Contract Contract { get; set; } = new();
}