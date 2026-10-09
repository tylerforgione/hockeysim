using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Game;
using HockeySim.Domain;

namespace HockeySim.Desktop.Roster;

/// <summary>
/// The managed team's roster with player profiles and current-season totals.
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
        _roster = new TeamRosterViewModel(session, session.ManagedTeam.Id);
    }

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
        Roster = new TeamRosterViewModel(_session, _session.ManagedTeam.Id, Roster.SelectedPlayer?.Id, Roster.Columns);
        OnPropertyChanged(nameof(Subtitle));
    }
}