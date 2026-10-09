using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// A season in progress: its preseason and regular season, the current date, the completed-match
/// history, and the current-season team and player totals and player health derived from the
/// regular season. The season advances one league day at a time; a day's results are applied
/// together or not at all, so the history never holds part of a day and a scheduled match can be
/// completed only once.
/// </summary>
/// <remarks>
/// Preseason matches are kept apart from the regular season's: they never reach the records,
/// statistics, standings, or health, so everything derived from the regular season reads only
/// its own schedule and history.
/// </remarks>
public sealed class Season
{
    private readonly List<CompletedMatch> _preseasonMatches = [];
    private readonly ReadOnlyCollection<CompletedMatch> _preseasonMatchesView;
    private readonly List<CompletedMatch> _completedMatches = [];
    private readonly ReadOnlyCollection<CompletedMatch> _completedMatchesView;
    private readonly Dictionary<TeamId, TeamRecord> _teamRecords;
    private readonly Dictionary<TeamId, TeamSeasonStatistics> _teamStatistics;
    private readonly Dictionary<PlayerId, SkaterSeasonStatistics> _skaterStatistics = [];
    private readonly Dictionary<PlayerId, GoalieSeasonStatistics> _goalieStatistics = [];
    private readonly Dictionary<PlayerId, PlayerHealth> _health;

    /// <summary>Creates a season without a preseason, starting on opening day.</summary>
    public Season(League league, SeasonSchedule schedule)
        : this(league, new SeasonSchedule([]), schedule)
    {
    }

    /// <summary>Creates a season starting on the first preseason day, or on opening day without one.</summary>
    /// <param name="preseasonSchedule">The exhibition matches, all before opening day; possibly none.</param>
    /// <param name="schedule">The regular season.</param>
    public Season(League league, SeasonSchedule preseasonSchedule, SeasonSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(league);
        ArgumentNullException.ThrowIfNull(preseasonSchedule);
        ArgumentNullException.ThrowIfNull(schedule);

        if (schedule.Matches.Count == 0)
        {
            throw new ArgumentException("A season requires at least one scheduled match.", nameof(schedule));
        }

        var leagueTeamIds = league.Teams.Select(team => team.Id).ToHashSet();
        if (schedule.Matches.Concat(preseasonSchedule.Matches).Any(match =>
                !leagueTeamIds.Contains(match.HomeTeamId) || !leagueTeamIds.Contains(match.AwayTeamId)))
        {
            throw new ArgumentException("Every scheduled team must belong to the league.", nameof(schedule));
        }

        var openingDay = schedule.Matches[0].Date;
        if (preseasonSchedule.Matches.Any(match => match.Date >= openingDay))
        {
            throw new ArgumentException("Every preseason match must be before opening day.", nameof(preseasonSchedule));
        }

        if (league.Teams.SelectMany(team => team.Roster).Any(player =>
                player.Biography.BirthDate > openingDay
                || player.AgeOn(openingDay) is < Player.MinimumAge or > Player.MaximumAge))
        {
            throw new ArgumentException(
                $"Every player must be between {Player.MinimumAge} and {Player.MaximumAge} years old on opening day.",
                nameof(league));
        }

        League = league;
        PreseasonSchedule = preseasonSchedule;
        Schedule = schedule;
        OpeningDay = openingDay;
        CurrentDate = preseasonSchedule.Matches.Count > 0 ? preseasonSchedule.Matches[0].Date : openingDay;
        _preseasonMatchesView = _preseasonMatches.AsReadOnly();
        _completedMatchesView = _completedMatches.AsReadOnly();
        _teamRecords = league.Teams.ToDictionary(team => team.Id, team => new TeamRecord(team.Id));
        _teamStatistics = league.Teams.ToDictionary(team => team.Id, team => new TeamSeasonStatistics(team.Id));
        _health = league.Teams
            .SelectMany(team => team.Roster)
            .ToDictionary(player => player.Id, player => PlayerHealth.Healthy(player.Id));
    }

    public League League { get; }

