using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Players;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Roster;

/// <summary>
/// A read-only roster split into skaters and goalies, with the selected player's profile.
/// </summary>
public sealed partial class TeamRosterViewModel : ObservableObject
{
    private readonly TeamSnapshot _team;

    [ObservableProperty]
    private PlayerRowViewModel? _selectedSkater;

    [ObservableProperty]
    private PlayerRowViewModel? _selectedGoalie;

    [ObservableProperty]
    private PlayerDetailViewModel? _selectedPlayer;

    public TeamRosterViewModel(TeamSnapshot team, PlayerId? initiallySelectedPlayerId = null)
    {
        ArgumentNullException.ThrowIfNull(team);

        _team = team;
        var rows = team.Roster
            .OrderBy(player => player.Position)
            .ThenBy(player => player.LastName, StringComparer.Ordinal)
            .Select(player => new PlayerRowViewModel(player, team.Lineup))
            .ToList();
        Skaters = rows.Where(row => row.Player.Position != Position.Goalie).ToList();
        Goalies = rows.Where(row => row.Player.Position == Position.Goalie).ToList();

        var initialRow = rows.FirstOrDefault(row => row.Player.Id == initiallySelectedPlayerId) ?? rows[0];
        SelectPlayer(initialRow.Player.Id);
    }

    public IReadOnlyList<PlayerRowViewModel> Skaters { get; }

    public IReadOnlyList<PlayerRowViewModel> Goalies { get; }

    public void SelectPlayer(PlayerId playerId)
    {
        var row = Skaters.Concat(Goalies).Single(row => row.Player.Id == playerId);
        if (row.Player.Position == Position.Goalie)
        {
            SelectedGoalie = row;
        }
        else
        {
            SelectedSkater = row;
        }
    }

    partial void OnSelectedSkaterChanged(PlayerRowViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedGoalie = null;
        SelectedPlayer = new PlayerDetailViewModel(value.Player, _team);
    }

    partial void OnSelectedGoalieChanged(PlayerRowViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedSkater = null;
        SelectedPlayer = new PlayerDetailViewModel(value.Player, _team);
    }
}