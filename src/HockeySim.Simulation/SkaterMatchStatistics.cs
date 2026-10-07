using HockeySim.Domain;

namespace HockeySim.Simulation;

/// <summary>
/// One dressed skater's production in a match, derived from the play-by-play and their shifts.
/// Every dressed skater has an entry, including those who recorded nothing; scratched players
/// have none. Shootout attempts are not counted.
/// </summary>
/// <param name="PlusMinus">
/// Goals the team scored at even strength while the skater was on the ice, less those it
/// conceded.
/// </param>
/// <param name="TimeOnIce">Time on the ice in regulation and overtime, in whole seconds.</param>
/// <param name="Shots">Shots on goal, including goals.</param>
/// <param name="ShotAttempts">Shots on goal, missed shots, and shots that were blocked.</param>
/// <param name="BlockedShots">The opponent's shot attempts this skater blocked.</param>
/// <param name="ExpectedGoals">The summed expected-goal value of this skater's unblocked attempts.</param>
public sealed record SkaterMatchStatistics(
    PlayerId PlayerId,
    int Goals,
    int Assists,
    int PlusMinus,
    TimeSpan TimeOnIce,
    int Shots,
    int ShotAttempts,
    int Hits,
    int BlockedShots,
    int FaceoffsWon,
    int FaceoffsLost,
    int Takeaways,
    int Giveaways,
    double ExpectedGoals)
{
    /// <summary>
    /// Always one: an entry is an appearance. Exposed so season totals can sum it directly.
    /// </summary>
    public int GamesPlayed => 1;

    public int Points => Goals + Assists;
}