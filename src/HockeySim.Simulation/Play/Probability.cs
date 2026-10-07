namespace HockeySim.Simulation.Play;

/// <summary>Log-odds helpers for shifting a base chance by rating differences.</summary>
internal static class Probability
{
    /// <summary>
    /// Shifts a base probability by an adjustment in log-odds, so no adjustment gives the base
    /// probability and differences move it smoothly toward zero or one, within the engine's bounds.
    /// </summary>
    public static double Adjust(double baseProbability, double logOddsAdjustment) =>
        Math.Clamp(
            Logistic(Logit(baseProbability) + logOddsAdjustment),
            MatchTuning.MinimumChance,
            MatchTuning.MaximumChance);

    public static double Logit(double probability) => Math.Log(probability / (1 - probability));

    public static double Logistic(double logOdds) => 1 / (1 + Math.Exp(-logOdds));
}