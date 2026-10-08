namespace HockeySim.Simulation.Play;

/// <summary>
/// Compares the skaters on the ice for the team with the puck against the defending team's. Play
/// outcomes are base rates shifted by these comparisons.
/// </summary>
internal sealed class SkillComparison(MatchState state)
{
    /// <summary>
    /// The attacking skaters' offence less the defending skaters' defence, in rating points, as
    /// they perform at their current energy.
    /// </summary>
    /// <remarks>Each extra skater the attackers have on the ice adds to their edge, and each one fewer takes from it.</remarks>
    public double AttackingEdge() =>
        SkillEdge() + (MatchTuning.ManpowerEdgePerSkater * (state.Possessor.OnIce.Count - state.Opponent(state.Possessor).OnIce.Count));

    /// <summary>The attacking edge from the skaters' ratings alone, whatever the strength state.</summary>
    public double SkillEdge() =>
        state.Possessor.MeanOnIce(slot => slot.Offence) - state.Opponent(state.Possessor).MeanOnIce(slot => slot.Defence);

    /// <summary>Above one when the attackers are stronger, below one when the defenders are.</summary>
    public double AttackingEdgeFactor() => Math.Exp(MatchTuning.PlayEdgeSensitivity * AttackingEdge());

    /// <summary>More physical defending skaters throw more hits.</summary>
    public double HitRateFactor() =>
        Math.Exp(MatchTuning.HitRateSensitivity
            * (state.Opponent(state.Possessor).MeanOnIce(slot => slot.Skater.Physicality) - MatchTuning.ReferenceRating))
        * (state.OpenIce ? MatchTuning.OpenIceHitMultiplier : 1);
}