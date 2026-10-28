using System.Globalization;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Teams;

/// <summary>
/// The league's teams by conference and division, each with its colours, record, and points, for
/// choosing one to open its team page.
/// </summary>
public sealed partial class TeamsPageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;
    private readonly Action<TeamId> _openTeam;

    [ObservableProperty]
    private IReadOnlyList<TeamDirectoryConferenceViewModel> _conferences = [];

    /// <param name="openTeam">Opens a team's page from its name.</param>
    public TeamsPageViewModel(GameSession session, Action<TeamId> openTeam)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(openTeam);

        _session = session;
        _openTeam = openTeam;
        Refresh();
    }

    public override string Subtitle => string.Create(CultureInfo.CurrentCulture, $"{Conferences.Sum(conference => conference.Divisions.Sum(division => division.Teams.Count))} teams");

    public override void Refresh()
    {
        var snapshot = _session.Snapshot;
        var records = snapshot.Season.Standings.Conferences
            .SelectMany(conference => conference.Divisions)
            .SelectMany(division => division.Teams)
            .ToDictionary(entry => entry.Record.TeamId, entry => entry.Record);
        Conferences = snapshot.League.Conferences
            .Select(conference => new TeamDirectoryConferenceViewModel(
                conference.Name,
                conference.Divisions
                    .Select(division => new TeamDirectoryDivisionViewModel(
                        division.Name,
                        division.Teams.Select(team => CreateRow(snapshot, team, records[team.Id])).ToList()))
                    .ToList()))
            .ToList();
        OnPropertyChanged(nameof(Subtitle));
    }

    private TeamDirectoryRowViewModel CreateRow(GameSnapshot snapshot, TeamSnapshot team, TeamRecordSnapshot record) =>
        new(
            team.Id,
            team.Name,
            team.Colours.Primary,
            team.Id == snapshot.ManagedTeamId,
            string.Create(CultureInfo.CurrentCulture, $"{record.Wins}-{record.RegulationLosses}-{record.OvertimeLosses + record.ShootoutLosses}"),
            record.Points,
            new RelayCommand(() => _openTeam(team.Id)));
}

/// <summary>A conference's divisions in the directory.</summary>
public sealed record TeamDirectoryConferenceViewModel(string Name, IReadOnlyList<TeamDirectoryDivisionViewModel> Divisions);

/// <summary>A division's teams in the directory, in league order.</summary>
public sealed record TeamDirectoryDivisionViewModel(string Name, IReadOnlyList<TeamDirectoryRowViewModel> Teams);

/// <param name="Colour">The team's primary colour, shown as a swatch beside its name.</param>
/// <param name="Record">Wins, regulation losses, and overtime and shootout losses, such as "5-3-1".</param>
/// <param name="OpenTeamCommand">Opens the team's page.</param>
public sealed record TeamDirectoryRowViewModel(
    TeamId Id,
    string Name,
    Colour Colour,
    bool IsManaged,
    string Record,
    int Points,
    ICommand OpenTeamCommand);

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