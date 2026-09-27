using System.Collections.ObjectModel;

namespace HockeySim.Domain;

public sealed class DefencePair
{
    private readonly ReadOnlyCollection<Player> _players;

    public DefencePair(Player leftDefence, Player rightDefence)
    {
        ArgumentNullException.ThrowIfNull(leftDefence);
        ArgumentNullException.ThrowIfNull(rightDefence);

        if (leftDefence.Position != Position.Defence || rightDefence.Position != Position.Defence)
        {
            throw new ArgumentException("A defence pair must contain two defence players.");
        }

        if (leftDefence.Id == rightDefence.Id)
        {
            throw new ArgumentException("A player cannot occupy both places on a defence pair.");
        }

        LeftDefence = leftDefence;
        RightDefence = rightDefence;
        _players = Array.AsReadOnly([leftDefence, rightDefence]);
    }

    public Player LeftDefence { get; }

    public Player RightDefence { get; }

    public IReadOnlyList<Player> Players => _players;
}