namespace HockeySim.Domain;

/// <summary>
/// Decides which teams have clinched or been eliminated from the remaining schedule.
/// </summary>
/// <remarks>
/// <para>
/// A status is reported only when no outcome of the remaining games could change it, so the
/// calculation errs towards reporting later. It compares teams pairwise on the per-team ranking
/// criteria (points, then games played, regulation wins, regulation and overtime wins, and wins),
/// using the best and worst finish each team can still reach. A team is certainly ahead of
/// another only when its worst finish outranks the other's best on those criteria. Head-to-head,
/// goal differential, and goals for depend on which clubs end up tied and on future scores, so a
/// team that could draw level on every per-team criterion is treated as possibly ahead.
/// </para>
/// <para>
/// Counting every team that could finish ahead also ignores that those teams take points from
/// each other when they meet; that too only delays a status. Once the season is complete there is
/// nothing left to guarantee, and the statuses follow the final standings with every tie-breaker.
/// </para>
/// </remarks>
internal static class PlayoffRace
{
    public static Dictionary<TeamId, PlayoffStatus> Statuses(Season season) =>
        season.IsComplete ? FinalStatuses(season) : GuaranteedStatuses(season);

    private static Dictionary<TeamId, PlayoffStatus> FinalStatuses(Season season)
    {
        var statuses = season.League.Teams.ToDictionary(team => team.Id, _ => PlayoffStatus.Eliminated);
        void Raise(TeamId teamId, PlayoffStatus status) =>
            statuses[teamId] = status > statuses[teamId] ? status : statuses[teamId];

        foreach (var conference in season.League.Conferences)
        {
            var wildCard = season.RankWildCard(conference);
            foreach (var teamId in wildCard.Qualifiers)
            {
                Raise(teamId, PlayoffStatus.ClinchedPlayoffSpot);
            }

            foreach (var leaders in wildCard.DivisionLeaders)
            {
                Raise(leaders.Teams[0].Record.TeamId, PlayoffStatus.ClinchedDivision);
            }

            Raise(Leader(season, TeamsOf(conference)), PlayoffStatus.ClinchedConference);
        }

        Raise(Leader(season, season.League.Teams), PlayoffStatus.ClinchedBestRecord);
        return statuses;
    }

    private static Dictionary<TeamId, PlayoffStatus> GuaranteedStatuses(Season season)
    {
        var outlooks = Outlooks(season);
        var statuses = new Dictionary<TeamId, PlayoffStatus>();
        foreach (var conference in season.League.Conferences)
        {
            foreach (var division in conference.Divisions)
            {
                foreach (var team in division.Teams)
                {
                    statuses[team.Id] = GuaranteedStatus(team.Id, division, conference, season.League, outlooks);
                }
            }
        }

        return statuses;
    }

    private static PlayoffStatus GuaranteedStatus(
        TeamId teamId,
        Division division,
        Conference conference,
        League league,
        Dictionary<TeamId, Outlook> outlooks)
    {
        var team = outlooks[teamId];
        bool PossiblyAhead(Team other) => other.Id != teamId && !team.CertainlyAhead(outlooks[other.Id]);
        bool CertainlyAhead(Team other) => other.Id != teamId && outlooks[other.Id].CertainlyAhead(team);

        if (!league.Teams.Any(PossiblyAhead))
        {
            return PlayoffStatus.ClinchedBestRecord;
        }

        if (!TeamsOf(conference).Any(PossiblyAhead))
        {
            return PlayoffStatus.ClinchedConference;
        }

        if (!division.Teams.Any(PossiblyAhead))
        {
            return PlayoffStatus.ClinchedDivision;
        }

        var possiblyAhead = conference.Divisions.ToDictionary(group => group, group => group.Teams.Count(PossiblyAhead));
        if (possiblyAhead[division] < WildCardStandings.DivisionQualifiers
            || MostWildCardTeamsAhead(possiblyAhead.Values) < WildCardStandings.WildCardsPerConference)
        {
            return PlayoffStatus.ClinchedPlayoffSpot;
        }

        var certainlyAhead = conference.Divisions.ToDictionary(group => group, group => group.Teams.Count(CertainlyAhead));
        if (certainlyAhead[division] >= WildCardStandings.DivisionQualifiers
            && MostWildCardTeamsAhead(certainlyAhead.Values) >= WildCardStandings.WildCardsPerConference)
        {
            return PlayoffStatus.Eliminated;
        }

        return PlayoffStatus.Undecided;
    }

