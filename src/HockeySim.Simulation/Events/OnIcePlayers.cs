using System.Collections.ObjectModel;

using HockeySim.Domain;

namespace HockeySim.Simulation.Events;

/// <summary>
/// The players both teams had on the ice at one moment. Consecutive events share an instance
/// until someone changes, so a match's play-by-play does not copy the same group for every event.
/// </summary>
public sealed class OnIcePlayers
{
    private readonly ReadOnlyCollection<PlayerId> _homeSkaters;
    private readonly ReadOnlyCollection<PlayerId> _awaySkaters;

    internal OnIcePlayers(
        IEnumerable<PlayerId> homeSkaters,
        PlayerId? homeGoalie,
        IEnumerable<PlayerId> awaySkaters,
        PlayerId? awayGoalie)
    {
        _homeSkaters = homeSkaters.ToList().AsReadOnly();
        _awaySkaters = awaySkaters.ToList().AsReadOnly();
        HomeGoalie = homeGoalie;
        AwayGoalie = awayGoalie;
    }

    public IReadOnlyList<PlayerId> HomeSkaters => _homeSkaters;

    /// <summary>The home goalie in net, or <see langword="null"/> while pulled for an extra attacker.</summary>
    public PlayerId? HomeGoalie { get; }

    public IReadOnlyList<PlayerId> AwaySkaters => _awaySkaters;

    /// <summary>The away goalie in net, or <see langword="null"/> while pulled for an extra attacker.</summary>
    public PlayerId? AwayGoalie { get; }

    public StrengthState Strength => new(_homeSkaters.Count, _awaySkaters.Count);

    public override string ToString() =>
        $"[{string.Join(" ", _homeSkaters)} G {HomeGoalie?.ToString() ?? "pulled"}] v [{string.Join(" ", _awaySkaters)} G {AwayGoalie?.ToString() ?? "pulled"}]";
}