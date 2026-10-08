using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// A forward line's left wing, centre, and right wing.
/// </summary>
/// <remarks>
/// Any skater can fill any place, whatever their natural position; see <see cref="SkaterFit"/>.
/// </remarks>
public sealed class ForwardLine
{
    private readonly ReadOnlyCollection<Player> _players;

    public ForwardLine(Player leftWing, Player centre, Player rightWing)
    {
        ArgumentNullException.ThrowIfNull(leftWing);
        ArgumentNullException.ThrowIfNull(centre);
        ArgumentNullException.ThrowIfNull(rightWing);

        if (new[] { leftWing, centre, rightWing }.Any(player => player.Position == Position.Goalie))
        {
            throw new ArgumentException("A forward line cannot include a goalie.");
        }

        if (new[] { leftWing.Id, centre.Id, rightWing.Id }.Distinct().Count() != 3)
        {
            throw new ArgumentException("A player cannot occupy multiple places on a forward line.");
        }

        LeftWing = leftWing;
        Centre = centre;
        RightWing = rightWing;
        _players = Array.AsReadOnly([leftWing, centre, rightWing]);
    }

    public Player LeftWing { get; }

    public Player Centre { get; }

    public Player RightWing { get; }

    public IReadOnlyList<Player> Players => _players;
}