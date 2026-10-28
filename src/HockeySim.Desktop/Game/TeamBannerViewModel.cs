using System.Globalization;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Players;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.Standings;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Game;

/// <summary>
/// A team's banner: its identity, record and standing, and the next match, last result, and
/// current streak. The managed team's sits under the menu bar; a team page shows its own team's.
/// </summary>
public sealed partial class TeamBannerViewModel : ObservableObject
{
    private const string None = "—";

    private readonly GameSession _session;
    private readonly TeamId _teamId;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<BannerFigureViewModel> _figures = [];

    /// <param name="teamId">The team to show; the managed team without it.</param>
    /// <param name="openTeam">Opens the team's page from its name; the name is not a link without it.</param>
    public TeamBannerViewModel(GameSession session, TeamId? teamId = null, Action<TeamId>? openTeam = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _teamId = teamId ?? session.Snapshot.ManagedTeamId;
        OpenTeamCommand = new RelayCommand(() => openTeam?.Invoke(_teamId), () => openTeam is not null);
        Refresh();
    }

    /// <summary>Shows the team's page.</summary>
    public ICommand OpenTeamCommand { get; }

    public string TeamName => _session.GetTeam(_teamId).Name;

    public TeamColours Colours => _session.GetTeam(_teamId).Colours;

    /// <summary>The team's division and conference, such as "Atlantic · Eastern".</summary>
    public string Division
    {
        get
        {
            var (conference, division) = _session.FindDivision(_session.GetTeam(_teamId));
            return $"{division.Name} · {conference.Name}";
        }
    }

    public string TeamInitials => PlayerDisplay.TeamInitials(TeamName);

    public void Refresh()
    {
        var snapshot = _session.Snapshot;
        var team = _session.GetTeam(_teamId);
        var (conference, division) = _session.FindDivision(team);
        var conferenceStandings = snapshot.Season.Standings.Conferences.Single(standings => standings.Name == conference.Name);
        var divisionEntry = conferenceStandings.Divisions
            .Single(standings => standings.Name == division.Name).Teams
            .Single(entry => entry.Record.TeamId == team.Id);
        var conferenceEntry = conferenceStandings.Teams.Single(entry => entry.Record.TeamId == team.Id);
        var record = divisionEntry.Record;

        Summary = string.Create(
            CultureInfo.CurrentCulture,
            $"{record.Wins}-{record.RegulationLosses}-{record.OvertimeLosses + record.ShootoutLosses} · {record.Points} pts · {MatchDisplay.Ordinal(divisionEntry.Rank)} in {division.Name} · {MatchDisplay.Ordinal(conferenceEntry.Rank)} in {conference.Name}");

        // The streak counts regular-season games only; the last result covers every phase.
        var season = snapshot.Season;
        var lastResult = season.PreseasonResults
            .Concat(season.Results)
            .Concat(season.Playoffs?.Results ?? [])
            .LastOrDefault(result => TeamResults.Involves(result, team.Id));
        var regularSeasonResults = season.Results.Where(result => TeamResults.Involves(result, team.Id)).ToList();
        Figures =
        [
            new("NEXT", NextMatch(snapshot, team.Id)),
            new("LAST", lastResult is null ? None : LastResult(lastResult, team.Id)),
            new("STREAK", TeamForm.Streak(regularSeasonResults, team.Id)),
        ];
    }

    private string NextMatch(GameSnapshot snapshot, TeamId teamId)
    {
        var next = MatchDisplay.NextMatch(snapshot, teamId);
        if (next is null)
        {
            return None;
        }

        var when = next.Date == snapshot.Season.CurrentDate ? "Today" : next.Date.ToString("ddd", CultureInfo.CurrentCulture);
        return $"{Opponent(next.HomeTeamId, next.AwayTeamId, teamId)} · {when}";
    }

    private string LastResult(CompletedMatchSnapshot result, TeamId teamId) =>
        $"{MatchDisplay.ResultFor(result, teamId)} {Opponent(result.Home.TeamId, result.Away.TeamId, teamId)}";

    private string Opponent(TeamId homeTeamId, TeamId awayTeamId, TeamId teamId) =>
        homeTeamId == teamId
            ? $"vs {PlayerDisplay.TeamInitials(_session.GetTeam(awayTeamId).Name)}"
            : $"@ {PlayerDisplay.TeamInitials(_session.GetTeam(homeTeamId).Name)}";
}

/// <summary>One of the key figures on the right of a banner, such as the next match.</summary>
public sealed record BannerFigureViewModel(string Label, string Value);