using System.Collections.ObjectModel;

using HockeySim.Domain;

namespace HockeySim.Management.GameManagement.Snapshots;

/// <summary>
/// The season's progress: the current date and phase, completed results, and current-season totals.
/// </summary>
public sealed class SeasonSnapshot
{
    private readonly ReadOnlyCollection<CompletedMatchSnapshot> _preseasonResults;
    private readonly ReadOnlyCollection<CompletedMatchSnapshot> _results;
    private readonly ReadOnlyCollection<TeamRecordSnapshot> _teamRecords;
    private readonly ReadOnlyCollection<TeamSeasonStatisticsSnapshot> _teamStatistics;
    private readonly ReadOnlyCollection<SkaterSeasonStatisticsSnapshot> _skaterStatistics;
    private readonly ReadOnlyCollection<GoalieSeasonStatisticsSnapshot> _goalieStatistics;

    private SeasonSnapshot(
        DateOnly currentDate,
        SeasonPhase phase,
        bool isComplete,
        IReadOnlyList<CompletedMatchSnapshot> preseasonResults,
        IReadOnlyList<CompletedMatchSnapshot> results,
        IReadOnlyList<TeamRecordSnapshot> teamRecords,
        IReadOnlyList<TeamSeasonStatisticsSnapshot> teamStatistics,
        StandingsSnapshot standings,
        IReadOnlyList<SkaterSeasonStatisticsSnapshot> skaterStatistics,
        IReadOnlyList<GoalieSeasonStatisticsSnapshot> goalieStatistics)
    {
        CurrentDate = currentDate;
        Phase = phase;
        IsComplete = isComplete;
        _preseasonResults = new ReadOnlyCollection<CompletedMatchSnapshot>(preseasonResults.ToList());
        _results = new ReadOnlyCollection<CompletedMatchSnapshot>(results.ToList());
        _teamRecords = new ReadOnlyCollection<TeamRecordSnapshot>(teamRecords.ToList());
        _teamStatistics = new ReadOnlyCollection<TeamSeasonStatisticsSnapshot>(teamStatistics.ToList());
        Standings = standings;
        _skaterStatistics = new ReadOnlyCollection<SkaterSeasonStatisticsSnapshot>(skaterStatistics.ToList());
        _goalieStatistics = new ReadOnlyCollection<GoalieSeasonStatisticsSnapshot>(goalieStatistics.ToList());
    }

    /// <summary>
    /// The next league day to be played, or the day after the final scheduled match once the
    /// season is complete.
    /// </summary>
    public DateOnly CurrentDate { get; }

    /// <summary>The phase of the current date: the preseason until opening day.</summary>
    public SeasonPhase Phase { get; }

    /// <summary>
    /// Whether every regular-season match has been played. A complete season cannot be advanced.
    /// </summary>
    public bool IsComplete { get; }

    /// <summary>
    /// Every completed preseason match, in schedule order. They count toward no record, statistic,
    /// or standings.
    /// </summary>
    public IReadOnlyList<CompletedMatchSnapshot> PreseasonResults => _preseasonResults;

    /// <summary>Every completed regular-season match, in schedule order.</summary>
    public IReadOnlyList<CompletedMatchSnapshot> Results => _results;

    /// <summary>Every team's record, in league team order; see <see cref="Standings"/> for rankings.</summary>
    public IReadOnlyList<TeamRecordSnapshot> TeamRecords => _teamRecords;

    /// <summary>Every team's special teams, faceoff, and shot totals, in league team order.</summary>
    public IReadOnlyList<TeamSeasonStatisticsSnapshot> TeamStatistics => _teamStatistics;

    /// <summary>League, conference, and division standings from the results so far.</summary>
    public StandingsSnapshot Standings { get; }

    /// <summary>Every skater who has appeared, in league team and roster order.</summary>
    public IReadOnlyList<SkaterSeasonStatisticsSnapshot> SkaterStatistics => _skaterStatistics;

    /// <summary>Every goalie who has started, in league team and roster order.</summary>
    public IReadOnlyList<GoalieSeasonStatisticsSnapshot> GoalieStatistics => _goalieStatistics;

    internal static SeasonSnapshot Create(Season season) =>
        new(
            season.CurrentDate,
            season.Phase,
            season.IsComplete,
            season.PreseasonMatches.Select(CompletedMatchSnapshot.Create).ToList(),
            season.CompletedMatches.Select(CompletedMatchSnapshot.Create).ToList(),
            season.TeamRecords.Select(TeamRecordSnapshot.Create).ToList(),
            season.TeamStatistics.Select(TeamSeasonStatisticsSnapshot.Create).ToList(),
            StandingsSnapshot.Create(season),
            season.SkaterStatistics.Select(SkaterSeasonStatisticsSnapshot.Create).ToList(),
            season.GoalieStatistics.Select(GoalieSeasonStatisticsSnapshot.Create).ToList());
}

