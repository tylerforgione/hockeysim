using System.Collections.Immutable;

namespace HockeySim.Domain;

/// <summary>
/// A player's injuries this season and the hidden wear on each body part. Wear rises with impacts
/// and injuries, raises the risk of injuring that part, and never recovers. It is hidden
/// information and must never reach anything shown to the user.
/// </summary>
public sealed class PlayerHealth
{
    private readonly ImmutableList<Injury> _injuries;
    private readonly ImmutableDictionary<BodyPart, int> _wear;

    private PlayerHealth(PlayerId playerId, ImmutableList<Injury> injuries, ImmutableDictionary<BodyPart, int> wear)
    {
        PlayerId = playerId;
        _injuries = injuries;
        _wear = wear;
    }

    public PlayerId PlayerId { get; }

    /// <summary>Every injury suffered this season, healed or not, in the order suffered.</summary>
    public IReadOnlyList<Injury> Injuries => _injuries;

    /// <summary>A player who has never been injured and has no wear.</summary>
    public static PlayerHealth Healthy(PlayerId playerId) =>
        new(playerId, [], ImmutableDictionary<BodyPart, int>.Empty);

    /// <summary>Hidden wear on a body part: zero until the part takes an impact.</summary>
    public int Wear(BodyPart bodyPart) => _wear.GetValueOrDefault(bodyPart);

    /// <summary>The injuries not yet healed on a date.</summary>
    public IReadOnlyList<Injury> InjuriesOn(DateOnly date) =>
        _injuries.Where(injury => injury.IsActiveOn(date)).ToList().AsReadOnly();

    /// <summary>Whether every injury the player has on a date is one they can play through.</summary>
    public bool CanPlayOn(DateOnly date) =>
        _injuries.All(injury => !injury.IsActiveOn(date) || injury.Definition.CanPlayThrough);

    /// <summary>
    /// The rating points the player's injuries take off on a date, summed over every injury they
    /// are playing through.
    /// </summary>
    public IReadOnlyDictionary<Rating, int> RatingReductionsOn(DateOnly date)
    {
        var reductions = new Dictionary<Rating, int>();
        foreach (var injury in InjuriesOn(date))
        {
            foreach (var (rating, points) in injury.Definition.RatingReductions)
            {
                reductions[rating] = reductions.GetValueOrDefault(rating) + points;
            }
        }

        return reductions.AsReadOnly();
    }

    public PlayerHealth Add(Injury injury)
    {
        ArgumentNullException.ThrowIfNull(injury);
        return new PlayerHealth(PlayerId, _injuries.Add(injury), _wear);
    }

    public PlayerHealth AddWear(BodyPart bodyPart, int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        return new PlayerHealth(PlayerId, _injuries, _wear.SetItem(bodyPart, Wear(bodyPart) + points));
    }
}