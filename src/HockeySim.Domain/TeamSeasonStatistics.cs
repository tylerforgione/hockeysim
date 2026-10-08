namespace HockeySim.Domain;

/// <summary>
/// A team's current-season special teams, faceoffs, and shot totals, accumulated from each
/// completed match. Its wins, losses, and goals are in its <see cref="TeamRecord"/>.
/// </summary>
public sealed record TeamSeasonStatistics(TeamId TeamId)
{
    public int GamesPlayed { get; private init; }

    public int PowerPlayGoals { get; private init; }

    public int PowerPlayOpportunities { get; private init; }

    /// <summary>The opponents' power-play opportunities: the times the team had to kill a penalty.</summary>
    public int TimesShorthanded { get; private init; }

    /// <summary>The opponents' power-play goals.</summary>
    public int PowerPlayGoalsAgainst { get; private init; }

    public int ShorthandedGoals { get; private init; }

    public int FaceoffsWon { get; private init; }

    public int FaceoffsLost { get; private init; }

    /// <summary>Both teams' shot totals by strength situation, from this team's side.</summary>
    public SituationalShotTotals ShotTotals { get; private init; } = SituationalShotTotals.None;

    /// <summary>
    /// Power-play goals per opportunity, or <see langword="null"/> before a first opportunity.
    /// </summary>
    public double? PowerPlayPercentage =>
        PowerPlayOpportunities == 0 ? null : PowerPlayGoals / (double)PowerPlayOpportunities;

    /// <summary>
    /// The share of times shorthanded without conceding a power-play goal, or
    /// <see langword="null"/> before the team has been shorthanded.
    /// </summary>
    public double? PenaltyKillPercentage =>
        TimesShorthanded == 0 ? null : 1 - (PowerPlayGoalsAgainst / (double)TimesShorthanded);

    /// <summary>Faceoffs won as a share of those taken, or <see langword="null"/> before any.</summary>
    public double? FaceoffPercentage =>
        FaceoffsWon + FaceoffsLost == 0 ? null : FaceoffsWon / (double)(FaceoffsWon + FaceoffsLost);

    internal TeamSeasonStatistics Add(CompletedMatch match)
    {
        var (team, opponent) = match.Home.TeamId == TeamId ? (match.Home, match.Away) : (match.Away, match.Home);

        return this with
        {
            GamesPlayed = GamesPlayed + 1,
            PowerPlayGoals = PowerPlayGoals + team.PowerPlayGoals,
            PowerPlayOpportunities = PowerPlayOpportunities + team.PowerPlayOpportunities,
            TimesShorthanded = TimesShorthanded + opponent.PowerPlayOpportunities,
            PowerPlayGoalsAgainst = PowerPlayGoalsAgainst + opponent.PowerPlayGoals,
            ShorthandedGoals = ShorthandedGoals + team.ShorthandedGoals,
            FaceoffsWon = FaceoffsWon + team.FaceoffsWon,
            FaceoffsLost = FaceoffsLost + team.FaceoffsLost,
            ShotTotals = ShotTotals.Add(team.ShotTotals),
        };
    }
}