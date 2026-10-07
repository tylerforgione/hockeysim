using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// The skaters a team sends out together in one special situation, one per slot of the
/// situation's <see cref="SpecialSituationFormat"/>.
/// </summary>
/// <remarks>
/// Any skater can fill any slot; the slot's role says where they play, not what they are. A
/// lineup checks that the unit's skaters are dressed.
/// </remarks>
public sealed class SpecialSituationUnit
{
    private readonly ReadOnlyCollection<Player> _players;

    /// <param name="players">The skaters in slot order.</param>
    public SpecialSituationUnit(SpecialSituation situation, IEnumerable<Player> players)
    {
        ArgumentNullException.ThrowIfNull(players);

        var format = SpecialSituationFormat.For(situation);
        var playerList = players.ToList();
        if (playerList.Count != format.Roles.Count || playerList.Any(player => player is null))
        {
            throw new ArgumentException(
                $"A {situation} unit must have exactly {format.Roles.Count} skaters.",
                nameof(players));
        }

        if (playerList.Any(player => player.Position == Position.Goalie))
        {
            throw new ArgumentException("A special-situation unit cannot include a goalie.", nameof(players));
        }

        if (playerList.Select(player => player.Id).Distinct().Count() != playerList.Count)
        {
            throw new ArgumentException("A player cannot occupy more than one slot in a unit.", nameof(players));
        }

        Format = format;
        _players = playerList.AsReadOnly();
        Centre = playerList[format.Roles.ToList().IndexOf(SkaterRole.Centre)];
    }

    public SpecialSituation Situation => Format.Situation;

    public SpecialSituationFormat Format { get; }

    /// <summary>
    /// Gets the skaters in slot order, matching <see cref="SpecialSituationFormat.Roles"/>.
    /// </summary>
    public IReadOnlyList<Player> Players => _players;

    /// <summary>
    /// Gets the skater in the centre slot, who takes the unit's faceoffs.
    /// </summary>
    public Player Centre { get; }
}