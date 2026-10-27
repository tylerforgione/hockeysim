using HockeySim.Domain;

namespace HockeySim.Simulation.Play;

/// <summary>
/// The one rule for a team's <see cref="StrengthSituation"/>, shared by the statistics derived
/// from the play-by-play and by the time on ice the match keeps as it elapses, so a shot and the
/// seconds around it are always put in the same situation.
/// </summary>
internal static class StrengthSituations
{
    /// <summary>
    /// The skaters the penalties allow a team: those on the ice, less the extra attacker that
    /// replaces a pulled goalie.
    /// </summary>
    public static int Manpower(int skatersOnIce, bool goaliePulled) => goaliePulled ? skatersOnIce - 1 : skatersOnIce;

    public static StrengthSituation For(int manpower, int opponentManpower, bool bothGoaliesInNet) =>
        manpower > opponentManpower ? StrengthSituation.PowerPlay
        : manpower < opponentManpower ? StrengthSituation.PenaltyKill
        : manpower == 5 && bothGoaliesInNet ? StrengthSituation.FiveOnFive
        : StrengthSituation.Other;
}