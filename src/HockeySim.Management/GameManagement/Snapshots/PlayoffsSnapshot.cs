using System.Collections.ObjectModel;

using HockeySim.Domain;

namespace HockeySim.Management.GameManagement.Snapshots;

/// <summary>
/// The playoffs as they stand: every series formed so far, the champion once crowned, and the
/// playoff results, records, and statistics, which are kept apart from the regular season's.
/// </summary>
public sealed class PlayoffsSnapshot
{
    private readonly ReadOnlyCollection<PlayoffSeriesSnapshot> _series;
    private readonly ReadOnlyCollection<CompletedMatchSnapshot> _results;
    private readonly ReadOnlyCollection<TeamRecordSnapshot> _teamRecords;
    private readonly ReadOnlyCollection<TeamSeasonStatisticsSnapshot> _teamStatistics;
    private readonly ReadOnlyCollection<SkaterSeasonStatisticsSnapshot> _skaterStatistics;
    private readonly ReadOnlyCollection<GoalieSeasonStatisticsSnapshot> _goalieStatistics;

    private PlayoffsSnapshot(
        PlayoffRound currentRound,
        TeamId? championId,
        IReadOnlyList<PlayoffSeriesSnapshot> series,
        IReadOnlyList<CompletedMatchSnapshot> results,
        IReadOnlyList<TeamRecordSnapshot> teamRecords,
        IReadOnlyList<TeamSeasonStatisticsSnapshot> teamStatistics,
        IReadOnlyList<SkaterSeasonStatisticsSnapshot> skaterStatistics,
        IReadOnlyList<GoalieSeasonStatisticsSnapshot> goalieStatistics)
    {
        CurrentRound = currentRound;
        ChampionId = championId;
        _series = new ReadOnlyCollection<PlayoffSeriesSnapshot>(series.ToList());
        _results = new ReadOnlyCollection<CompletedMatchSnapshot>(results.ToList());
        _teamRecords = new ReadOnlyCollection<TeamRecordSnapshot>(teamRecords.ToList());
        _teamStatistics = new ReadOnlyCollection<TeamSeasonStatisticsSnapshot>(teamStatistics.ToList());
        _skaterStatistics = new ReadOnlyCollection<SkaterSeasonStatisticsSnapshot>(skaterStatistics.ToList());
        _goalieStatistics = new ReadOnlyCollection<GoalieSeasonStatisticsSnapshot>(goalieStatistics.ToList());
    }

    /// <summary>The latest round to have started.</summary>
    public PlayoffRound CurrentRound { get; }

    /// <summary>The winner of the final, once it is decided.</summary>
    public TeamId? ChampionId { get; }

    /// <summary>
    /// Every series formed so far, round by round, each round in bracket order: by conference,
    /// then by division bracket.
    /// </summary>
    public IReadOnlyList<PlayoffSeriesSnapshot> Series => _series;

    /// <summary>Every completed playoff match, in schedule order.</summary>
    public IReadOnlyList<CompletedMatchSnapshot> Results => _results;

    /// <summary>Every qualifier's playoff record, in league team order. Points mean nothing here.</summary>
    public IReadOnlyList<TeamRecordSnapshot> TeamRecords => _teamRecords;

    /// <summary>Every qualifier's playoff special teams, faceoff, and shot totals, in league team order.</summary>
    public IReadOnlyList<TeamSeasonStatisticsSnapshot> TeamStatistics => _teamStatistics;

    /// <summary>Playoff totals for every skater who has appeared, in league team and roster order.</summary>
    public IReadOnlyList<SkaterSeasonStatisticsSnapshot> SkaterStatistics => _skaterStatistics;

    /// <summary>Playoff totals for every goalie who has started, in league team and roster order.</summary>
    public IReadOnlyList<GoalieSeasonStatisticsSnapshot> GoalieStatistics => _goalieStatistics;

    internal static PlayoffsSnapshot Create(Playoffs playoffs) =>
        new(
            playoffs.CurrentRound,
            playoffs.Champion?.TeamId,
            playoffs.Series.Select(PlayoffSeriesSnapshot.Create).ToList(),
            playoffs.CompletedMatches.Select(CompletedMatchSnapshot.Create).ToList(),
            playoffs.TeamRecords.Select(TeamRecordSnapshot.Create).ToList(),
            playoffs.TeamStatistics.Select(TeamSeasonStatisticsSnapshot.Create).ToList(),
            playoffs.SkaterStatistics.Select(SkaterSeasonStatisticsSnapshot.Create).ToList(),
            playoffs.GoalieStatistics.Select(GoalieSeasonStatisticsSnapshot.Create).ToList());
}

/// <summary>
/// A best-of-seven series. The higher-ranked team has home ice: it hosts games one, two, five,
/// and seven.
/// </summary>
/// <param name="Games">The games played so far, in order.</param>
/// <param name="NextGame">The scheduled game not yet played; none once the series is decided.</param>
public sealed record PlayoffSeriesSnapshot(
    PlayoffRound Round,
    PlayoffSeedSnapshot HigherRanked,
    PlayoffSeedSnapshot LowerRanked,
    int HigherRankedWins,
    int LowerRankedWins,
    TeamId? WinnerId,
    IReadOnlyList<CompletedMatchSnapshot> Games,
    ScheduledMatchSnapshot? NextGame)
{
    internal static PlayoffSeriesSnapshot Create(PlayoffSeries series) =>
        new(
            series.Round,
            PlayoffSeedSnapshot.Create(series.HigherRanked),
            PlayoffSeedSnapshot.Create(series.LowerRanked),
            series.HigherRankedWins,
            series.LowerRankedWins,
            series.Winner?.TeamId,
            series.Games.Select(CompletedMatchSnapshot.Create).ToList().AsReadOnly(),
            series.NextGame is null ? null : ScheduledMatchSnapshot.Create(series.NextGame));
}

/// <summary>How a team qualified: a division finish of one to three, or a wild-card rank of one or two.</summary>
/// <param name="DivisionName">The team's own division, even for a wild card.</param>
public sealed record PlayoffSeedSnapshot(TeamId TeamId, string ConferenceName, string DivisionName, bool IsWildCard, int Rank)
{
    internal static PlayoffSeedSnapshot Create(PlayoffSeed seed) =>
        new(seed.TeamId, seed.Conference.Name, seed.Division.Name, seed.IsWildCard, seed.Rank);
}