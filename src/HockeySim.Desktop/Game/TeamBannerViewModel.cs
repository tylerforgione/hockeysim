using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Players;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.Standings;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Game;

/// <summary>
/// The managed team's banner under the menu bar: its identity, record and standing, and the next
/// match, last result, and current streak.
/// </summary>
public sealed partial class TeamBannerViewModel : ObservableObject
{
    private const string None = "—";

    private readonly GameSession _session;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<BannerFigureViewModel> _figures = [];

    public TeamBannerViewModel(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        Refresh();
    }

    public string TeamName => _session.ManagedTeam.Name;

    public string TeamInitials => PlayerDisplay.TeamInitials(TeamName);

    public void Refresh()
    {
        var snapshot = _session.Snapshot;
        var team = _session.ManagedTeam;
        var (conference, division) = _session.FindDivision(team);
        var conferenceStandings = snapshot.Season.Standings.Conferences.Single(standings => standings.Name == conference.Name);
        var divisionEntry = conferenceStandings.Divisions
            .Single(standings => standings.Name == division.Name).Teams
            .Single(entry => entry.Record.TeamId == team.Id);
        var conferenceEntry = conferenceStandings.Teams.Single(entry => entry.Record.TeamId == team.Id);
        var record = divisionEntry.Record;

        Summary = string.Create(
            CultureInfo.CurrentCulture,
            $"{record.Wins}-{record.RegulationLosses}-{record.OvertimeLosses + record.ShootoutLosses} · {record.Points} pts · {Ordinal(divisionEntry.Rank)} in {division.Name} · {Ordinal(conferenceEntry.Rank)} in {conference.Name}");

        // The streak counts regular-season games only; the last result covers every phase.
        var season = snapshot.Season;
        var lastResult = season.PreseasonResults
            .Concat(season.Results)
            .Concat(season.Playoffs?.Results ?? [])
            .LastOrDefault(result => Involves(result, team.Id));
        var regularSeasonResults = season.Results.Where(result => Involves(result, team.Id)).ToList();
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

    private static bool Involves(CompletedMatchSnapshot result, TeamId teamId) =>
        result.Home.TeamId == teamId || result.Away.TeamId == teamId;

    private static string Ordinal(int rank)
    {
        var suffix = (rank % 100) switch
        {
            11 or 12 or 13 => "th",
            _ => (rank % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th",
            },
        };
        return string.Create(CultureInfo.CurrentCulture, $"{rank}{suffix}");
    }
}

/// <summary>One of the key figures on the right of a banner, such as the next match.</summary>
public sealed record BannerFigureViewModel(string Label, string Value);