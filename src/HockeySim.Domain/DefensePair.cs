using System.Collections.ObjectModel;

namespace HockeySim.Domain;

public sealed class DefensePair
{
    private readonly ReadOnlyCollection<Player> _players;

    public DefensePair(Player leftDefense, Player rightDefense)
    {
        ArgumentNullException.ThrowIfNull(leftDefense);
        ArgumentNullException.ThrowIfNull(rightDefense);

        if (leftDefense.Position != Position.Defense || rightDefense.Position != Position.Defense)
        {
            throw new ArgumentException("A defense pair must contain two defense players.");
        }

        if (leftDefense.Id == rightDefense.Id)
        {
            throw new ArgumentException("A player cannot occupy both places on a defense pair.");
        }

        LeftDefense = leftDefense;
        RightDefense = rightDefense;
        _players = Array.AsReadOnly([leftDefense, rightDefense]);
    }

    public Player LeftDefense { get; }

    public Player RightDefense { get; }

    public IReadOnlyList<Player> Players => _players;
}