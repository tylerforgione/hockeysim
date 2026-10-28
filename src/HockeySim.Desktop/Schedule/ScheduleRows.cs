using HockeySim.Desktop.Game;
using HockeySim.Desktop.Tables;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Schedule;

/// <summary>
/// Builds a team's schedule rows, shared by the Schedule page and the schedule on a team page.
/// </summary>
public static class ScheduleRows
{
    /// <summary>Puts rows (in any order) into date order, then the table's sort.</summary>
    public static List<ScheduleMatchRowViewModel> Sorted(IEnumerable<ScheduleMatchRowViewModel> rows, TableSort? sort) =>
        TableSorting.Apply(rows.OrderBy(row => row.Date), ScheduleTableColumns.Matches, sort);

    /// <summary>
    /// Lists the team's matches in date order, then the table's sort. The phases follow one another and a team plays at
    /// most once a day, so a date identifies a team's match in any phase. Opponents link to their team page through
    /// <paramref name="openTeam"/>.
    /// </summary>
    public static List<ScheduleMatchRowViewModel> Create(GameSession session, TeamId teamId, TableSort? sort, Action<TeamId>? openTeam = null)
    {
        var snapshot = session.Snapshot;
        var season = snapshot.Season;
        var results = AllResults(snapshot)
            .Where(result => TeamResults.Involves(result, teamId))
            .ToDictionary(result => result.Date);
        var matches = snapshot.Schedule.PreseasonMatches.Select(match => (Match: match, Phase: SeasonPhase.Preseason))
            .Concat(snapshot.Schedule.Matches.Select(match => (Match: match, Phase: SeasonPhase.RegularSeason)))
            .Concat(snapshot.Schedule.PlayoffMatches.Select(match => (Match: match, Phase: SeasonPhase.Playoffs)))
            .Where(entry => entry.Match.HomeTeamId == teamId || entry.Match.AwayTeamId == teamId)
            .ToList();
        var nextDate = season.IsComplete
            ? (DateOnly?)null
            : matches
                .Where(entry => entry.Match.Date >= season.CurrentDate)
                .Select(entry => (DateOnly?)entry.Match.Date)
                .FirstOrDefault();

        return Sorted(matches
            .Select(entry =>
            {
                var match = entry.Match;
                var isHome = match.HomeTeamId == teamId;
                var opponent = session.GetTeam(isHome ? match.AwayTeamId : match.HomeTeamId);
                results.TryGetValue(match.Date, out var result);
                var (phaseLabel, phaseDescription) = MatchDisplay.PhaseOf(entry.Phase, season.Playoffs, match.Date, teamId);
                return new ScheduleMatchRowViewModel(
                    match.Date,
                    match.HomeTeamId,
                    entry.Phase,
                    phaseLabel,
                    phaseDescription,
                    MatchDisplay.ShortDate(match.Date),
                    isHome ? "vs" : "@",
                    opponent.Name,
                    $"{session.GetTeam(match.AwayTeamId).Name} @ {session.GetTeam(match.HomeTeamId).Name}",
                    result is null ? string.Empty : MatchDisplay.ResultFor(result, teamId),
                    result is not null && result.WinnerId == teamId,
                    result is not null,
                    match.Date == nextDate,
                    result is null ? null : GoalDifferential(result, teamId))
                {
                    OpenOpponentCommand = NameLinks.Open(openTeam, opponent.Id),
                };
            }), sort);
    }

    private static int GoalDifferential(CompletedMatchSnapshot result, TeamId teamId) =>
        result.Home.TeamId == teamId
            ? result.Home.Score - result.Away.Score
            : result.Away.Score - result.Home.Score;

    /// <summary>Gets every completed match: preseason, regular season, then playoffs.</summary>
    public static IEnumerable<CompletedMatchSnapshot> AllResults(GameSnapshot snapshot)
    {
        var season = snapshot.Season;
        return season.PreseasonResults.Concat(season.Results).Concat(season.Playoffs?.Results ?? []);
    }
}