    /// <summary>The exhibition matches before opening day, possibly none.</summary>
    public SeasonSchedule PreseasonSchedule { get; }

    /// <summary>The regular season.</summary>
    public SeasonSchedule Schedule { get; }

    /// <summary>The date of the first regular-season match.</summary>
    public DateOnly OpeningDay { get; }

    /// <summary>
    /// The next league day to be played. Once the season is complete, the day after the final
    /// scheduled match.
    /// </summary>
    public DateOnly CurrentDate { get; private set; }

    /// <summary>The phase of <see cref="CurrentDate"/>: the preseason until opening day.</summary>
    public SeasonPhase Phase => CurrentDate < OpeningDay ? SeasonPhase.Preseason : SeasonPhase.RegularSeason;

    /// <summary>Whether every regular-season match has been played.</summary>
    public bool IsComplete => _completedMatches.Count == Schedule.Matches.Count;

    /// <summary>Every completed preseason match, in schedule order.</summary>
    public IReadOnlyList<CompletedMatch> PreseasonMatches => _preseasonMatchesView;

    /// <summary>Every completed regular-season match, in schedule order.</summary>
    public IReadOnlyList<CompletedMatch> CompletedMatches => _completedMatchesView;

    /// <summary>The matches scheduled on <see cref="CurrentDate"/> in its phase, possibly none.</summary>
    public IReadOnlyList<ScheduledMatch> CurrentDateMatches =>
        (Phase == SeasonPhase.Preseason ? PreseasonSchedule : Schedule).Matches
            .Where(match => match.Date == CurrentDate)
            .ToList()
            .AsReadOnly();

    /// <summary>Every team's record, in league team order.</summary>
    public IReadOnlyList<TeamRecord> TeamRecords =>
        League.Teams.Select(team => _teamRecords[team.Id]).ToList().AsReadOnly();

    /// <summary>Every team's special teams, faceoff, and shot totals, in league team order.</summary>
    public IReadOnlyList<TeamSeasonStatistics> TeamStatistics =>
        League.Teams.Select(team => _teamStatistics[team.Id]).ToList().AsReadOnly();

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

    /// <summary>
    /// Ranks a conference in the wild-card format: each division's leaders, then the rest of
    /// the conference for the wild cards. As the season stands, these are its playoff qualifiers.
    /// </summary>
    public WildCardStandings RankWildCard(Conference conference)
    {
        ArgumentNullException.ThrowIfNull(conference);

        if (!League.Conferences.Contains(conference))
        {
            throw new ArgumentException("The conference must belong to the league.", nameof(conference));
        }

        var divisionLeaders = conference.Divisions
            .Select(division => new DivisionLeaders(
                division,
                RankStandings(division.Teams.Select(team => team.Id))
                    .Take(WildCardStandings.DivisionQualifiers)
                    .ToList()
                    .AsReadOnly()))
            .ToList();
        var leaderIds = divisionLeaders
            .SelectMany(leaders => leaders.Teams)
            .Select(entry => entry.Record.TeamId)
            .ToHashSet();
        var wildCardRace = RankStandings(conference.Divisions
            .SelectMany(division => division.Teams)
            .Select(team => team.Id)
            .Where(teamId => !leaderIds.Contains(teamId)));

        return new WildCardStandings(conference, divisionLeaders, wildCardRace);
    }

    /// <summary>
    /// Every team's guaranteed playoff status. During the season a status
    /// is reported only once no remaining result can change it; once the season is complete, the
    /// final standings decide every team.
    /// </summary>
    public IReadOnlyDictionary<TeamId, PlayoffStatus> PlayoffStatuses() =>
        PlayoffRace.Statuses(this).AsReadOnly();

    /// <summary>Totals for every skater who has appeared, in league team and roster order.</summary>
    public IReadOnlyList<SkaterSeasonStatistics> SkaterStatistics => InRosterOrder(_skaterStatistics);

    /// <summary>Totals for every goalie who has started, in league team and roster order.</summary>
    public IReadOnlyList<GoalieSeasonStatistics> GoalieStatistics => InRosterOrder(_goalieStatistics);

