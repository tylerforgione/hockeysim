using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// A defence pair's left and right defence.
/// </summary>
/// <remarks>
/// Any skater can fill either place, whatever their natural position; see <see cref="SkaterFit"/>.
/// </remarks>
public sealed class DefencePair
{
    private readonly ReadOnlyCollection<Player> _players;

    public DefencePair(Player leftDefence, Player rightDefence)
    {
        ArgumentNullException.ThrowIfNull(leftDefence);
        ArgumentNullException.ThrowIfNull(rightDefence);

        if (leftDefence.Position == Position.Goalie || rightDefence.Position == Position.Goalie)
        {
            throw new ArgumentException("A defence pair cannot include a goalie.");
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