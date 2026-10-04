using HockeySim.Desktop.Schedule;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Players;

/// <summary>
/// One player's current-season totals as the roster and player detail show them. Skaters use
/// goals and assists; goalies use shots and goals against. A player who has not appeared has
/// <see cref="None"/>: zero games and no save percentage.
/// </summary>
public sealed record PlayerSeasonTotals(int GamesPlayed, int Goals, int Assists, int ShotsAgainst, int GoalsAgainst)
{
    public static PlayerSeasonTotals None { get; } = new(0, 0, 0, 0, 0);

    public int Points => Goals + Assists;

    public int Saves => ShotsAgainst - GoalsAgainst;

    /// <summary>".912" style, or a dash before the goalie has faced a shot.</summary>
    public string SavePercentage => MatchDisplay.SavePercentage(Saves, ShotsAgainst);

    /// <summary>
    /// Indexes the season's totals by player. Skaters and goalies are disjoint: a goalie only
    /// appears in goal, so no player has both kinds of totals.
    /// </summary>
    public static Dictionary<PlayerId, PlayerSeasonTotals> Index(SeasonSnapshot season)
    {
        ArgumentNullException.ThrowIfNull(season);

        return season.SkaterStatistics
            .Select(skater => (skater.PlayerId, Totals: new PlayerSeasonTotals(skater.GamesPlayed, skater.Goals, skater.Assists, 0, 0)))
            .Concat(season.GoalieStatistics
                .Select(goalie => (goalie.PlayerId, Totals: new PlayerSeasonTotals(goalie.GamesPlayed, 0, 0, goalie.ShotsAgainst, goalie.GoalsAgainst))))
            .ToDictionary(pair => pair.PlayerId, pair => pair.Totals);
    }
}