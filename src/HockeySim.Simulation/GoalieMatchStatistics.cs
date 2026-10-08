using HockeySim.Domain;

namespace HockeySim.Simulation;

/// <summary>
/// The starting goalie's record for a match. The starting goalie plays the whole match, so the
/// dressed backup and any scratched goalie have no entry. Shootout attempts are not counted.
/// </summary>
/// <param name="ShotsAgainst">
/// The opponent's shots on goal in regulation and overtime, less its empty-net goals, which the
/// goalie was not in net to face.
/// </param>
/// <param name="GoalsAgainst">
/// The opponent's player-scored goals less its empty-net goals; excludes a shootout deciding goal.
/// </param>
/// <param name="ExpectedGoalsAgainst">
/// The summed expected-goal value of the opponent's unblocked attempts. Empty-net attempts carry
/// none, so this is every attempt the goalie faced.
/// </param>
/// <param name="TimeOnIce">
/// Time in net in regulation and overtime, in whole seconds; time pulled for an extra attacker is
/// not counted.
/// </param>
public sealed record GoalieMatchStatistics(
    PlayerId PlayerId,
    int ShotsAgainst,
    int GoalsAgainst,
    double ExpectedGoalsAgainst,
    TimeSpan TimeOnIce)
{
    /// <summary>
    /// Always one: an entry is an appearance. Exposed so season totals can sum it directly.
    /// </summary>
    public int GamesPlayed => 1;

    public int Saves => ShotsAgainst - GoalsAgainst;

    /// <summary>
    /// Saves as a share of shots against, or <see langword="null"/> when the goalie faced no
    /// shots and the percentage is undefined.
    /// </summary>
    public double? SavePercentage => ShotsAgainst == 0 ? null : Saves / (double)ShotsAgainst;
}