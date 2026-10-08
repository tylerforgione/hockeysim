using HockeySim.Simulation.Events;

namespace HockeySim.Simulation.Play;

/// <summary>
/// The expected-goal (xG) model: the chance that an unblocked shot attempt scores, from its
/// context alone, for a league-average shooter against a league-average goalie. Blocked attempts
/// have no xG, as in public NHL models built on unblocked (Fenwick) attempts, and neither do
/// attempts at an empty net, which those models also leave out.
/// </summary>
/// <remarks>
/// Each danger level has a base chance. A rebound multiplies the odds of scoring by
/// <see cref="MatchTuning.ReboundOddsMultiplier"/> and a rush by
/// <see cref="MatchTuning.RushOddsMultiplier"/>. The engine then splits the xG into reaching the net and beating the goalie, adjusting each by the
/// shooter's and goalie's ratings, so a reference-rated shooter against a reference-rated goalie
/// scores exactly as often as the xG says. The values are in <see cref="MatchTuning"/>; see
/// docs/areas/match-engine.md.
/// </remarks>
internal static class ExpectedGoalsModel
{
    public static double ExpectedGoals(ShotContext context)
    {
        if (context.IsPenaltyShot)
        {
            return MatchTuning.PenaltyShotExpectedGoals;
        }

        var logOdds = Probability.Logit(BaseExpectedGoals(context.Danger));
        if (context.IsRebound)
        {
            logOdds += Math.Log(MatchTuning.ReboundOddsMultiplier);
        }

        if (context.IsRush)
        {
            logOdds += Math.Log(MatchTuning.RushOddsMultiplier);
        }

        return Probability.Logistic(logOdds);
    }

    /// <summary>The chance a reference-rated shooter's unblocked attempt reaches the net.</summary>
    public static double OnNetChance(ShotContext context) =>
        context.IsRebound
            ? MatchTuning.ReboundOnNetChance
            : context.Danger switch
            {
                ShotDanger.Low => MatchTuning.LowDangerOnNetChance,
                ShotDanger.Medium => MatchTuning.MediumDangerOnNetChance,
                _ => MatchTuning.HighDangerOnNetChance,
            };

    private static double BaseExpectedGoals(ShotDanger danger) => danger switch
    {
        ShotDanger.Low => MatchTuning.LowDangerExpectedGoals,
        ShotDanger.Medium => MatchTuning.MediumDangerExpectedGoals,
        _ => MatchTuning.HighDangerExpectedGoals,
    };
}