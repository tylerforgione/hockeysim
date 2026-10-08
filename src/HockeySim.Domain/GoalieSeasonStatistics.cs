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

    public double ExpectedGoalsAgainst { get; private init; }

    /// <summary>Time in net, less any time pulled for an extra attacker.</summary>
    public TimeSpan TimeOnIce { get; private init; }

    /// <summary>
    /// Starts in which the goalie was charged with no goal. Empty-net goals and a shootout are not
    /// charged to the goalie, so neither prevents one.
    /// </summary>
    public int Shutouts { get; private init; }

    /// <summary>
    /// Saves as a share of shots against, or <see langword="null"/> before the goalie has faced a
    /// shot.
    /// </summary>
    public double? SavePercentage => ShotsAgainst == 0 ? null : Saves / (double)ShotsAgainst;

    /// <summary>
    /// Goals against per sixty minutes in net, or <see langword="null"/> before any time in net.
    /// </summary>
    public double? GoalsAgainstAverage => SeasonAverages.PerSixtyMinutes(GoalsAgainst, TimeOnIce);

    /// <summary>
    /// Goals saved above expected (GSAx): expected goals against less goals against, positive for
    /// a goalie who stopped more than a league-average goalie would have.
    /// </summary>
    public double GoalsSavedAboveExpected => ExpectedGoalsAgainst - GoalsAgainst;

    /// <summary>Average time in net, or <see langword="null"/> before a first start.</summary>
    public TimeSpan? TimeOnIcePerGame => SeasonAverages.PerGame(TimeOnIce, GamesPlayed);

    internal GoalieSeasonStatistics Add(GoalieBoxScore boxScore) =>
        this with
        {
            GamesPlayed = GamesPlayed + 1,
            ShotsAgainst = ShotsAgainst + boxScore.ShotsAgainst,
            GoalsAgainst = GoalsAgainst + boxScore.GoalsAgainst,
            ExpectedGoalsAgainst = ExpectedGoalsAgainst + boxScore.ExpectedGoalsAgainst,
            TimeOnIce = TimeOnIce + boxScore.TimeOnIce,
            Shutouts = Shutouts + (boxScore.GoalsAgainst == 0 ? 1 : 0),
        };
}