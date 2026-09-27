using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Game;
using HockeySim.Domain;

namespace HockeySim.Desktop.Roster;

/// <summary>
/// The managed team's roster with player profiles.
/// </summary>
public sealed partial class RosterPageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;

    [ObservableProperty]
    private TeamRosterViewModel _roster;

    public RosterPageViewModel(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _roster = new TeamRosterViewModel(session.ManagedTeam);
    }

    public override string Title => "Roster";

    public override string Subtitle
    {
        get
        {
            var team = _session.ManagedTeam;
            return $"{team.Name} · {team.Roster.Count} players · {team.Lineup.DressedPlayerIds.Count} dressed · {team.ScratchedPlayerIds.Count} scratched";
        }
    }

    public void SelectPlayer(PlayerId playerId)
    {
        Roster.SelectPlayer(playerId);
    }

    public override void Refresh()
    {
        Roster = new TeamRosterViewModel(_session.ManagedTeam, Roster.SelectedPlayer?.Id);
        OnPropertyChanged(nameof(Subtitle));
    }
}