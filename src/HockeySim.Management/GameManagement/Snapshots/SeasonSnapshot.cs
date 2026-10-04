using System.Collections.ObjectModel;

using HockeySim.Domain;

namespace HockeySim.Management.GameManagement.Snapshots;

/// <summary>
/// The regular season's progress: the current date, completed results, and current-season totals.
/// </summary>
public sealed class SeasonSnapshot
{
    private readonly ReadOnlyCollection<CompletedMatchSnapshot> _results;
    private readonly ReadOnlyCollection<TeamRecordSnapshot> _teamRecords;
    private readonly ReadOnlyCollection<SkaterSeasonStatisticsSnapshot> _skaterStatistics;
    private readonly ReadOnlyCollection<GoalieSeasonStatisticsSnapshot> _goalieStatistics;

    private SeasonSnapshot(
        DateOnly currentDate,
        bool isComplete,
        IReadOnlyList<CompletedMatchSnapshot> results,
        IReadOnlyList<TeamRecordSnapshot> teamRecords,
        IReadOnlyList<SkaterSeasonStatisticsSnapshot> skaterStatistics,
        IReadOnlyList<GoalieSeasonStatisticsSnapshot> goalieStatistics)
    {
        CurrentDate = currentDate;
        IsComplete = isComplete;
        _results = new ReadOnlyCollection<CompletedMatchSnapshot>(results.ToList());
        _teamRecords = new ReadOnlyCollection<TeamRecordSnapshot>(teamRecords.ToList());
        _skaterStatistics = new ReadOnlyCollection<SkaterSeasonStatisticsSnapshot>(skaterStatistics.ToList());
        _goalieStatistics = new ReadOnlyCollection<GoalieSeasonStatisticsSnapshot>(goalieStatistics.ToList());
    }

    /// <summary>
    /// The next league day to be played, or the day after the final scheduled match once the
    /// season is complete.
    /// </summary>
    public DateOnly CurrentDate { get; }

    /// <summary>
    /// Whether every scheduled match has been played. A complete season cannot be advanced.
    /// </summary>
    public bool IsComplete { get; }

    /// <summary>Every completed match, in schedule order.</summary>
    public IReadOnlyList<CompletedMatchSnapshot> Results => _results;

    /// <summary>Every team's record, in league team order; no ranking is applied.</summary>
    public IReadOnlyList<TeamRecordSnapshot> TeamRecords => _teamRecords;

    /// <summary>Every skater who has appeared, in league team and roster order.</summary>
    public IReadOnlyList<SkaterSeasonStatisticsSnapshot> SkaterStatistics => _skaterStatistics;

    /// <summary>Every goalie who has started, in league team and roster order.</summary>
    public IReadOnlyList<GoalieSeasonStatisticsSnapshot> GoalieStatistics => _goalieStatistics;

    internal static SeasonSnapshot Create(Season season) =>
        new(
            season.CurrentDate,
            season.IsComplete,
            season.CompletedMatches.Select(CompletedMatchSnapshot.Create).ToList(),
            season.TeamRecords.Select(TeamRecordSnapshot.Create).ToList(),
            season.SkaterStatistics.Select(SkaterSeasonStatisticsSnapshot.Create).ToList(),
            season.GoalieStatistics.Select(GoalieSeasonStatisticsSnapshot.Create).ToList());
}

public sealed record CompletedMatchSnapshot(
    DateOnly Date,
    MatchDecision Decision,
    CompletedMatchTeamSnapshot Home,
    CompletedMatchTeamSnapshot Away)
{
    public TeamId WinnerId => Home.Score > Away.Score ? Home.TeamId : Away.TeamId;

    internal static CompletedMatchSnapshot Create(CompletedMatch match) =>
        new(
            match.Date,
            match.Decision,
            CompletedMatchTeamSnapshot.Create(match.Home),
            CompletedMatchTeamSnapshot.Create(match.Away));
}

public sealed class CompletedMatchTeamSnapshot
{
    private readonly ReadOnlyCollection<SkaterBoxScoreSnapshot> _skaters;

