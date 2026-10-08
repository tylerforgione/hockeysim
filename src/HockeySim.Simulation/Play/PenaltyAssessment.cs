using HockeySim.Domain;
using HockeySim.Simulation.Events;

namespace HockeySim.Simulation.Play;

/// <summary>
/// Assesses the penalties called in play and keeps both teams in the strength state the penalties
/// being served call for. Each team plays a skater short for every penalty it is serving, up to
/// two, and the strength state picks the units on the ice. Which fouls are committed is decided by
/// <see cref="FoulPlay"/>.
/// </summary>
internal sealed class PenaltyAssessment(MatchState state)
{
    /// <summary>
    /// Records penalties called at the same moment and sends the players to the box. Penalties on
    /// both teams at once are coincidental and leave neither team short, except that one minor
    /// each at full strength, with no other penalties, plays four-on-four.
    /// </summary>
    public void AssessPenalties(List<CalledPenalty> penalties)
    {
        foreach (var penalty in penalties)
        {
            state.Record(new PenaltyEvent(state.Period, state.Now, state.OnIce(), penalty.Side.TeamId, penalty.Player.Id, penalty.Infraction, penalty.Kind));
        }

        var coincidental = penalties.Select(penalty => penalty.Side).Distinct().Count() > 1;
        var timed = penalties.Where(penalty => penalty.Kind is PenaltyKind.Minor or PenaltyKind.DoubleMinor or PenaltyKind.Major).ToList();
        var fourOnFour = coincidental
            && !state.ThreeOnThreeBase
            && !state.PenaltyBox.HasManpowerPenalties
            && timed.Count == 2
            && timed.All(penalty => penalty.Kind == PenaltyKind.Minor)
            && timed[0].Side != timed[1].Side;

        foreach (var penalty in penalties)
        {
            switch (penalty.Kind)
            {
                case PenaltyKind.GameMisconduct:
                    penalty.Player.Eject();
                    break;
                case PenaltyKind.PenaltyShot:
                    break;
                default:
                    state.PenaltyBox.Add(new ServedPenalty(penalty.Side, penalty.Player, penalty.Kind, affectsManpower: !coincidental || fourOnFour));
                    break;
            }
        }

        ApplyManpower(startOfPeriod: false);
    }

    /// <summary>Ends a delayed penalty: the goalie returns and the penalties still standing are assessed.</summary>
    public void EndDelayedPenalty(List<CalledPenalty> penalties)
    {
        var offenders = state.DelayedAgainst!;
        state.DelayedAgainst = null;
        state.DelayedPenalties.Clear();
        state.Opponent(offenders).PullGoalieForDelayedPenalty(false);
        state.ForgetOnIce();
        AssessPenalties(penalties);
    }

    public void ExpirePenalties()
    {
        if (state.PenaltyBox.ExpirePenalties())
        {
            ApplyManpower(startOfPeriod: false);
        }
    }

    /// <summary>
    /// Puts both teams in the strength state the penalties being served call for, and counts a
    /// power-play opportunity for each penalty the first time it gives the other team the advantage.
    /// </summary>
    public void ApplyManpower(bool startOfPeriod)
    {
        var home = Manpower(state.Home);
        var away = Manpower(state.Away);
        state.Home.SetStrength(home, away, startOfPeriod);
        state.Away.SetStrength(away, home, startOfPeriod);
        CountPowerPlay(state.Home, home > away);
        CountPowerPlay(state.Away, away > home);
        state.ForgetOnIce();
    }

    /// <summary>
    /// The skaters the penalties allow a side, not counting an extra attacker. Each penalty being
    /// served takes one away in regulation; in three-on-three overtime it gives the other team one
    /// more instead, so a team never has fewer than three.
    /// </summary>
    public int Manpower(MatchSide side)
    {
        var shorthanded = state.PenaltyBox.ShorthandedBy(side);
        if (!state.ThreeOnThreeBase)
        {
            return 5 - shorthanded;
        }

        return 3 + Math.Max(0, state.PenaltyBox.ShorthandedBy(state.Opponent(side)) - shorthanded);
    }

    private void CountPowerPlay(MatchSide side, bool hasAdvantage)
    {
        if (!hasAdvantage)
        {
            return;
        }

        foreach (var penalty in state.PenaltyBox.RunningManpowerPenalties(state.Opponent(side)))
        {
            if (!penalty.CountedAsPowerPlay)
            {
                penalty.CountedAsPowerPlay = true;
                side.AddPowerPlayOpportunity();
            }
        }
    }
}

/// <summary>A penalty called on a skater, before it is assessed.</summary>
internal sealed record CalledPenalty(MatchSide Side, SkaterState Player, Infraction Infraction, PenaltyKind Kind);