/// <param name="Goals">The scoring summary: every goal scored by a player, in the order scored.</param>
/// <param name="Penalties">The penalty summary: every penalty assessed, in the order called.</param>
public sealed record CompletedMatchSnapshot(
    DateOnly Date,
    MatchDecision Decision,
    CompletedMatchTeamSnapshot Home,
    CompletedMatchTeamSnapshot Away,
    IReadOnlyList<GoalSnapshot> Goals,
    IReadOnlyList<PenaltySnapshot> Penalties)
{
    public TeamId WinnerId => Home.Score > Away.Score ? Home.TeamId : Away.TeamId;

    internal static CompletedMatchSnapshot Create(CompletedMatch match) =>
        new(
            match.Date,
            match.Decision,
            CompletedMatchTeamSnapshot.Create(match.Home),
            CompletedMatchTeamSnapshot.Create(match.Away),
            match.Goals.Select(GoalSnapshot.Create).ToList().AsReadOnly(),
            match.Penalties.Select(PenaltySnapshot.Create).ToList().AsReadOnly());
}

/// <param name="Period">One to three for regulation, then overtime.</param>
/// <param name="TimeInPeriod">Elapsed time in the period.</param>
/// <param name="IsEmptyNet">Scored while the conceding team's goalie was pulled.</param>
public sealed record GoalSnapshot(
    int Period,
    TimeSpan TimeInPeriod,
    TeamId TeamId,
    PlayerId ScorerId,
    PlayerId? PrimaryAssistId,
    PlayerId? SecondaryAssistId,
    GoalSituation Situation,
    bool IsEmptyNet)
{
    internal static GoalSnapshot Create(MatchGoal goal) =>
        new(
            goal.Period,
            goal.TimeInPeriod,
            goal.TeamId,
            goal.ScorerId,
            goal.PrimaryAssistId,
            goal.SecondaryAssistId,
            goal.Situation,
            goal.IsEmptyNet);
}

/// <param name="Period">One to three for regulation, then overtime.</param>
/// <param name="TimeInPeriod">Elapsed time in the period when play stopped for the penalty.</param>
/// <param name="Minutes">The penalty minutes charged; none for a penalty shot.</param>
public sealed record PenaltySnapshot(
    int Period,
    TimeSpan TimeInPeriod,
    TeamId TeamId,
    PlayerId PlayerId,
    Infraction Infraction,
    PenaltyKind Kind,
    int Minutes)
{
    internal static PenaltySnapshot Create(MatchPenalty penalty) =>
        new(penalty.Period, penalty.TimeInPeriod, penalty.TeamId, penalty.PlayerId, penalty.Infraction, penalty.Kind, penalty.Minutes);
}

public sealed class CompletedMatchTeamSnapshot
{
    private readonly ReadOnlyCollection<SkaterBoxScoreSnapshot> _skaters;

    private CompletedMatchTeamSnapshot(
        TeamId teamId,
        int score,
        int shots,
        int powerPlayOpportunities,
        IReadOnlyList<SkaterBoxScoreSnapshot> skaters,
        GoalieBoxScoreSnapshot goalie,
        SituationalShotTotals shotTotals)
    {
        TeamId = teamId;
        Score = score;
        Shots = shots;
        PowerPlayOpportunities = powerPlayOpportunities;
        _skaters = new ReadOnlyCollection<SkaterBoxScoreSnapshot>(skaters.ToList());
        Goalie = goalie;
        ShotTotals = shotTotals;
    }

    public TeamId TeamId { get; }

    /// <summary>The final score, including the deciding goal for a shootout winner.</summary>
    public int Score { get; }

    /// <summary>Shots on goal in regulation and overtime; shootout attempts are excluded.</summary>
    public int Shots { get; }

    /// <summary>Opponent penalties that gave the team a manpower advantage, each counted once.</summary>
    public int PowerPlayOpportunities { get; }

    public int PowerPlayGoals => _skaters.Sum(skater => skater.PowerPlayGoals);

    public int PenaltyMinutes => _skaters.Sum(skater => skater.PenaltyMinutes);

    public IReadOnlyList<SkaterBoxScoreSnapshot> Skaters => _skaters;

    public GoalieBoxScoreSnapshot Goalie { get; }

    /// <summary>
    /// Both teams' shot totals by strength situation from this team's side, excluding penalty shots.
    /// </summary>
    public SituationalShotTotals ShotTotals { get; }