    /// <summary>
    /// Bounds how many wild-card contenders finish ahead of a team, given how many teams of each
    /// division finish ahead of it.
    /// </summary>
    /// <remarks>
    /// A team that finishes ahead of ours and is not one of its division's leaders has the
    /// division's leaders ranked above it too, so they also finish ahead of ours. Each division
    /// therefore puts at most (teams ahead − leaders) contenders ahead of ours. Counting teams
    /// that could finish ahead gives an upper bound; counting teams certainly ahead gives the
    /// number of contenders certainly ahead, since at most the leaders among them are not
    /// contenders.
    /// </remarks>
    private static int MostWildCardTeamsAhead(IEnumerable<int> teamsAheadByDivision) =>
        teamsAheadByDivision.Sum(count => Math.Max(0, count - WildCardStandings.DivisionQualifiers));

    private static Dictionary<TeamId, Outlook> Outlooks(Season season)
    {
        var scheduledGames = season.League.Teams.ToDictionary(team => team.Id, _ => 0);
        foreach (var match in season.Schedule.Matches)
        {
            scheduledGames[match.HomeTeamId]++;
            scheduledGames[match.AwayTeamId]++;
        }

        return season.TeamRecords.ToDictionary(
            record => record.TeamId,
            record => Outlook.Create(record, scheduledGames[record.TeamId]));
    }

    private static TeamId Leader(Season season, IEnumerable<Team> teams) =>
        season.RankStandings(teams.Select(team => team.Id))[0].Record.TeamId;

    private static IEnumerable<Team> TeamsOf(Conference conference) =>
        conference.Divisions.SelectMany(division => division.Teams);

    /// <summary>
    /// The per-team ranking criteria, compared lexicographically. Games played is negated
    /// because fewer ranks higher; by the end of the season every team has played its schedule.
    /// </summary>
    private readonly record struct RankingKey(
        int Points,
        int NegatedGamesPlayed,
        int RegulationWins,
        int RegulationAndOvertimeWins,
        int Wins) : IComparable<RankingKey>
    {
        public int CompareTo(RankingKey other) =>
            (Points, NegatedGamesPlayed, RegulationWins, RegulationAndOvertimeWins, Wins)
                .CompareTo((other.Points, other.NegatedGamesPlayed, other.RegulationWins, other.RegulationAndOvertimeWins, other.Wins));
    }

    /// <summary>
    /// The extremes of a team's final ranking key over every outcome of its remaining games.
    /// </summary>
    /// <param name="Worst">
    /// Losing every remaining game in regulation, the only way to finish on the current points,
    /// leaves every criterion where it is now.
    /// </param>
    /// <param name="Best">
    /// Finishing on the most points means winning every remaining game, and the regulation and
    /// regulation-and-overtime wins can then be at most the current ones plus those games. Any
    /// outcome's key is at most this one on every criterion, so it is at most this one overall.
    /// </param>
    private readonly record struct Outlook(RankingKey Worst, RankingKey Best)
    {
        public static Outlook Create(TeamRecord record, int scheduledGames)
        {
            var remaining = scheduledGames - record.GamesPlayed;
            return new Outlook(
                new RankingKey(
                    record.Points,
                    -scheduledGames,
                    record.RegulationWins,
                    record.RegulationAndOvertimeWins,
                    record.Wins),
                new RankingKey(
                    record.Points + (TeamRecord.PointsPerWin * remaining),
                    -scheduledGames,
                    record.RegulationWins + remaining,
                    record.RegulationAndOvertimeWins + remaining,
                    record.Wins + remaining));
        }

        /// <summary>
        /// Whether this team finishes ahead of <paramref name="other"/> in every outcome. When
        /// points end level, this team finished on its worst points and the other on its best,
        /// so comparing worst with best keys covers every tie on points too.
        /// </summary>
        public bool CertainlyAhead(Outlook other) => Worst.CompareTo(other.Best) > 0;
    }
}