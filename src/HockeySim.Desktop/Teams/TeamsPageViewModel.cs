using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Roster;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Teams;

/// <summary>
/// Browses every team's roster. Rosters here are read-only; only the managed team's lineup is
/// editable, and only from the Lines page.
/// </summary>
public sealed partial class TeamsPageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;
    private readonly Action<TeamId>? _openLines;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private TeamEntryViewModel _selectedTeam;

    [ObservableProperty]
    private TeamRosterViewModel _roster;

    /// <param name="openLines">Opens the Lines page on the shown team; the roster's Lines link is disabled without it.</param>
    public TeamsPageViewModel(GameSession session, Action<TeamId>? openLines = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _openLines = openLines;
        Teams = TeamEntryViewModel.ForLeague(session.Snapshot);
        _selectedTeam = Teams.Single(team => team.IsManaged);
        _roster = new TeamRosterViewModel(session, _selectedTeam.Id, openLines: openLines);
    }

    public override string Subtitle => $"{SelectedTeam.Name} · {SelectedTeam.Division}";

    /// <summary>
    /// Gets every team in league order: by conference, then division.
    /// </summary>
    public IReadOnlyList<TeamEntryViewModel> Teams { get; }

    public bool IsSelectedTeamManaged => SelectedTeam.IsManaged;

    public string SelectedTeamInitials => PlayerDisplay.TeamInitials(SelectedTeam.Name);

    public string OwnershipNote => IsSelectedTeamManaged
        ? "Your team"
        : "Read-only";

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
    /// Rebuilds the roster from the latest snapshot, keeping the chosen group, columns, statistics, and
    /// sorts so browsing from team to team compares like with like.
    /// </summary>
    private TeamRosterViewModel CreateRoster(TeamId teamId, PlayerId? selectedPlayerId) =>
        new(_session, teamId, selectedPlayerId, Roster.Columns, Roster.Statistics, Roster.Sorts, Roster.Group, Roster.ForwardPosition, _openLines);
}

public sealed record TeamEntryViewModel(TeamId Id, string Name, string Division, bool IsManaged)
{
    /// <summary>
    /// Lists every team in league order (conference, then division), each labelled "Division · Conference".
    /// </summary>
    public static List<TeamEntryViewModel> ForLeague(GameSnapshot snapshot) => snapshot.League.Conferences
        .SelectMany(conference => conference.Divisions.Select(division => (conference, division)))
        .SelectMany(pair => pair.division.Teams.Select(team => new TeamEntryViewModel(
            team.Id,
            team.Name,
            $"{pair.division.Name} · {pair.conference.Name}",
            team.Id == snapshot.ManagedTeamId)))
        .ToList();
}