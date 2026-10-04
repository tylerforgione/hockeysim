using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// The regular season in progress: the current date, the completed-match history, and the
/// current-season totals derived from it. The season advances one league day at a time; a day's
/// results are applied together or not at all, so the history never holds part of a day and a
/// scheduled match can be completed only once.
/// </summary>
public sealed class Season
{
    private readonly List<CompletedMatch> _completedMatches = [];
    private readonly ReadOnlyCollection<CompletedMatch> _completedMatchesView;
    private readonly Dictionary<TeamId, TeamRecord> _teamRecords;
    private readonly Dictionary<PlayerId, SkaterSeasonStatistics> _skaterStatistics = [];
    private readonly Dictionary<PlayerId, GoalieSeasonStatistics> _goalieStatistics = [];

    public Season(League league, SeasonSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(league);
        ArgumentNullException.ThrowIfNull(schedule);

        if (schedule.Matches.Count == 0)
        {
            throw new ArgumentException("A season requires at least one scheduled match.", nameof(schedule));
        }

        var leagueTeamIds = league.Teams.Select(team => team.Id).ToHashSet();
        if (schedule.Matches.Any(match =>
                !leagueTeamIds.Contains(match.HomeTeamId) || !leagueTeamIds.Contains(match.AwayTeamId)))
        {
            throw new ArgumentException("Every scheduled team must belong to the league.", nameof(schedule));
        }

        League = league;
        Schedule = schedule;
        CurrentDate = schedule.Matches[0].Date;
        _completedMatchesView = _completedMatches.AsReadOnly();
        _teamRecords = league.Teams.ToDictionary(team => team.Id, team => new TeamRecord(team.Id));
    }

    public League League { get; }

    public SeasonSchedule Schedule { get; }

    /// <summary>
    /// The next league day to be played. Once the season is complete, the day after the final
    /// scheduled match.
    /// </summary>
    public DateOnly CurrentDate { get; private set; }

    public bool IsComplete => _completedMatches.Count == Schedule.Matches.Count;

    /// <summary>Every completed match, in schedule order.</summary>
    public IReadOnlyList<CompletedMatch> CompletedMatches => _completedMatchesView;

    /// <summary>The matches scheduled on <see cref="CurrentDate"/>, possibly none.</summary>
    public IReadOnlyList<ScheduledMatch> CurrentDateMatches =>
        Schedule.Matches.Where(match => match.Date == CurrentDate).ToList().AsReadOnly();

    /// <summary>Every team's record, in league team order.</summary>
    public IReadOnlyList<TeamRecord> TeamRecords =>
        League.Teams.Select(team => _teamRecords[team.Id]).ToList().AsReadOnly();

    /// <summary>
    /// Ranks a group of league teams, such as a division, a conference, or the whole league, by
    /// the NHL regular-season procedure. Head-to-head compares only games among the teams tied
    /// within this group.
    /// </summary>
    /// <param name="teamIds">League teams; their order and any repetition do not affect the ranking.</param>
    /// <returns>
    /// The teams best first. Teams level on every criterion share a rank and keep league team
    /// order.
    /// </returns>
    public IReadOnlyList<StandingsEntry> RankStandings(IEnumerable<TeamId> teamIds)
    {
        ArgumentNullException.ThrowIfNull(teamIds);

        var requested = teamIds.ToHashSet();
        if (!requested.All(_teamRecords.ContainsKey))
        {
            throw new ArgumentException("Every ranked team must belong to the league.", nameof(teamIds));
        }

        var records = League.Teams
            .Where(team => requested.Contains(team.Id))
            .Select(team => _teamRecords[team.Id])
            .ToList();
        return StandingsRanking.Rank(records, _completedMatchesView);
    }

    /// <summary>Totals for every skater who has appeared, in league team and roster order.</summary>
    public IReadOnlyList<SkaterSeasonStatistics> SkaterStatistics => InRosterOrder(_skaterStatistics);

