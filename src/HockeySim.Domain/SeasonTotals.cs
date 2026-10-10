using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// Team records and team and player statistics accumulated from one part of the season's
/// completed matches, such as the regular season or the playoffs, which are kept apart.
/// </summary>
internal sealed class SeasonTotals
{
    private readonly ReadOnlyCollection<Team> _teams;
    private readonly Dictionary<TeamId, TeamRecord> _teamRecords;
    private readonly Dictionary<TeamId, TeamSeasonStatistics> _teamStatistics;
    private readonly Dictionary<PlayerId, SkaterSeasonStatistics> _skaterStatistics = [];
    private readonly Dictionary<PlayerId, GoalieSeasonStatistics> _goalieStatistics = [];

    /// <param name="teams">The teams that take part, in league team order.</param>
    public SeasonTotals(IEnumerable<Team> teams)
    {
        _teams = teams.ToList().AsReadOnly();
        _teamRecords = _teams.ToDictionary(team => team.Id, team => new TeamRecord(team.Id));
        _teamStatistics = _teams.ToDictionary(team => team.Id, team => new TeamSeasonStatistics(team.Id));
    }

    /// <summary>Every taking-part team's record, in league team order.</summary>
    public IReadOnlyList<TeamRecord> TeamRecords =>
        _teams.Select(team => _teamRecords[team.Id]).ToList().AsReadOnly();

    /// <summary>Every taking-part team's special teams, faceoff, and shot totals, in league team order.</summary>
    public IReadOnlyList<TeamSeasonStatistics> TeamStatistics =>
        _teams.Select(team => _teamStatistics[team.Id]).ToList().AsReadOnly();

    /// <summary>Totals for every skater who has appeared, in league team and roster order.</summary>
    public IReadOnlyList<SkaterSeasonStatistics> SkaterStatistics => InRosterOrder(_skaterStatistics);

    /// <summary>Totals for every goalie who has started, in league team and roster order.</summary>
    public IReadOnlyList<GoalieSeasonStatistics> GoalieStatistics => InRosterOrder(_goalieStatistics);

    public bool Includes(TeamId teamId) => _teamRecords.ContainsKey(teamId);

    public TeamRecord RecordOf(TeamId teamId) => _teamRecords[teamId];

    public void Add(CompletedMatch match)
    {
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
    }

    private ReadOnlyCollection<T> InRosterOrder<T>(Dictionary<PlayerId, T> statistics) =>
        _teams
            .SelectMany(team => team.Roster)
            .Where(player => statistics.ContainsKey(player.Id))
            .Select(player => statistics[player.Id])
            .ToList()
            .AsReadOnly();
}