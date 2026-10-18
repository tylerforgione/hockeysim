using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Game;
using HockeySim.Domain;

namespace HockeySim.Desktop.Roster;

/// <summary>
/// The managed team's roster with player profiles and regular-season or playoff totals.
/// </summary>
public sealed partial class RosterPageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;
    private readonly Action<TeamId>? _openLines;

    [ObservableProperty]
    private TeamRosterViewModel _roster;

    /// <param name="openLines">Opens the Lines page on the roster's team; the roster's Lines link is disabled without it.</param>
    public RosterPageViewModel(GameSession session, Action<TeamId>? openLines = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _openLines = openLines;
        _roster = new TeamRosterViewModel(session, session.ManagedTeam.Id, openLines: openLines);
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
        Roster = new TeamRosterViewModel(_session, _session.ManagedTeam.Id, Roster.SelectedPlayer?.Id, Roster.Columns, Roster.Statistics, Roster.Sorts, Roster.Group, Roster.ForwardPosition, _openLines);
        OnPropertyChanged(nameof(Subtitle));
    }
}