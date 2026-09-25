using System.Collections.ObjectModel;

namespace HockeySim.Domain;

public sealed class Team
{
    public const int RequiredRosterSize = 23;

    private readonly ReadOnlyCollection<Player> _roster;

    public Team(TeamId id, string name, IEnumerable<Player> roster, Lineup lineup)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("A team identity cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(lineup);

        var rosterList = roster.ToList();
        if (rosterList.Count != RequiredRosterSize)
        {
            throw new ArgumentException(
                $"A team roster must contain exactly {RequiredRosterSize} players.",
                nameof(roster));
        }

        if (rosterList.Select(player => player.Id).Distinct().Count() != rosterList.Count)
        {
            throw new ArgumentException("Player identities must be unique within a roster.", nameof(roster));
        }

        if (rosterList.Select(player => player.Number).Distinct().Count() != rosterList.Count)
        {
            throw new ArgumentException("Player numbers must be unique within a roster.", nameof(roster));
        }

        var rosterIds = rosterList.Select(player => player.Id).ToHashSet();
        if (lineup.DressedPlayers.Any(player => !rosterIds.Contains(player.Id)))
        {
            throw new ArgumentException("Every dressed player must belong to the team roster.", nameof(lineup));
        }

        Id = id;
        Name = name;
        _roster = rosterList.AsReadOnly();
        Lineup = lineup;
    }

    public void SetLineup(Lineup lineup)
    {
        ArgumentNullException.ThrowIfNull(lineup);

        var rosterIds = _roster.Select(player => player.Id).ToHashSet();
        if (lineup.DressedPlayers.Any(player => !rosterIds.Contains(player.Id)))
        {
            throw new ArgumentException(
                "Every dressed player must be on the roster",
                nameof(lineup)
            );
        }

        Lineup = lineup;
    }

    public TeamId Id { get; }

    public string Name { get; }

    public IReadOnlyList<Player> Roster => _roster;

    public Lineup Lineup { get; private set; }
}