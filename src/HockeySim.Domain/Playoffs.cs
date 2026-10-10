using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// The NHL playoffs that follow the regular season: sixteen qualifiers in a fixed bracket of
/// best-of-seven series, ending when the final crowns a champion. Records and statistics are
/// kept apart from the regular season's.
/// </summary>
/// <remarks>
/// <para>
/// Within each division's bracket, the division winner meets a wild card (the conference's
/// better division winner meets the lower wild card) and second meets third; the winners meet in
/// the second round, then the conference final and the final. Home ice goes to the team that
/// placed higher in its bracket through the first two rounds, whatever the teams' points, and to
/// the better regular-season record from the conference final on.
/// </para>
/// <para>
/// The rounds run in lockstep: every undecided series in a round plays on the same days, one game
/// every <see cref="DaysBetweenGames"/> days, and each next game is scheduled only once the
/// previous one has been played. The next round starts <see cref="DaysBetweenGames"/> days after
/// the last game of the round. Nothing here is random, so the bracket and its dates follow from
/// the regular season and the playoff results alone.
/// </para>
/// </remarks>
public sealed class Playoffs
{
    /// <summary>The days from one game to the next, and from the end of a round to the next round.</summary>
    public const int DaysBetweenGames = 2;

    private readonly Func<TeamId, TeamId, bool> _hasBetterRecord;
    private readonly List<PlayoffSeries> _series = [];
    private readonly ReadOnlyCollection<PlayoffSeries> _seriesView;
    private readonly List<ScheduledMatch> _schedule = [];
    private readonly ReadOnlyCollection<ScheduledMatch> _scheduleView;
    private readonly List<CompletedMatch> _completedMatches = [];
    private readonly ReadOnlyCollection<CompletedMatch> _completedMatchesView;
    private readonly SeasonTotals _totals;

    /// <param name="firstRound">The first-round series in bracket order.</param>
    /// <param name="hasBetterRecord">Whether the first team finished the regular season ranked above the second.</param>
    private Playoffs(
        League league,
        IReadOnlyList<PlayoffSeries> firstRound,
        Func<TeamId, TeamId, bool> hasBetterRecord,
        DateOnly startDate)
    {
        _hasBetterRecord = hasBetterRecord;
        _seriesView = _series.AsReadOnly();
        _scheduleView = _schedule.AsReadOnly();
        _completedMatchesView = _completedMatches.AsReadOnly();

        var qualifiers = firstRound
            .SelectMany(series => new[] { series.HigherRanked.TeamId, series.LowerRanked.TeamId })
            .ToHashSet();
        _totals = new SeasonTotals(league.Teams.Where(team => qualifiers.Contains(team.Id)));

        StartRound(firstRound, startDate);
    }

    /// <summary>
    /// Every series formed so far, round by round, each round in bracket order: by conference,
    /// then by division bracket. A round's series are formed once the previous round is decided.
    /// </summary>
    public IReadOnlyList<PlayoffSeries> Series => _seriesView;

    /// <summary>The latest round to have started.</summary>
    public PlayoffRound CurrentRound => _series[^1].Round;

    /// <summary>Every playoff match scheduled so far, played or not, in date order.</summary>
    public IReadOnlyList<ScheduledMatch> Schedule => _scheduleView;

    /// <summary>Every completed playoff match, in schedule order.</summary>
    public IReadOnlyList<CompletedMatch> CompletedMatches => _completedMatchesView;

    /// <summary>The winner of the final, once it is decided.</summary>
    public PlayoffSeed? Champion => CurrentRound == PlayoffRound.Final ? _series[^1].Winner : null;

    public bool IsComplete => Champion is not null;

    /// <summary>Every qualifier's playoff record, in league team order.</summary>
    public IReadOnlyList<TeamRecord> TeamRecords => _totals.TeamRecords;

    /// <summary>Every qualifier's playoff special teams, faceoff, and shot totals, in league team order.</summary>
    public IReadOnlyList<TeamSeasonStatistics> TeamStatistics => _totals.TeamStatistics;

    /// <summary>Playoff totals for every skater who has appeared, in league team and roster order.</summary>
    public IReadOnlyList<SkaterSeasonStatistics> SkaterStatistics => _totals.SkaterStatistics;

    /// <summary>Playoff totals for every goalie who has started, in league team and roster order.</summary>
    public IReadOnlyList<GoalieSeasonStatistics> GoalieStatistics => _totals.GoalieStatistics;

