namespace HockeySim.Domain;

/// <summary>
/// A skater's current-season totals, accumulated from each appearance's box score.
/// </summary>
public sealed record SkaterSeasonStatistics(PlayerId PlayerId, TeamId TeamId)
{
    public int GamesPlayed { get; private init; }

    public int Goals { get; private init; }

    public int Assists { get; private init; }

    public int Points => Goals + Assists;

    internal SkaterSeasonStatistics Add(SkaterBoxScore boxScore) =>
        this with
        {
            GamesPlayed = GamesPlayed + 1,
            Goals = Goals + boxScore.Goals,
            Assists = Assists + boxScore.Assists,
        };
}