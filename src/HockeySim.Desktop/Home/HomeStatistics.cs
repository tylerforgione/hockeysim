using HockeySim.Desktop.Players;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Home;

/// <summary>
/// Which statistics the home page's panels show, and where to find them. The playoff figures show
/// once the managed team has a playoff series, and still after it is out; a team that missed the
/// playoffs keeps the regular season's.
/// </summary>
public static class HomeStatistics
{
    public static StatisticsSet For(GameSnapshot snapshot, TeamId teamId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot.Season.Playoffs is { } playoffs
            && playoffs.Series.Any(series => series.HigherRanked.TeamId == teamId || series.LowerRanked.TeamId == teamId)
                ? StatisticsSet.Playoffs
                : StatisticsSet.RegularSeason;
    }

    public static IReadOnlyList<TeamRecordSnapshot> Records(SeasonSnapshot season, StatisticsSet statistics) =>
        statistics == StatisticsSet.Playoffs ? season.Playoffs?.TeamRecords ?? [] : season.TeamRecords;

    public static IReadOnlyList<TeamSeasonStatisticsSnapshot> TeamStatistics(SeasonSnapshot season, StatisticsSet statistics) =>
        statistics == StatisticsSet.Playoffs ? season.Playoffs?.TeamStatistics ?? [] : season.TeamStatistics;

    public static IEnumerable<SkaterSeasonStatisticsSnapshot> Skaters(SeasonSnapshot season, StatisticsSet statistics, TeamId teamId) =>
        (statistics == StatisticsSet.Playoffs ? season.Playoffs?.SkaterStatistics ?? [] : season.SkaterStatistics)
            .Where(skater => skater.TeamId == teamId);

    public static IEnumerable<GoalieSeasonStatisticsSnapshot> Goalies(SeasonSnapshot season, StatisticsSet statistics, TeamId teamId) =>
        (statistics == StatisticsSet.Playoffs ? season.Playoffs?.GoalieStatistics ?? [] : season.GoalieStatistics)
            .Where(goalie => goalie.TeamId == teamId);
}