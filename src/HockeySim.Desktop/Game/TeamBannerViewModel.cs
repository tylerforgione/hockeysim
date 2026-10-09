using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Players;
using HockeySim.Desktop.Schedule;
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

        var results = snapshot.Season.Results.Where(result => Involves(result, team.Id)).ToList();
        Figures =
        [
            new("NEXT", NextMatch(snapshot, team.Id)),
            new("LAST", results.Count == 0 ? None : LastResult(results[^1], team.Id)),
            new("STREAK", Streak(results, team.Id)),
        ];
    }

    private string NextMatch(GameSnapshot snapshot, TeamId teamId)
    {
        var season = snapshot.Season;
        var next = season.IsComplete
            ? null
            : snapshot.Schedule.Matches.FirstOrDefault(match =>
                match.Date >= season.CurrentDate && (match.HomeTeamId == teamId || match.AwayTeamId == teamId));
        if (next is null)
        {
            return None;
        }

        var when = next.Date == season.CurrentDate ? "Today" : next.Date.ToString("ddd", CultureInfo.CurrentCulture);
        return $"{Opponent(next.HomeTeamId, next.AwayTeamId, teamId)} · {when}";
    }

    private string LastResult(CompletedMatchSnapshot result, TeamId teamId) =>
        $"{MatchDisplay.ResultFor(result, teamId)} {Opponent(result.Home.TeamId, result.Away.TeamId, teamId)}";

    private string Opponent(TeamId homeTeamId, TeamId awayTeamId, TeamId teamId) =>
        homeTeamId == teamId
            ? $"vs {PlayerDisplay.TeamInitials(_session.GetTeam(awayTeamId).Name)}"
            : $"@ {PlayerDisplay.TeamInitials(_session.GetTeam(homeTeamId).Name)}";

    /// <summary>
    /// The run of like results ending with the latest: wins (W), regulation losses (L), or overtime
    /// and shootout losses (OT), as standings count them.
    /// </summary>
    private static string Streak(List<CompletedMatchSnapshot> results, TeamId teamId)
    {
        if (results.Count == 0)
        {
            return None;
        }

        var kind = StreakKind(results[^1], teamId);
        var length = 0;
        for (var index = results.Count - 1; index >= 0 && StreakKind(results[index], teamId) == kind; index--)
        {
            length++;
        }

        return string.Create(CultureInfo.CurrentCulture, $"{kind}{length}");
    }

    private static string StreakKind(CompletedMatchSnapshot result, TeamId teamId) =>
        result.WinnerId == teamId ? "W" : result.Decision == MatchDecision.Regulation ? "L" : "OT";

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