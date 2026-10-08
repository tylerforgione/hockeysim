using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// What a completed match did to its players' health: the injuries suffered, in the order they
/// happened, and the hidden wear each body part took. The season applies them when the match's day
/// is completed, so a player's health always follows from the completed matches.
/// </summary>
public sealed class MatchHealthChanges
{
    private readonly ReadOnlyCollection<MatchInjury> _injuries;
    private readonly ReadOnlyCollection<WearGain> _wear;

    /// <param name="injuries">Every injury suffered, in time order.</param>
    /// <param name="wear">At most one entry for each player's body part.</param>
    public MatchHealthChanges(IEnumerable<MatchInjury> injuries, IEnumerable<WearGain> wear)
    {
        ArgumentNullException.ThrowIfNull(injuries);
        ArgumentNullException.ThrowIfNull(wear);

        var injuryList = injuries.ToList();
        var wearList = wear.ToList();
        if (injuryList.Any(injury => injury is null) || wearList.Any(gain => gain is null))
        {
            throw new ArgumentException("A match's health changes cannot contain a missing injury or wear.");
        }

        if (injuryList.Zip(injuryList.Skip(1)).Any(pair =>
                (pair.First.Period, pair.First.TimeInPeriod).CompareTo((pair.Second.Period, pair.Second.TimeInPeriod)) > 0))
        {
            throw new ArgumentException("A match's injuries must be in time order.", nameof(injuries));
        }

        if (wearList.Select(gain => (gain.PlayerId, gain.BodyPart)).Distinct().Count() != wearList.Count)
        {
            throw new ArgumentException("A match records each player's body part's wear at most once.", nameof(wear));
        }

        _injuries = injuryList.AsReadOnly();
        _wear = wearList.AsReadOnly();
    }

    /// <summary>A match that injured nobody and added no wear, such as one in which injuries are off.</summary>
    public static MatchHealthChanges None { get; } = new([], []);

    public IReadOnlyList<MatchInjury> Injuries => _injuries;

    /// <summary>Hidden information: never shown to the user.</summary>
    public IReadOnlyList<WearGain> Wear => _wear;
}