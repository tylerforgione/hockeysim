namespace HockeySim.Domain;

/// <summary>
/// A skater's current-season totals, accumulated from each appearance's box score. Shootouts are
/// excluded, as they are from every individual statistic.
/// </summary>
public sealed record SkaterSeasonStatistics(PlayerId PlayerId, TeamId TeamId)
{
    public int GamesPlayed { get; private init; }

    public int Goals { get; private init; }

    public int Assists { get; private init; }

    public int Points => Goals + Assists;

    public int PlusMinus { get; private init; }

    public TimeSpan TimeOnIce { get; private init; }

    public int Shots { get; private init; }

    public int ShotAttempts { get; private init; }

    public int Hits { get; private init; }

    public int BlockedShots { get; private init; }

    public int FaceoffsWon { get; private init; }

    public int FaceoffsLost { get; private init; }

    public int Takeaways { get; private init; }

    public int Giveaways { get; private init; }

    public double ExpectedGoals { get; private init; }

    public int PenaltyMinutes { get; private init; }

    public int PowerPlayGoals { get; private init; }

    public int PowerPlayAssists { get; private init; }

    public int PowerPlayPoints => PowerPlayGoals + PowerPlayAssists;

    public int ShorthandedGoals { get; private init; }

    public int ShorthandedAssists { get; private init; }

    public int ShorthandedPoints => ShorthandedGoals + ShorthandedAssists;

    public int EmptyNetGoals { get; private init; }

    /// <summary>Both teams' shot totals while the skater was on the ice, by strength situation.</summary>
    public SituationalShotTotals OnIce { get; private init; } = SituationalShotTotals.None;

    /// <summary>
    /// Faceoffs won as a share of those taken, or <see langword="null"/> for a skater who has taken none.
    /// </summary>
    public double? FaceoffPercentage =>
        FaceoffsWon + FaceoffsLost == 0 ? null : FaceoffsWon / (double)(FaceoffsWon + FaceoffsLost);

    /// <summary>Average time on ice, or <see langword="null"/> before a first appearance.</summary>
    public TimeSpan? TimeOnIcePerGame => SeasonAverages.PerGame(TimeOnIce, GamesPlayed);

    internal SkaterSeasonStatistics Add(SkaterBoxScore boxScore) =>
        this with
        {
            GamesPlayed = GamesPlayed + 1,
            Goals = Goals + boxScore.Goals,
            Assists = Assists + boxScore.Assists,
            PlusMinus = PlusMinus + boxScore.PlusMinus,
            TimeOnIce = TimeOnIce + boxScore.TimeOnIce,
            Shots = Shots + boxScore.Shots,
            ShotAttempts = ShotAttempts + boxScore.ShotAttempts,
            Hits = Hits + boxScore.Hits,
            BlockedShots = BlockedShots + boxScore.BlockedShots,
            FaceoffsWon = FaceoffsWon + boxScore.FaceoffsWon,
            FaceoffsLost = FaceoffsLost + boxScore.FaceoffsLost,
            Takeaways = Takeaways + boxScore.Takeaways,
            Giveaways = Giveaways + boxScore.Giveaways,
            ExpectedGoals = ExpectedGoals + boxScore.ExpectedGoals,
            PenaltyMinutes = PenaltyMinutes + boxScore.PenaltyMinutes,
            PowerPlayGoals = PowerPlayGoals + boxScore.PowerPlayGoals,
            PowerPlayAssists = PowerPlayAssists + boxScore.PowerPlayAssists,
            ShorthandedGoals = ShorthandedGoals + boxScore.ShorthandedGoals,
            ShorthandedAssists = ShorthandedAssists + boxScore.ShorthandedAssists,
            EmptyNetGoals = EmptyNetGoals + boxScore.EmptyNetGoals,
            OnIce = OnIce.Add(boxScore.OnIce),
        };
}