    /// <summary>Totals for every goalie who has started, in league team and roster order.</summary>
    public IReadOnlyList<GoalieSeasonStatistics> GoalieStatistics => InRosterOrder(_goalieStatistics);

    /// <summary>
    /// Records the results of every match scheduled on <see cref="CurrentDate"/> and moves to the
    /// next calendar day. The whole day is validated before anything changes.
    /// </summary>
    /// <param name="results">Exactly one result for each match scheduled today; none on an empty day.</param>
    public void CompleteDay(IEnumerable<CompletedMatch> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (IsComplete)
        {
            throw new InvalidOperationException("The regular season is complete; no further days can be played.");
        }

        var resultList = results.ToList();
        ValidateDay(resultList);

        // Keep the history in schedule order regardless of the order results were supplied in.
        foreach (var scheduledMatch in CurrentDateMatches)
        {
            Apply(resultList.Single(result => ReferenceEquals(result.ScheduledMatch, scheduledMatch)));
        }

        CurrentDate = CurrentDate.AddDays(1);
    }

    private void ValidateDay(List<CompletedMatch> results)
    {
        if (results.Any(result => result is null))
        {
            throw new ArgumentException("A day's results cannot contain a missing result.", nameof(results));
        }

        // Results must reference the schedule's own entries, so a result can never be applied to a
        // match on another date or to one that has already been completed.
        var todaysMatches = CurrentDateMatches.ToHashSet(ReferenceEqualityComparer.Instance);
        var resultMatches = results.Select(result => result.ScheduledMatch).ToList();
        if (resultMatches.Count != todaysMatches.Count
            || resultMatches.Distinct(ReferenceEqualityComparer.Instance).Count() != resultMatches.Count
            || !resultMatches.All(todaysMatches.Contains))
        {
            throw new ArgumentException(
                $"Exactly one result is required for each match scheduled on {CurrentDate:yyyy-MM-dd}.",
                nameof(results));
        }

        foreach (var result in results)
        {
            ValidateAppearances(result.Home);
            ValidateAppearances(result.Away);
        }
    }

    private void ValidateAppearances(CompletedMatchTeam side)
    {
        var roster = League.Teams.Single(team => team.Id == side.TeamId).Roster.ToDictionary(player => player.Id);

        if (side.Skaters.Any(skater =>
                !roster.TryGetValue(skater.PlayerId, out var player) || player.Position == Position.Goalie)
            || !roster.TryGetValue(side.Goalie.PlayerId, out var goalie)
            || goalie.Position != Position.Goalie)
        {
            throw new ArgumentException(
                "Every appearing player must be a rostered skater or goalie of their team.");
        }
    }

    private void Apply(CompletedMatch match)
    {
        _completedMatches.Add(match);

        foreach (var side in new[] { match.Home, match.Away })
        {
            _teamRecords[side.TeamId] = _teamRecords[side.TeamId].Add(match);

            foreach (var skater in side.Skaters)
            {
                var totals = _skaterStatistics.GetValueOrDefault(skater.PlayerId)
                    ?? new SkaterSeasonStatistics(skater.PlayerId, side.TeamId);
                _skaterStatistics[skater.PlayerId] = totals.Add(skater);
            }

            var goalieTotals = _goalieStatistics.GetValueOrDefault(side.Goalie.PlayerId)
                ?? new GoalieSeasonStatistics(side.Goalie.PlayerId, side.TeamId);
            _goalieStatistics[side.Goalie.PlayerId] = goalieTotals.Add(side.Goalie);
        }
    }

    private ReadOnlyCollection<T> InRosterOrder<T>(Dictionary<PlayerId, T> statistics) =>
        League.Teams
            .SelectMany(team => team.Roster)
            .Where(player => statistics.ContainsKey(player.Id))
            .Select(player => statistics[player.Id])
            .ToList()
            .AsReadOnly();
}