    /// <summary>
    /// A rostered player's injuries and hidden wear, from every completed match so far. Injuries
    /// heal by date, so ask the health about <see cref="CurrentDate"/>.
    /// </summary>
    public PlayerHealth HealthOf(PlayerId playerId) =>
        _health.TryGetValue(playerId, out var health)
            ? health
            : throw new ArgumentException("The player is not rostered in the league.", nameof(playerId));

    /// <summary>
    /// Records the results of every match scheduled on <see cref="CurrentDate"/> and moves to the
    /// next calendar day. The whole day is validated before anything changes. A preseason day's
    /// results are only kept: they cannot injure or wear anyone, and count toward nothing.
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
        var isPreseason = Phase == SeasonPhase.Preseason;
        foreach (var scheduledMatch in CurrentDateMatches)
        {
            var result = resultList.Single(result => ReferenceEquals(result.ScheduledMatch, scheduledMatch));
            if (isPreseason)
            {
                _preseasonMatches.Add(result);
            }
            else
            {
                Apply(result);
            }
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

        if (Phase == SeasonPhase.Preseason
            && results.Any(result => result.Health.Injuries.Count > 0 || result.Health.Wear.Count > 0))
        {
            throw new ArgumentException("A preseason match cannot injure or wear any player.", nameof(results));
        }

        foreach (var result in results)
        {
            ValidateAppearances(result.Home);
            ValidateAppearances(result.Away);
            ValidateInjuryCap(result, result.Home.TeamId);
            ValidateInjuryCap(result, result.Away.TeamId);
        }
    }

    /// <summary>
    /// The match's injuries that cannot be played through must leave the team at least
    /// <see cref="InjuryCap.MinimumAbleSkaters"/> skaters and <see cref="InjuryCap.MinimumAbleGoalies"/>
    /// goalies able to play.
    /// </summary>
    private void ValidateInjuryCap(CompletedMatch result, TeamId teamId)
    {
        var roster = League.Teams.Single(team => team.Id == teamId).Roster;
        var newlyUnable = result.Health.Injuries
            .Where(injury => injury.TeamId == teamId && !injury.Definition.CanPlayThrough)
            .Select(injury => injury.PlayerId)
            .ToHashSet();

        foreach (var goalies in new[] { true, false })
        {
            var able = roster
                .Where(player => (player.Position == Position.Goalie) == goalies && _health[player.Id].CanPlayOn(CurrentDate))
                .ToList();
            var lost = able.Count(player => newlyUnable.Contains(player.Id));
            var minimum = goalies ? InjuryCap.MinimumAbleGoalies : InjuryCap.MinimumAbleSkaters;
            if (lost > 0 && able.Count - lost < minimum)
            {
                throw new ArgumentException(
                    $"Injuries cannot leave a team with fewer than {InjuryCap.MinimumAbleSkaters} skaters and {InjuryCap.MinimumAbleGoalies} goalies able to play.");
            }
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

        if (side.Skaters.Select(skater => skater.PlayerId).Append(side.Goalie.PlayerId)
            .Any(playerId => !_health[playerId].CanPlayOn(CurrentDate)))
        {
            throw new ArgumentException("A player with an injury they cannot play through cannot appear.");
        }
    }

    private void Apply(CompletedMatch match)
    {
        _completedMatches.Add(match);

        foreach (var side in new[] { match.Home, match.Away })
        {
            _teamRecords[side.TeamId] = _teamRecords[side.TeamId].Add(match);
            _teamStatistics[side.TeamId] = _teamStatistics[side.TeamId].Add(match);

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

        foreach (var injury in match.Health.Injuries)
        {
            _health[injury.PlayerId] = _health[injury.PlayerId].Add(new Injury(injury.Type, match.Date, injury.RecoveryDays));
        }

        foreach (var gain in match.Health.Wear)
        {
            _health[gain.PlayerId] = _health[gain.PlayerId].AddWear(gain.BodyPart, gain.Points);
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