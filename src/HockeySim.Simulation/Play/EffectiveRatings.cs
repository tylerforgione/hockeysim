using HockeySim.Domain;

namespace HockeySim.Simulation.Play;

/// <summary>
/// A player's ratings as they play in a match: their own ratings less the points taken off by any
/// injury they are playing through, never below zero.
/// </summary>
internal sealed class EffectiveRatings(Player player, IReadOnlyDictionary<Rating, int> reductions)
{
    public Player Player { get; } = player;

    public double this[Rating rating] =>
        Math.Max(0, Player.GetRating(rating).Value - reductions.GetValueOrDefault(rating));
}