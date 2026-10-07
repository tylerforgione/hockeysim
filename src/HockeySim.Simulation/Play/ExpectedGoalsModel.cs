using HockeySim.Simulation.Events;

namespace HockeySim.Simulation.Play;

/// <summary>
/// The expected-goal (xG) model: the chance that an unblocked shot attempt scores, from its
/// context alone, for a league-average shooter against a league-average goalie. Blocked attempts
/// have no xG, as in public NHL models built on unblocked (Fenwick) attempts.
/// </summary>
/// <remarks>
/// Each danger level has a base chance. A rebound multiplies the odds of scoring by
/// <see cref="ReboundOddsMultiplier"/> and a rush by <see cref="RushOddsMultiplier"/>. The engine
/// then splits the xG into reaching the net and beating the goalie, adjusting each by the
/// shooter's and goalie's ratings, so a reference-rated shooter against a reference-rated goalie
/// scores exactly as often as the xG says. See docs/architecture.md.
/// </remarks>
internal static class ExpectedGoalsModel
{
    public const double LowDangerExpectedGoals = 0.02;
    public const double MediumDangerExpectedGoals = 0.055;
    public const double HighDangerExpectedGoals = 0.14;
    public const double ReboundOddsMultiplier = 2.0;
    public const double RushOddsMultiplier = 1.3;

    // The share of unblocked attempts by a reference-rated shooter that reach the net.
    public const double LowDangerOnNetChance = 0.66;
    public const double MediumDangerOnNetChance = 0.72;
    public const double HighDangerOnNetChance = 0.76;
    public const double ReboundOnNetChance = 0.80;

    public static double ExpectedGoals(ShotContext context)
    {
        var logOdds = Probability.Logit(BaseExpectedGoals(context.Danger));
        if (context.IsRebound)
        {
            logOdds += Math.Log(ReboundOddsMultiplier);
        }

        if (context.IsRush)
        {
            logOdds += Math.Log(RushOddsMultiplier);
        }

        return Probability.Logistic(logOdds);
    }

    /// <summary>The chance a reference-rated shooter's unblocked attempt reaches the net.</summary>
    public static double OnNetChance(ShotContext context) =>
        context.IsRebound
            ? ReboundOnNetChance
            : context.Danger switch
            {
                ShotDanger.Low => LowDangerOnNetChance,
                ShotDanger.Medium => MediumDangerOnNetChance,
                _ => HighDangerOnNetChance,
            };

    private static double BaseExpectedGoals(ShotDanger danger) => danger switch
    {
        ShotDanger.Low => LowDangerExpectedGoals,
        ShotDanger.Medium => MediumDangerExpectedGoals,
        _ => HighDangerExpectedGoals,
    };
}