using HockeySim.Domain;

namespace HockeySim.Simulation;

/// <summary>
/// The players' health going into a match, and whether the match can injure them. A dressed player
/// who cannot play on the match date is out of the match from the start; one playing through an
/// injury plays at reduced ratings. Players without a <see cref="PlayerHealth"/> are healthy and
/// have no wear.
/// </summary>
public sealed class MatchHealth
{
    private readonly Dictionary<PlayerId, PlayerHealth> _players;

    /// <param name="date">The match date, on which current injuries are judged.</param>
    /// <param name="players">The health of the rostered players of both teams.</param>
    /// <param name="injuriesPossible">
    /// Whether the match can injure players or add wear; preseason matches cannot.
    /// </param>
    public MatchHealth(DateOnly date, IEnumerable<PlayerHealth> players, bool injuriesPossible = true)
    {
        ArgumentNullException.ThrowIfNull(players);

        Date = date;
        _players = players.ToDictionary(health => health.PlayerId);
        InjuriesPossible = injuriesPossible;
    }

    /// <summary>Every player healthy, with no wear, in a match that can injure them.</summary>
    public static MatchHealth AllHealthy { get; } = new(DateOnly.MinValue, []);

    public DateOnly Date { get; }

    public bool InjuriesPossible { get; }

    public PlayerHealth For(PlayerId playerId) =>
        _players.TryGetValue(playerId, out var health) ? health : PlayerHealth.Healthy(playerId);

    public bool CanPlay(PlayerId playerId) => For(playerId).CanPlayOn(Date);
}