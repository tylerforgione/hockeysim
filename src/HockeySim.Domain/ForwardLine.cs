using System.Collections.ObjectModel;

namespace HockeySim.Domain;

public sealed class ForwardLine
{
    private readonly ReadOnlyCollection<Player> _players;

    public ForwardLine(Player leftWing, Player center, Player rightWing)
    {
        ArgumentNullException.ThrowIfNull(leftWing);
        ArgumentNullException.ThrowIfNull(center);
        ArgumentNullException.ThrowIfNull(rightWing);

        if (leftWing.Position != Position.Wing || rightWing.Position != Position.Wing)
        {
            throw new ArgumentException("A forward line must have two wings.");
        }

        if (center.Position != Position.Center)
        {
            throw new ArgumentException("A forward line must have one center.", nameof(center));
        }

        if (new[] { leftWing.Id, center.Id, rightWing.Id }.Distinct().Count() != 3)
        {
            throw new ArgumentException("A player cannot occupy multiple places on a forward line.");
        }

        LeftWing = leftWing;
        Center = center;
        RightWing = rightWing;
        _players = Array.AsReadOnly([leftWing, center, rightWing]);
    }

    public Player LeftWing { get; }

    public Player Center { get; }

    public Player RightWing { get; }

    public IReadOnlyList<Player> Players => _players;
}