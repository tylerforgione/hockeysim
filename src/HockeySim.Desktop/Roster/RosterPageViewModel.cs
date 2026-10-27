using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Domain;

namespace HockeySim.Desktop.Roster;

/// <summary>
/// The managed team's roster with player profiles and regular-season or playoff totals.
/// </summary>
public sealed partial class RosterPageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;
    private readonly Action<TeamId>? _openLines;
    private readonly Action<PlayerId, PlayerProfileTab, StatisticsSet>? _openPlayer;

    [ObservableProperty]
    private TeamRosterViewModel _roster;

    /// <param name="openLines">Opens the Lines page on the roster's team; the roster's Lines link is disabled without it.</param>
    /// <param name="openPlayer">Opens a player's profile from the roster; the Profile and Game log links are disabled without it.</param>
    public RosterPageViewModel(GameSession session, Action<TeamId>? openLines = null, Action<PlayerId, PlayerProfileTab, StatisticsSet>? openPlayer = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _openLines = openLines;
        _openPlayer = openPlayer;
        _roster = new TeamRosterViewModel(session, session.ManagedTeam.Id, openLines: openLines, openPlayer: openPlayer);
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
        Roster = new TeamRosterViewModel(_session, _session.ManagedTeam.Id, Roster.SelectedStrip?.PlayerId, Roster.Columns, Roster.Statistics, Roster.Sorts, Roster.Group, Roster.ForwardPosition, _openLines, _openPlayer);
        OnPropertyChanged(nameof(Subtitle));
    }
}