    internal static CompletedMatchTeamSnapshot Create(CompletedMatchTeam team) =>
        new(
            team.TeamId,
            team.Score,
            team.Shots,
            team.PowerPlayOpportunities,
            team.Skaters.Select(SkaterBoxScoreSnapshot.Create).ToList(),
            GoalieBoxScoreSnapshot.Create(team.Goalie),
            team.ShotTotals);
}

/// <param name="PlusMinus">
/// Even-strength and shorthanded goals for, less those against, while on the ice; power-play and
/// penalty-shot goals do not count.
/// </param>
/// <param name="PowerPlayGoals">Power-play goals, counted among the goals.</param>
/// <param name="ShorthandedGoals">Shorthanded goals, counted among the goals.</param>
/// <param name="EmptyNetGoals">Goals into a net whose goalie was pulled, counted among the goals.</param>
/// <param name="Shots">Shots on goal, including goals.</param>
/// <param name="ShotAttempts">Shots on goal, missed shots, and blocked attempts.</param>
/// <param name="BlockedShots">The opponent's attempts this skater blocked.</param>
/// <param name="OnIce">
/// Both teams' shot totals while the skater was on the ice, by strength situation; penalty shots
/// are not counted.
/// </param>
public sealed record SkaterBoxScoreSnapshot(
    PlayerId PlayerId,
    int Goals,
    int Assists,
    int PlusMinus,
    TimeSpan TimeOnIce,
    int Shots,
    int ShotAttempts,
    int Hits,
    int BlockedShots,
    int FaceoffsWon,
    int FaceoffsLost,
    int Takeaways,
    int Giveaways,
    double ExpectedGoals,
    int PenaltyMinutes,
    int PowerPlayGoals,
    int PowerPlayAssists,
    int ShorthandedGoals,
    int ShorthandedAssists,
    int EmptyNetGoals,
    SituationalShotTotals OnIce)
{
    public int Points => Goals + Assists;

    public int PowerPlayPoints => PowerPlayGoals + PowerPlayAssists;

    public int ShorthandedPoints => ShorthandedGoals + ShorthandedAssists;

    internal static SkaterBoxScoreSnapshot Create(SkaterBoxScore boxScore) =>
        new(
            boxScore.PlayerId,
            boxScore.Goals,
            boxScore.Assists,
            boxScore.PlusMinus,
            boxScore.TimeOnIce,
            boxScore.Shots,
            boxScore.ShotAttempts,
            boxScore.Hits,
            boxScore.BlockedShots,
            boxScore.FaceoffsWon,
            boxScore.FaceoffsLost,
            boxScore.Takeaways,
            boxScore.Giveaways,
            boxScore.ExpectedGoals,
            boxScore.PenaltyMinutes,
            boxScore.PowerPlayGoals,
            boxScore.PowerPlayAssists,
            boxScore.ShorthandedGoals,
            boxScore.ShorthandedAssists,
            boxScore.EmptyNetGoals,
            boxScore.OnIce);
}

