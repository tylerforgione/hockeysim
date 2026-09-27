using System.Collections.ObjectModel;

namespace HockeySim.Management.NewGame;

/// <summary>
/// Describes the stable choices a caller can present before creating a game.
/// </summary>
public sealed class NewGameOptionsSnapshot
{
    private readonly ReadOnlyCollection<NewGameConferenceSnapshot> _conferences;
    private readonly ReadOnlyCollection<string> _teamNames;

    private NewGameOptionsSnapshot(IReadOnlyList<NewGameConferenceSnapshot> conferences)
    {
        _conferences = new ReadOnlyCollection<NewGameConferenceSnapshot>(conferences.ToList());
        _teamNames = new ReadOnlyCollection<string>(
            conferences
                .SelectMany(conference => conference.Divisions)
                .SelectMany(division => division.TeamNames)
                .ToList());
    }

    public IReadOnlyList<NewGameConferenceSnapshot> Conferences => _conferences;

    public IReadOnlyList<string> TeamNames => _teamNames;

    internal static NewGameOptionsSnapshot Create(IEnumerable<NewGameConferenceSnapshot> conferences) =>
        new(conferences.ToList());
}

public sealed class NewGameConferenceSnapshot
{
    private readonly ReadOnlyCollection<NewGameDivisionSnapshot> _divisions;

    private NewGameConferenceSnapshot(string name, IReadOnlyList<NewGameDivisionSnapshot> divisions)
    {
        Name = name;
        _divisions = new ReadOnlyCollection<NewGameDivisionSnapshot>(divisions.ToList());
    }

    public string Name { get; }

    public IReadOnlyList<NewGameDivisionSnapshot> Divisions => _divisions;

    internal static NewGameConferenceSnapshot Create(
        string name,
        IEnumerable<NewGameDivisionSnapshot> divisions) =>
        new(name, divisions.ToList());
}

public sealed class NewGameDivisionSnapshot
{
    private readonly ReadOnlyCollection<string> _teamNames;

    private NewGameDivisionSnapshot(string name, IReadOnlyList<string> teamNames)
    {
        Name = name;
        _teamNames = new ReadOnlyCollection<string>(teamNames.ToList());
    }

    public string Name { get; }

    public IReadOnlyList<string> TeamNames => _teamNames;

    internal static NewGameDivisionSnapshot Create(string name, IEnumerable<string> teamNames) =>
        new(name, teamNames.ToList());
}