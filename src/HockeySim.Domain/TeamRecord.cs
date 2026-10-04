namespace HockeySim.Domain;

/// <summary>
/// A team's current-season results. Wins and losses are kept by how the match was decided so
/// standings rules can distinguish them; goals include a shootout deciding goal.
/// </summary>
public sealed record TeamRecord(TeamId TeamId)
{
    public const int PointsPerWin = 2;
    public const int PointsPerOvertimeOrShootoutLoss = 1;

    public int RegulationWins { get; private init; }

    public int OvertimeWins { get; private init; }

    public int ShootoutWins { get; private init; }

    public int RegulationLosses { get; private init; }

    public int OvertimeLosses { get; private init; }

    public int ShootoutLosses { get; private init; }

    /// <summary>Goals scored, including one for each shootout won.</summary>
    public int GoalsFor { get; private init; }

    /// <summary>Goals conceded, including one for each shootout lost.</summary>
    public int GoalsAgainst { get; private init; }

    public int GamesPlayed => Wins + RegulationLosses + OvertimeLosses + ShootoutLosses;

    public int Wins => RegulationWins + OvertimeWins + ShootoutWins;

    /// <summary>Wins excluding shootout wins (the standings "ROW" column).</summary>
    public int RegulationAndOvertimeWins => RegulationWins + OvertimeWins;

    public int GoalDifferential => GoalsFor - GoalsAgainst;

    /// <summary>
    /// Two points for any win, one for a loss in overtime or a shootout, none for a regulation loss.
    /// </summary>
    public int Points =>
        (PointsPerWin * Wins) + (PointsPerOvertimeOrShootoutLoss * (OvertimeLosses + ShootoutLosses));

    internal TeamRecord Add(CompletedMatch match)
    {
        var team = match.Home.TeamId == TeamId ? match.Home : match.Away;
        var opponent = match.Home.TeamId == TeamId ? match.Away : match.Home;
        var won = team.Score > opponent.Score;

        return this with
        {
            RegulationWins = RegulationWins + Count(won && match.Decision == MatchDecision.Regulation),
            OvertimeWins = OvertimeWins + Count(won && match.Decision == MatchDecision.Overtime),
            ShootoutWins = ShootoutWins + Count(won && match.Decision == MatchDecision.Shootout),
            RegulationLosses = RegulationLosses + Count(!won && match.Decision == MatchDecision.Regulation),
            OvertimeLosses = OvertimeLosses + Count(!won && match.Decision == MatchDecision.Overtime),
            ShootoutLosses = ShootoutLosses + Count(!won && match.Decision == MatchDecision.Shootout),
            GoalsFor = GoalsFor + team.Score,
            GoalsAgainst = GoalsAgainst + opponent.Score,
        };
    }

    private static int Count(bool condition) => condition ? 1 : 0;
}