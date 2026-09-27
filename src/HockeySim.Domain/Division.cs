using System.Collections.ObjectModel;

namespace HockeySim.Domain;

public sealed class Division
{
    public const int RequiredTeamCount = 8;

    private readonly ReadOnlyCollection<Team> _teams;

    public Division(string name, IEnumerable<Team> teams)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(teams);

        var teamList = teams.ToList();
        if (teamList.Count != RequiredTeamCount)
        {
            throw new ArgumentException(
                $"A division must contain exactly {RequiredTeamCount} teams.",
                nameof(teams));
        }

        if (teamList.Select(team => team.Id).Distinct().Count() != teamList.Count)
        {
            throw new ArgumentException("Team identities must be unique within a division.", nameof(teams));
        }

        Name = name;
        _teams = teamList.AsReadOnly();
    }

    public string Name { get; }

    public IReadOnlyList<Team> Teams => _teams;
}