    /// <summary>The series in <paramref name="round"/>, in bracket order; none before it starts.</summary>
    public IReadOnlyList<PlayoffSeries> SeriesIn(PlayoffRound round) =>
        _series.Where(series => series.Round == round).ToList().AsReadOnly();

    /// <summary>
    /// Seeds the qualifiers from the final regular-season standings and schedules the first game
    /// of every first-round series on <paramref name="startDate"/>.
    /// </summary>
    internal static Playoffs Start(Season season, DateOnly startDate)
    {
        bool HasBetterRecord(TeamId first, TeamId second) =>
            season.RankStandings([first, second])[0].Record.TeamId == first;

        var firstRound = new List<PlayoffSeries>();
        foreach (var conference in season.League.Conferences)
        {
            var standings = season.RankWildCard(conference);
            var wildCards = standings.WildCardRace
                .Take(WildCardStandings.WildCardsPerConference)
                .Select((entry, index) => Seed(entry, conference, isWildCard: true, rank: index + 1))
                .ToList();
            var winners = standings.DivisionLeaders.Select(leaders => leaders.Teams[0].Record.TeamId).ToList();
            var bestWinner = HasBetterRecord(winners[0], winners[1]) ? winners[0] : winners[1];

            foreach (var leaders in standings.DivisionLeaders)
            {
                var (first, second, third) = (
                    Seed(leaders.Teams[0], conference, isWildCard: false, rank: 1),
                    Seed(leaders.Teams[1], conference, isWildCard: false, rank: 2),
                    Seed(leaders.Teams[2], conference, isWildCard: false, rank: 3));

                // The better division winner meets the lower wild card.
                var wildCard = first.TeamId == bestWinner ? wildCards[1] : wildCards[0];
                firstRound.Add(new PlayoffSeries(PlayoffRound.FirstRound, first, wildCard));
                firstRound.Add(new PlayoffSeries(PlayoffRound.FirstRound, second, third));
            }
        }

        return new Playoffs(season.League, firstRound, HasBetterRecord, startDate);

        PlayoffSeed Seed(StandingsEntry entry, Conference conference, bool isWildCard, int rank) =>
            new(
                entry.Record.TeamId,
                conference,
                conference.Divisions.Single(division => division.Teams.Any(team => team.Id == entry.Record.TeamId)),
                isWildCard,
                rank);
    }

    /// <summary>The playoff matches scheduled on <paramref name="date"/>, in bracket order.</summary>
    internal IReadOnlyList<ScheduledMatch> MatchesOn(DateOnly date) =>
        _schedule.Where(match => match.Date == date).ToList().AsReadOnly();

    /// <summary>Records a played game in its series and in the playoff totals.</summary>
    internal void Apply(CompletedMatch game)
    {
        var series = _series.Single(series => ReferenceEquals(series.NextGame, game.ScheduledMatch));
        series.Record(game);
        _completedMatches.Add(game);
        _totals.Add(game);
    }

    /// <summary>
    /// After a day of playoff games on <paramref name="playedDate"/>, schedules each undecided
    /// series' next game, or, once the whole round is decided, starts the next round.
    /// </summary>
    internal void ScheduleAfter(DateOnly playedDate)
    {
        var nextDate = playedDate.AddDays(DaysBetweenGames);
        var round = _series.Where(series => series.Round == CurrentRound).ToList();
        if (round.Any(series => !series.IsDecided))
        {
            foreach (var series in round.Where(series => !series.IsDecided))
            {
                _schedule.Add(series.ScheduleNextGame(nextDate));
            }

            return;
        }

        if (CurrentRound == PlayoffRound.Final)
        {
            return;
        }

        // Neighbouring series in bracket order feed the same series in the next round.
        var nextRound = CurrentRound + 1;
        var winners = round.Select(series => series.Winner!).ToList();
        StartRound(
            Enumerable.Range(0, winners.Count / 2)
                .Select(index => Pair(nextRound, winners[2 * index], winners[(2 * index) + 1]))
                .ToList(),
            nextDate);
    }

    private PlayoffSeries Pair(PlayoffRound round, PlayoffSeed first, PlayoffSeed second)
    {
        var firstRanksHigher = round <= PlayoffRound.SecondRound
            ? first.PlacedAbove(second)
            : _hasBetterRecord(first.TeamId, second.TeamId);
        return firstRanksHigher
            ? new PlayoffSeries(round, first, second)
            : new PlayoffSeries(round, second, first);
    }

    private void StartRound(IReadOnlyList<PlayoffSeries> round, DateOnly startDate)
    {
        foreach (var series in round)
        {
            _series.Add(series);
            _schedule.Add(series.ScheduleNextGame(startDate));
        }
    }
}