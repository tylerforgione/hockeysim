namespace HockeySim.Domain;

/// <summary>
/// A goalie's current-season totals, accumulated from each start's box score. Shootouts are
/// excluded, as they are from every individual statistic.
/// </summary>
public sealed record GoalieSeasonStatistics(PlayerId PlayerId, TeamId TeamId)
{
    public int GamesPlayed { get; private init; }

    public int ShotsAgainst { get; private init; }

    public int GoalsAgainst { get; private init; }

    public int Saves => ShotsAgainst - GoalsAgainst;

    /// <summary>
    /// Saves as a share of shots against, or <see langword="null"/> before the goalie has faced a
    /// shot.
    /// </summary>
    public double? SavePercentage => ShotsAgainst == 0 ? null : Saves / (double)ShotsAgainst;

    internal GoalieSeasonStatistics Add(GoalieBoxScore boxScore) =>
        this with
        {
            GamesPlayed = GamesPlayed + 1,
            ShotsAgainst = ShotsAgainst + boxScore.ShotsAgainst,
            GoalsAgainst = GoalsAgainst + boxScore.GoalsAgainst,
        };
}