namespace HockeySim.Management.Tests;

/// <summary>
/// A recent NHL league average the engine is calibrated to, and how far a generated season may
/// stray from it before the tuning counts as broken.
/// </summary>
internal sealed record CalibrationTarget(string Name, double Nhl, double Tolerance, Func<LeagueMeasurements, double> Measure)
{
    public double Lowest => Nhl - Tolerance;

    public double Highest => Nhl + Tolerance;

    public override string ToString() => Name;
}

/// <summary>
/// The calibration targets: averages of the 2023-24, 2024-25, and 2025-26 NHL regular seasons, per
/// team per game unless the name says per match. docs/areas/match-engine.md lists each source and
/// the values a generated season measures.
/// </summary>
/// <remarks>
/// Tolerances are wide on purpose. They catch broken tuning, not small rebalancing, and leave room
/// for the season-to-season noise of one generated league.
/// </remarks>
internal static class NhlTargets
{
    // A generated season is 84 games; NHL standings points are scaled up from 82.
    private const double SeasonLengthScale = 84.0 / 82.0;

    public static IReadOnlyList<CalibrationTarget> All { get; } =
    [
        // Hockey-Reference league averages; goals include shootout winners, as the score does.
        new("Goals", 3.06, 0.35, measured => measured.Goals),
        new("Shots on goal", 28.8, 3.0, measured => measured.Shots),

        // NHL.com team shot attempts: shots, missed shots, and attempts blocked.
        new("Shot attempts", 59.5, 6.0, measured => measured.ShotAttempts),

        // MoneyPuck, all situations.
        new("Expected goals", 3.12, 0.45, measured => measured.ExpectedGoals),
        new("Save percentage", 0.900, 0.012, measured => measured.SavePercentage),

        // Hockey-Reference standings and game results.
        new("Regulation share of matches", 0.780, 0.05, measured => measured.RegulationShare),
        new("Overtime share of matches", 0.150, 0.06, measured => measured.OvertimeShare),
        new("Shootout share of matches", 0.071, 0.035, measured => measured.ShootoutShare),

        new("Power-play opportunities", 2.87, 0.5, measured => measured.PowerPlayOpportunities),
        new("Power-play percentage", 0.212, 0.04, measured => measured.PowerPlayPercentage),

        // NHL.com team penalties; fights estimated from majors, most of which are for fighting.
        new("Penalty minutes", 8.84, 1.5, measured => measured.PenaltyMinutes),
        new("Fights per match", 0.20, 0.1, measured => measured.FightsPerMatch),

        // NHL.com team real-time statistics. Takeaways and giveaways are from 2024-25 and 2025-26
        // only, since the league's tracking of them changed after 2023-24.
        new("Hits", 21.5, 4.0, measured => measured.Hits),
        new("Blocked shots", 15.1, 3.0, measured => measured.BlockedShots),
        new("Takeaways", 4.7, 1.5, measured => measured.Takeaways),
        new("Giveaways", 14.8, 3.5, measured => measured.Giveaways),

        // NHL.com team faceoffs: every faceoff involves both teams, and three in ten are taken
        // in the neutral zone.
        new("Faceoffs per match", 56.4, 6.0, measured => measured.FaceoffsPerMatch),
        new("Centre-ice share of faceoffs", 0.30, 0.05, measured => measured.CentreIceFaceoffShare),

        new("Empty-net goals per match", 0.375, 0.12, measured => measured.EmptyNetGoalsPerMatch),

        // NHL.com skater time on ice per game, ranked within each team: forwards in threes and
        // defence in twos as lines and pairs.
        new("First forward line's share of forward ice time", 0.311, 0.04, measured => measured.ForwardLineTimeShares[0]),
        new("Second forward line's share of forward ice time", 0.269, 0.04, measured => measured.ForwardLineTimeShares[1]),
        new("Third forward line's share of forward ice time", 0.232, 0.04, measured => measured.ForwardLineTimeShares[2]),
        new("Fourth forward line's share of forward ice time", 0.188, 0.04, measured => measured.ForwardLineTimeShares[3]),
        new("First defence pair's share of defence ice time", 0.390, 0.04, measured => measured.DefencePairTimeShares[0]),
        new("Second defence pair's share of defence ice time", 0.335, 0.04, measured => measured.DefencePairTimeShares[1]),
        new("Third defence pair's share of defence ice time", 0.275, 0.04, measured => measured.DefencePairTimeShares[2]),

        // Injuries in matches, estimated from public man-games-lost tallies (about 140 a team a
        // season) less the share not suffered in games. Recovery is in league days; a team plays
        // about every other day, so about 11 days is five or six matches.
        new("Injuries missing matches", 0.30, 0.12, measured => measured.InjuriesMissingMatches),
        new("Recovery days of injuries missing matches", 11.0, 4.0, measured => measured.MeanRecoveryDays),
        new("Players out injured (man-games lost)", 1.67, 0.6, measured => measured.ManGamesLost),

        // Public NHL data does not count knocks played through. The target is about one every five
        // matches (17 a season), about as many as injuries that miss matches. Lasting about a week,
        // each is carried for two or three more matches, so a team dresses someone playing hurt in
        // about three matches in ten.
        new("Injuries played through", 0.20, 0.08, measured => measured.PlayThroughInjuries),
        new("Share of matches playing hurt", 0.30, 0.12, measured => measured.PlayingHurtShare),

        // Hockey-Reference final standings: the spread between strong and weak teams.
        new("Standard deviation of standings points", 15.0 * SeasonLengthScale, 6.0, measured => measured.PointsStandardDeviation),
        new("Fewest standings points", 52.3 * SeasonLengthScale, 18.0, measured => measured.FewestPoints),
        new("Most standings points", 117.0 * SeasonLengthScale, 15.0, measured => measured.MostPoints),
    ];
}