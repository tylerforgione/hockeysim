using System.Collections.ObjectModel;

namespace HockeySim.Management.NewGame;

/// <summary>
/// Describes the stable choices a caller can present before creating a game.
/// </summary>
public sealed class NewGameOptionsSnapshot
{
    private readonly ReadOnlyCollection<string> _teamNames;

    private NewGameOptionsSnapshot(IReadOnlyList<string> teamNames)
    {
        _teamNames = new ReadOnlyCollection<string>(teamNames.ToList());
    }

    public IReadOnlyList<string> TeamNames => _teamNames;

    internal static NewGameOptionsSnapshot Create(IEnumerable<string> teamNames) =>
        new(teamNames.ToList());
}