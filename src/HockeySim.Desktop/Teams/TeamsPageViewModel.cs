using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Roster;
using HockeySim.Domain;

namespace HockeySim.Desktop.Teams;

/// <summary>
/// Browses every team's roster. Rosters here are read-only; only the managed team's lineup is
/// editable, and only from the Lines page.
/// </summary>
public sealed partial class TeamsPageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private TeamEntryViewModel _selectedTeam;

    [ObservableProperty]
    private TeamRosterViewModel _roster;

    public TeamsPageViewModel(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        Teams = session.Snapshot.League.Conferences
            .SelectMany(conference => conference.Divisions.Select(division => (conference, division)))
            .SelectMany(pair => pair.division.Teams.Select(team => new TeamEntryViewModel(
                team.Id,
                team.Name,
                $"{pair.division.Name} · {pair.conference.Name}",
                team.Id == session.Snapshot.ManagedTeamId)))
            .ToList();
        _selectedTeam = Teams.Single(team => team.IsManaged);
        _roster = new TeamRosterViewModel(session, _selectedTeam.Id);
    }

    public override string Title => "League Teams";

    public override string Subtitle => $"{SelectedTeam.Name} · {SelectedTeam.Division}";

    /// <summary>
    /// Gets every team in league order: by conference, then division.
    /// </summary>
    public IReadOnlyList<TeamEntryViewModel> Teams { get; }

    public bool IsSelectedTeamManaged => SelectedTeam.IsManaged;

    public string SelectedTeamInitials => PlayerDisplay.TeamInitials(SelectedTeam.Name);

    public string OwnershipNote => IsSelectedTeamManaged
        ? "Your team · change lines from the Lines page"
        : "Read-only · other clubs set their own lineups";

    public void SelectTeam(TeamId teamId)
    {
        SelectedTeam = Teams.Single(team => team.Id == teamId);
    }

    public override void Refresh()
    {
        Roster = CreateRoster(SelectedTeam.Id, Roster.SelectedPlayer?.Id);
    }

    [RelayCommand]
    private void ShowPreviousTeam()
    {
        SelectedTeam = Teams[(Teams.ToList().IndexOf(SelectedTeam) + Teams.Count - 1) % Teams.Count];
    }

    [RelayCommand]
    private void ShowNextTeam()
    {
        SelectedTeam = Teams[(Teams.ToList().IndexOf(SelectedTeam) + 1) % Teams.Count];
    }

    partial void OnSelectedTeamChanged(TeamEntryViewModel value)
    {
        Roster = CreateRoster(value.Id, null);
        OnPropertyChanged(nameof(IsSelectedTeamManaged));
        OnPropertyChanged(nameof(SelectedTeamInitials));
        OnPropertyChanged(nameof(OwnershipNote));
    }

    /// <summary>
    /// Rebuilds the roster from the latest snapshot, keeping the chosen columns so browsing from
    /// team to team compares like with like.
    /// </summary>
    private TeamRosterViewModel CreateRoster(TeamId teamId, PlayerId? selectedPlayerId) =>
        new(_session, teamId, selectedPlayerId, Roster.Columns);
}

public sealed record TeamEntryViewModel(TeamId Id, string Name, string Division, bool IsManaged);