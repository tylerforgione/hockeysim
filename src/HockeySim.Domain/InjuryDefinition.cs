using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// One entry in the <see cref="InjuryCatalogue"/>: the body part an injury affects, the range of
/// recovery times it can take, and whether a player can play through it. How severe an injury is
/// shows in where its recovery time falls in the range.
/// </summary>
public sealed class InjuryDefinition
{
    internal InjuryDefinition(
        InjuryType type,
        BodyPart bodyPart,
        int minimumRecoveryDays,
        int maximumRecoveryDays,
        bool canPlayThrough,
        IReadOnlyDictionary<Rating, int>? ratingReductions = null)
    {
        Type = type;
        BodyPart = bodyPart;
        MinimumRecoveryDays = minimumRecoveryDays;
        MaximumRecoveryDays = maximumRecoveryDays;
        CanPlayThrough = canPlayThrough;
        RatingReductions = new ReadOnlyDictionary<Rating, int>(
            new Dictionary<Rating, int>(ratingReductions ?? new Dictionary<Rating, int>()));
    }

    public InjuryType Type { get; }

    public BodyPart BodyPart { get; }

    /// <summary>The fewest league days the injury takes to heal; always at least one.</summary>
    public int MinimumRecoveryDays { get; }

    /// <summary>The most league days the injury takes to heal.</summary>
    public int MaximumRecoveryDays { get; }

    /// <summary>
    /// Whether the player can keep playing while it heals, at the <see cref="RatingReductions"/>.
    /// A player with any injury that cannot be played through cannot play.
    /// </summary>
    public bool CanPlayThrough { get; }

    /// <summary>
    /// Rating points taken off while a player plays through the injury. Empty for an injury that
    /// cannot be played through.
    /// </summary>
    public IReadOnlyDictionary<Rating, int> RatingReductions { get; }

    /// <summary>Whether a recovery time is within this injury's range.</summary>
    public bool AllowsRecoveryDays(int days) => days >= MinimumRecoveryDays && days <= MaximumRecoveryDays;
}