    private CompletedMatchTeamSnapshot(
        TeamId teamId,
        int score,
        int shots,
        IReadOnlyList<SkaterBoxScoreSnapshot> skaters,
        GoalieBoxScoreSnapshot goalie)
    {
        TeamId = teamId;
        Score = score;
        Shots = shots;
        _skaters = new ReadOnlyCollection<SkaterBoxScoreSnapshot>(skaters.ToList());
        Goalie = goalie;
    }

    public TeamId TeamId { get; }

    /// <summary>The final score, including the deciding goal for a shootout winner.</summary>
    public int Score { get; }

    /// <summary>Shots on goal in regulation and overtime; shootout attempts are excluded.</summary>
    public int Shots { get; }

    public IReadOnlyList<SkaterBoxScoreSnapshot> Skaters => _skaters;

    public GoalieBoxScoreSnapshot Goalie { get; }

    internal static CompletedMatchTeamSnapshot Create(CompletedMatchTeam team) =>
        new(
            team.TeamId,
            team.Score,
            team.Shots,
            team.Skaters.Select(SkaterBoxScoreSnapshot.Create).ToList(),
            GoalieBoxScoreSnapshot.Create(team.Goalie));
}

public sealed record SkaterBoxScoreSnapshot(PlayerId PlayerId, int Goals, int Assists)
{
    public int Points => Goals + Assists;

    internal static SkaterBoxScoreSnapshot Create(SkaterBoxScore boxScore) =>
        new(boxScore.PlayerId, boxScore.Goals, boxScore.Assists);
}

public sealed record GoalieBoxScoreSnapshot(PlayerId PlayerId, int ShotsAgainst, int GoalsAgainst)
{
    public int Saves => ShotsAgainst - GoalsAgainst;

    internal static GoalieBoxScoreSnapshot Create(GoalieBoxScore boxScore) =>
        new(boxScore.PlayerId, boxScore.ShotsAgainst, boxScore.GoalsAgainst);
}

/// <summary>
/// A team's current-season record. Overtime and shootout losses are kept separately; both earn
/// one standings point. Goals include shootout deciding goals.
/// </summary>
public sealed record TeamRecordSnapshot(
    TeamId TeamId,
    int GamesPlayed,
    int RegulationWins,
    int OvertimeWins,
    int ShootoutWins,
    int RegulationLosses,
    int OvertimeLosses,
    int ShootoutLosses,
    int Points,
    int GoalsFor,
    int GoalsAgainst)
{
    public int Wins => RegulationWins + OvertimeWins + ShootoutWins;

    internal static TeamRecordSnapshot Create(TeamRecord record) =>
        new(
            record.TeamId,
            record.GamesPlayed,
            record.RegulationWins,
            record.OvertimeWins,
            record.ShootoutWins,
            record.RegulationLosses,
            record.OvertimeLosses,
            record.ShootoutLosses,
            record.Points,
            record.GoalsFor,
            record.GoalsAgainst);
}

public sealed record SkaterSeasonStatisticsSnapshot(
    PlayerId PlayerId,
    TeamId TeamId,
    int GamesPlayed,
    int Goals,
    int Assists)
{
    public int Points => Goals + Assists;

    internal static SkaterSeasonStatisticsSnapshot Create(SkaterSeasonStatistics statistics) =>
        new(statistics.PlayerId, statistics.TeamId, statistics.GamesPlayed, statistics.Goals, statistics.Assists);
}

public sealed record GoalieSeasonStatisticsSnapshot(
    PlayerId PlayerId,
    TeamId TeamId,
    int GamesPlayed,
    int ShotsAgainst,
    int GoalsAgainst)
{
    public int Saves => ShotsAgainst - GoalsAgainst;

    /// <summary>Saves as a share of shots against, or <see langword="null"/> before any shot.</summary>
    public double? SavePercentage => ShotsAgainst == 0 ? null : Saves / (double)ShotsAgainst;

    internal static GoalieSeasonStatisticsSnapshot Create(GoalieSeasonStatistics statistics) =>
        new(statistics.PlayerId, statistics.TeamId, statistics.GamesPlayed, statistics.ShotsAgainst, statistics.GoalsAgainst);
}