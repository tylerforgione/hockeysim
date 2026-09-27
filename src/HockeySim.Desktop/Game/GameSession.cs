using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Inbox;
using HockeySim.Management.Lineups;

namespace HockeySim.Desktop.Game;

/// <summary>
/// Presents one running game to the shell's pages: it forwards commands to Management and
/// publishes the latest snapshot so every page renders the same state.
/// </summary>
public sealed partial class GameSession : ObservableObject
{
    private readonly GameManager _gameManager;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ManagedTeam))]
    private GameSnapshot _snapshot;

    public GameSession(GameManager gameManager, string gameName)
    {
        ArgumentNullException.ThrowIfNull(gameManager);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);

        _gameManager = gameManager;
        _snapshot = gameManager.GetSnapshot();
        GameName = gameName;
    }

    public string GameName { get; }

    public TeamSnapshot ManagedTeam => Snapshot.League.Teams.Single(team => team.Id == Snapshot.ManagedTeamId);

    public void SetLineup(SetLineupCommand command)
    {
        Snapshot = _gameManager.SetLineup(command);
    }

    public void MarkInboxMessageRead(InboxMessageId messageId)
    {
        Snapshot = _gameManager.MarkInboxMessageRead(messageId);
    }

    public (ConferenceSnapshot Conference, DivisionSnapshot Division) FindDivision(TeamSnapshot team)
    {
        foreach (var conference in Snapshot.League.Conferences)
        {
            foreach (var division in conference.Divisions)
            {
                if (division.Teams.Any(candidate => candidate.Id == team.Id))
                {
                    return (conference, division);
                }
            }
        }

        throw new InvalidOperationException($"Team '{team.Name}' does not belong to a division.");
    }
}