public sealed record GoalieBoxScoreSnapshot(
    PlayerId PlayerId,
    int ShotsAgainst,
    int GoalsAgainst,
    double ExpectedGoalsAgainst,
    TimeSpan TimeOnIce)
{
    public int Saves => ShotsAgainst - GoalsAgainst;

    internal static GoalieBoxScoreSnapshot Create(GoalieBoxScore boxScore) =>
        new(
            boxScore.PlayerId,
            boxScore.ShotsAgainst,
            boxScore.GoalsAgainst,
            boxScore.ExpectedGoalsAgainst,
            boxScore.TimeOnIce);
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

    /// <summary>Wins excluding shootout wins (the standings "ROW" column).</summary>
    public int RegulationAndOvertimeWins => RegulationWins + OvertimeWins;

    public int Losses => RegulationLosses + OvertimeLosses + ShootoutLosses;

    public int GoalDifferential => GoalsFor - GoalsAgainst;

    /// <summary>
    /// Points as a share of the points available, or <see langword="null"/> before any game.
    /// </summary>
    public double? PointsPercentage =>
        GamesPlayed == 0 ? null : Points / (double)(TeamRecord.PointsPerWin * GamesPlayed);

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

/// <summary>
/// A skater's current-season totals. The rates and percentages are <see langword="null"/> until
/// they are defined: time on ice per game before an appearance, faceoff percentage before a
/// faceoff.
/// </summary>
/// <param name="OnIce">Both teams' shot totals while the skater was on the ice, by strength situation.</param>
public sealed record SkaterSeasonStatisticsSnapshot(
    PlayerId PlayerId,
    TeamId TeamId,
    int GamesPlayed,
    int Goals,
    int Assists,
    int PlusMinus,
    TimeSpan TimeOnIce,
    TimeSpan? TimeOnIcePerGame,
    int Shots,
    int ShotAttempts,
    int Hits,
    int BlockedShots,
    int FaceoffsWon,
    int FaceoffsLost,
    double? FaceoffPercentage,
    int Takeaways,
    int Giveaways,
    double ExpectedGoals,
    int PenaltyMinutes,
    int PowerPlayGoals,
    int PowerPlayAssists,
    int ShorthandedGoals,
    int ShorthandedAssists,
    int EmptyNetGoals,
    SituationalShotTotals OnIce)
{
    public int Points => Goals + Assists;

    public int PowerPlayPoints => PowerPlayGoals + PowerPlayAssists;

    public int ShorthandedPoints => ShorthandedGoals + ShorthandedAssists;

    internal static SkaterSeasonStatisticsSnapshot Create(SkaterSeasonStatistics statistics) =>
        new(
            statistics.PlayerId,
            statistics.TeamId,
            statistics.GamesPlayed,
            statistics.Goals,
            statistics.Assists,
            statistics.PlusMinus,
            statistics.TimeOnIce,
            statistics.TimeOnIcePerGame,
            statistics.Shots,
            statistics.ShotAttempts,
            statistics.Hits,
            statistics.BlockedShots,
            statistics.FaceoffsWon,
            statistics.FaceoffsLost,
            statistics.FaceoffPercentage,
            statistics.Takeaways,
            statistics.Giveaways,
            statistics.ExpectedGoals,
            statistics.PenaltyMinutes,
            statistics.PowerPlayGoals,
            statistics.PowerPlayAssists,
            statistics.ShorthandedGoals,
            statistics.ShorthandedAssists,
            statistics.EmptyNetGoals,
            statistics.OnIce);
}

/// <summary>
/// A goalie's current-season totals. Save percentage, goals-against average, and time on ice per
/// game are <see langword="null"/> until defined.
/// </summary>
/// <param name="GoalsAgainstAverage">Goals against per sixty minutes in net.</param>
/// <param name="GoalsSavedAboveExpected">Expected goals against less goals against (GSAx).</param>
/// <param name="Shutouts">Starts in which the goalie was charged with no goal.</param>
public sealed record GoalieSeasonStatisticsSnapshot(
    PlayerId PlayerId,
    TeamId TeamId,
    int GamesPlayed,
    int ShotsAgainst,
    int GoalsAgainst,
    double? SavePercentage,
    double ExpectedGoalsAgainst,
    double GoalsSavedAboveExpected,
    double? GoalsAgainstAverage,
    int Shutouts,
    TimeSpan TimeOnIce,
    TimeSpan? TimeOnIcePerGame)
{
    public int Saves => ShotsAgainst - GoalsAgainst;

    internal static GoalieSeasonStatisticsSnapshot Create(GoalieSeasonStatistics statistics) =>
        new(
            statistics.PlayerId,
            statistics.TeamId,
            statistics.GamesPlayed,
            statistics.ShotsAgainst,
            statistics.GoalsAgainst,
            statistics.SavePercentage,
            statistics.ExpectedGoalsAgainst,
            statistics.GoalsSavedAboveExpected,
            statistics.GoalsAgainstAverage,
            statistics.Shutouts,
            statistics.TimeOnIce,
            statistics.TimeOnIcePerGame);
}

/// <summary>
/// A team's current-season special teams, faceoffs, and shot totals. Percentages are
/// <see langword="null"/> until defined.
/// </summary>
/// <param name="TimesShorthanded">The opponents' power-play opportunities.</param>
/// <param name="PenaltyKillPercentage">The share of times shorthanded without conceding a power-play goal.</param>
/// <param name="ShotTotals">Both teams' shot totals by strength situation, from this team's side.</param>
public sealed record TeamSeasonStatisticsSnapshot(
    TeamId TeamId,
    int GamesPlayed,
    int PowerPlayGoals,
    int PowerPlayOpportunities,
    double? PowerPlayPercentage,
    int TimesShorthanded,
    int PowerPlayGoalsAgainst,
    double? PenaltyKillPercentage,
    int ShorthandedGoals,
    int FaceoffsWon,
    int FaceoffsLost,
    double? FaceoffPercentage,
    SituationalShotTotals ShotTotals)
{
    internal static TeamSeasonStatisticsSnapshot Create(TeamSeasonStatistics statistics) =>
        new(
            statistics.TeamId,
            statistics.GamesPlayed,
            statistics.PowerPlayGoals,
            statistics.PowerPlayOpportunities,
            statistics.PowerPlayPercentage,
            statistics.TimesShorthanded,
            statistics.PowerPlayGoalsAgainst,
            statistics.PenaltyKillPercentage,
            statistics.ShorthandedGoals,
            statistics.FaceoffsWon,
            statistics.FaceoffsLost,
            statistics.FaceoffPercentage,
            statistics.ShotTotals);
}