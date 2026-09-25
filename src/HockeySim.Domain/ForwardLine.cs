using System.Collections.ObjectModel;

namespace HockeySim.Domain;

public sealed class ForwardLine
{
    private readonly ReadOnlyCollection<Player> _players;

    public ForwardLine(Player leftWing, Player centre, Player rightWing)
    {
        ArgumentNullException.ThrowIfNull(leftWing);
        ArgumentNullException.ThrowIfNull(centre);
        ArgumentNullException.ThrowIfNull(rightWing);

        if (leftWing.Position != Position.Wing || rightWing.Position != Position.Wing)
        {
            throw new ArgumentException("A forward line must have two wings.");
        }

        if (centre.Position != Position.Centre)
        {
            throw new ArgumentException("A forward line must have one centre.", nameof(centre));
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