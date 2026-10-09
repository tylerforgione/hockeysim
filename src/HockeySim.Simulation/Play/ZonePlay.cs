using HockeySim.Domain;
using HockeySim.Simulation.Events;

namespace HockeySim.Simulation.Play;

/// <summary>
/// Plays a step of possession: the team with the puck tries to move it from its own zone through
/// the neutral zone into the attacking zone and create a shot, and the defending team tries to win
/// it back with turnovers and hits. A step can also end in a foul (see <see cref="FoulPlay"/>), a
/// shot (see <see cref="ShotPlay"/>), or a stoppage.
/// </summary>
internal sealed class ZonePlay(
    MatchState state,
    Possession possession,
    PenaltyAssessment penalties,
    ShotPlay shots,
    FoulPlay fouls,
    InjuryPlay injuries,
    SkillComparison skill)
{
    /// <summary>Plays one step of possession. Returns whether a goal was scored.</summary>
    public bool PlayStep()
    {
        if (state.Rebound)
        {
            return PlayRebound();
        }

        if (fouls.TryFoul(out var scored))
        {
            return scored;
        }

        // Facing an empty net, a team that has the puck short of the attacking zone may shoot for
        // it from distance.
        if (state.Zone != Zone.Offensive
            && state.Opponent(state.Possessor).IsGoaliePulled
            && state.Random.Chance(MatchTuning.LongEmptyNetShotChance))
        {
            return shots.Shoot(rush: false, isRebound: false, fromDistance: true);
        }

        return state.Zone switch
        {
            Zone.Defensive => PlayDefensiveZone(),
            Zone.Neutral => PlayNeutralZone(),
            _ => PlayOffensiveZone(),
        };
    }

    private bool PlayDefensiveZone()
    {
        var edge = skill.AttackingEdgeFactor();
        switch (state.Random.NextWeightedIndex(
        [
            MatchTuning.DefensiveZoneExitWeight * edge,
            MatchTuning.DefensiveZoneTurnoverWeight / edge,
            MatchTuning.DefensiveZoneHitWeight * skill.HitRateFactor(),
            MatchTuning.IcingWeight,
            MatchTuning.DefensiveZoneHoldWeight,
        ]))
        {
            case 0:
                state.Zone = Zone.Neutral;
                break;
            case 1:
                Turnover(Zone.Offensive);
                break;
            case 2:
                Hit(Zone.Offensive);
                break;
            case 3:
                // A team killing a penalty may ice the puck; play goes on with the opponent
                // retrieving it.
                var possessor = state.Possessor;
                if (penalties.Manpower(possessor) < penalties.Manpower(state.Opponent(possessor)))
                {
                    possession.GiveTo(state.Opponent(possessor), Zone.Defensive);
                }
                else
                {
                    possession.Stoppage(zoneOwner: possessor);
                }

                break;
        }

        return false;
    }

    private bool PlayNeutralZone()
    {
        var edge = skill.AttackingEdgeFactor();
        switch (state.Random.NextWeightedIndex(
        [
            MatchTuning.CarryInWeight * edge * (state.OpenIce ? MatchTuning.OpenIceCarryInMultiplier : 1),
            MatchTuning.DumpInWeight,
            MatchTuning.NeutralZoneTurnoverWeight / edge,
            MatchTuning.NeutralZoneHitWeight * skill.HitRateFactor(),
            MatchTuning.OffsideWeight,
            MatchTuning.RegroupWeight,
        ]))
        {
            case 0:
                state.Zone = Zone.Offensive;
                state.Rush = true;
                break;
            case 1:
                if (state.Random.Chance(MatchTuning.DumpInRecoveryChance))
                {
                    state.Zone = Zone.Offensive;
                }
                else
                {
                    possession.GiveTo(state.Opponent(state.Possessor), Zone.Defensive);
                }

                break;
            case 2:
                Turnover(Zone.Neutral);
                break;
            case 3:
                Hit(Zone.Neutral);
                break;
            case 4:
                possession.Stoppage(zoneOwner: null);
                break;
            default:
                state.Zone = Zone.Defensive;
                break;
        }

        return false;
    }

    private bool PlayOffensiveZone()
    {
        var rush = state.Rush;
        state.Rush = false;
        var edge = skill.AttackingEdgeFactor();
        switch (state.Random.NextWeightedIndex(
        [
            MatchTuning.ShotAttemptWeight * edge
                * (rush ? MatchTuning.RushShotMultiplier : 1)
                * (state.OpenIce ? MatchTuning.OpenIceShotMultiplier : 1),
            MatchTuning.OffensiveZoneTurnoverWeight / edge,
            MatchTuning.OffensiveZoneHitWeight * skill.HitRateFactor(),
            MatchTuning.ClearedWeight,
            MatchTuning.OffensiveZoneStoppageWeight,
            MatchTuning.CycleWeight,
        ]))
        {
            case 0:
                return shots.Shoot(rush, isRebound: false, fromDistance: false);
            case 1:
                Turnover(Zone.Defensive);
                break;
            case 2:
                Hit(Zone.Defensive);
                break;
            case 3:
                possession.GiveTo(state.Opponent(state.Possessor), Zone.Defensive);
                break;
            case 4:
                possession.Stoppage(zoneOwner: state.Opponent(state.Possessor));
                break;
        }

        return false;
    }

    private bool PlayRebound()
    {
        state.Rebound = false;
        if (state.Random.Chance(MatchTuning.ReboundShotChance))
        {
            return shots.Shoot(rush: false, isRebound: true, fromDistance: false);
        }

        // Nobody got a stick on it; the scramble goes either way.
        if (state.Random.Chance(MatchTuning.ReboundScrambleRecoveryChance))
        {
            possession.GiveTo(state.Opponent(state.Possessor), Zone.Defensive);
        }

        return false;
    }

    private void Turnover(Zone opponentZone)
    {
        var attacker = state.Possessor;
        var defender = state.Opponent(attacker);
        switch (state.Random.NextWeightedIndex(
        [
            MatchTuning.TakeawayShare,
            MatchTuning.GiveawayShare,
            1 - MatchTuning.TakeawayShare - MatchTuning.GiveawayShare,
        ]))
        {
            case 0:
                var taker = state.Choose(defender, slot => 0.5 + (slot.Skater.StickChecking / 100));
                state.Record(new TakeawayEvent(state.Period, state.Now, state.OnIce(), defender.TeamId, taker.Id));
                break;
            case 1:
                var carrier = state.Choose(attacker, slot => 1.5 - (slot.Skater.PuckControl / 100));
                state.Record(new GiveawayEvent(state.Period, state.Now, state.OnIce(), attacker.TeamId, carrier.Id));
                break;
        }

        possession.GiveTo(defender, opponentZone);
    }

    private void Hit(Zone opponentZoneIfTurnedOver)
    {
        var attacker = state.Possessor;
        var defender = state.Opponent(attacker);
        var hitter = state.Choose(defender, slot =>
            (slot.IsDefence ? MatchTuning.DefenceHitterWeight : MatchTuning.ForwardHitterWeight)
            * Math.Max(0.1, 0.5 + (slot.Skater.Physicality / 100)));
        var carrierSlot = attacker.OnIce[state.Random.NextInt(0, attacker.OnIce.Count)];
        var carrier = carrierSlot.Skater;
        state.Record(new HitEvent(state.Period, state.Now, state.OnIce(), defender.TeamId, hitter.Id, carrier.Id));

        var turnoverChance = Probability.Adjust(
            MatchTuning.HitTurnoverChance,
            MatchTuning.HitTurnoverSensitivity
            * ((hitter.Physicality * hitter.Performance) - (carrierSlot.PuckProtection * carrier.Performance)));
        if (state.Random.Chance(turnoverChance))
        {
            possession.GiveTo(defender, opponentZoneIfTurnedOver);
        }

        if (state.DelayedAgainst is null && !state.FaceoffPending)
        {
            fouls.AfterHit(defender, hitter, attacker, carrier);
        }

        injuries.Contact(attacker, carrier, InjuryCause.Hit);
        injuries.Contact(defender, hitter, InjuryCause.Collision);
    }
}