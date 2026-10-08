using HockeySim.Domain;

namespace HockeySim.Simulation.Play;

/// <summary>
/// Decides the fouls committed in play. Defenders who are beaten hook and trip, attackers interfere
/// and slash, illegal hits, scrums, and fights follow hits, and tired or undisciplined skaters foul
/// more. A foul by the team with the puck stops play at once. A foul by the other team is a delayed
/// penalty: the team with the puck pulls its goalie for an extra attacker until the offenders touch
/// the puck or play stops, and a goal in that time wipes out a minor. <see cref="PenaltyAssessment"/>
/// assesses the penalties.
/// </summary>
internal sealed class FoulPlay(
    MatchState state,
    Possession possession,
    PenaltyAssessment penalties,
    ShotPlay shots,
    InjuryPlay injuries,
    SkillComparison skill)
{
    /// <summary>
    /// Decides whether a skater commits a penalty in this step instead of playing it: the defenders
    /// more when they are being beaten, and either side more when its skaters on the ice are
    /// undisciplined or tired. Returns whether a foul was committed, and whether a penalty shot
    /// awarded for it scored.
    /// </summary>
    public bool TryFoul(out bool scored)
    {
        scored = false;
        if (state.DelayedAgainst is not null)
        {
            return false;
        }

        var attacker = state.Possessor;
        var defender = state.Opponent(attacker);
        var defendingChance = CanPenalize(defender)
            ? MatchTuning.DefendingPenaltyChance
                * Math.Exp(MatchTuning.PenaltyEdgeSensitivity * skill.SkillEdge())
                * IndisciplineFactor(defender)
            : 0;
        var attackingChance = CanPenalize(attacker)
            ? MatchTuning.AttackingPenaltyChance * IndisciplineFactor(attacker)
            : 0;
        if (!state.Random.Chance(defendingChance + attackingChance))
        {
            return false;
        }

        if (state.Random.Chance(defendingChance / (defendingChance + attackingChance)))
        {
            var offender = ChooseOffender(defender);
            var onTheRush = state.Rush && state.Zone == Zone.Offensive;

            // A foul from behind with the net empty is penalized like any other; awarded goals are
            // not modelled.
            if (onTheRush && !defender.IsGoaliePulled && state.Random.Chance(MatchTuning.PenaltyShotShare))
            {
                scored = shots.TakePenaltyShot(defender, offender, DrawInfraction(MatchTuning.RushFouls));
                return true;
            }

            var fouls = onTheRush || state.Zone == Zone.Neutral ? MatchTuning.RushFouls
                : state.Zone == Zone.Offensive ? MatchTuning.DefendingZoneFouls
                : MatchTuning.ForecheckingFouls;
            CommitFoul(defender, offender, DrawInfraction(fouls));
        }
        else
        {
            var fouls = state.Zone == Zone.Defensive ? MatchTuning.OwnZoneFoulsWithThePuck : MatchTuning.AttackingFouls;
            CommitFoul(attacker, ChooseOffender(attacker), DrawInfraction(fouls));
        }

        return true;
    }

    /// <summary>
    /// After a hit, the hitter may be penalized for it, the two players may square off in a
    /// scrum, or the toughest of the hit team's skaters may answer with a fight. Fights are likelier
    /// between tough players and in lopsided matches; neither fights nor scrums happen in overtime.
    /// </summary>
    public void AfterHit(MatchSide hitterSide, SkaterState hitter, MatchSide hitSide, SkaterState hitPlayer)
    {
        if (!CanPenalize(hitterSide) || !CanPenalize(hitSide))
        {
            return;
        }

        var overtime = state.Period > MatchResult.RegulationPeriodCount;
        var responder = hitSide.OnIce.MaxBy(slot => slot.Skater.Toughness).Skater;
        var blowout = Math.Abs(state.HomeGoals - state.AwayGoals) >= MatchTuning.BlowoutGoalDifference;

        var illegalHitChance = MatchTuning.IllegalHitChance * Indiscipline(hitter);
        var scrumChance = overtime
            ? 0
            : MatchTuning.ScrumChance * Math.Sqrt(Indiscipline(hitter) * Indiscipline(hitPlayer));
        var fightChance = overtime
            ? 0
            : MatchTuning.FightChance
                * Math.Exp(MatchTuning.FightToughnessSensitivity * (((hitter.Toughness + responder.Toughness) / 2) - MatchTuning.ReferenceRating))
                * (blowout ? MatchTuning.BlowoutFightMultiplier : 1);
        if (!state.Random.Chance(illegalHitChance + scrumChance + fightChance))
        {
            return;
        }

        switch (state.Random.NextWeightedIndex([illegalHitChance, scrumChance, fightChance]))
        {
            case 0:
                CommitFoul(hitterSide, hitter, DrawInfraction(MatchTuning.IllegalHits));
                break;
            case 1:
                var scrum = new List<CalledPenalty>
                {
                    new(hitterSide, hitter, Infraction.Roughing, PenaltyKind.Minor),
                    new(hitSide, hitPlayer, Infraction.Roughing, PenaltyKind.Minor),
                };
                foreach (var (side, player) in new[] { (hitterSide, hitter), (hitSide, hitPlayer) })
                {
                    if (state.Random.Chance(MatchTuning.ScrumMisconductChance))
                    {
                        scrum.Add(new CalledPenalty(side, player, Infraction.Roughing, PenaltyKind.Misconduct));
                    }
                }

                penalties.AssessPenalties(scrum);
                possession.Stoppage(zoneOwner: null);
                break;
            default:
                penalties.AssessPenalties(
                [
                    new CalledPenalty(hitterSide, hitter, Infraction.Fighting, PenaltyKind.Major),
                    new CalledPenalty(hitSide, responder, Infraction.Fighting, PenaltyKind.Major),
                ]);
                possession.Stoppage(zoneOwner: null);
                injuries.Contact(hitterSide, hitter, InjuryCause.Fight);
                injuries.Contact(hitSide, responder, InjuryCause.Fight);
                break;
        }
    }

    /// <summary>
    /// A foul by one skater. The team with the puck is whistled at once, with the faceoff in its
    /// own zone; otherwise the penalty is delayed and the team with the puck pulls its goalie.
    /// A major for anything but fighting also carries a game misconduct.
    /// </summary>
    private void CommitFoul(MatchSide side, SkaterState offender, Infraction infraction)
    {
        var kind = DrawKind(infraction);
        var called = new List<CalledPenalty> { new(side, offender, infraction, kind) };
        if (kind == PenaltyKind.Major && infraction != Infraction.Fighting)
        {
            called.Add(new CalledPenalty(side, offender, infraction, PenaltyKind.GameMisconduct));
        }

        state.Rush = false;
        if (side == state.Possessor)
        {
            penalties.AssessPenalties(called);
            possession.Stoppage(zoneOwner: side);
            return;
        }

        state.DelayedAgainst = side;
        state.DelayedPenalties.AddRange(called);
        state.Opponent(side).PullGoalieForDelayedPenalty(true);
        state.ForgetOnIce();
    }

    /// <summary>A team down to its last few available skaters is not penalized further.</summary>
    private static bool CanPenalize(MatchSide side) => side.AvailableSkaterCount >= MatchTuning.MinimumAvailableSkaters;

    /// <summary>Above one when a side's skaters on the ice are less disciplined than the reference, or tired.</summary>
    private static double IndisciplineFactor(MatchSide side) =>
        Math.Exp(-MatchTuning.DisciplineSensitivity * (side.MeanOnIce(slot => slot.Skater.Discipline) - MatchTuning.ReferenceRating));

    private static double Indiscipline(SkaterState skater) =>
        Math.Exp(-MatchTuning.DisciplineSensitivity * ((skater.Discipline * skater.Performance) - MatchTuning.ReferenceRating));

    private SkaterState ChooseOffender(MatchSide side) =>
        state.Choose(side, slot => MatchTuning.MinimumIndisciplineWeight + 100 - (slot.Skater.Discipline * slot.Skater.Performance));

    private Infraction DrawInfraction((Infraction Infraction, double Weight)[] fouls)
    {
        var weights = new double[fouls.Length];
        for (var index = 0; index < fouls.Length; index++)
        {
            weights[index] = fouls[index].Weight;
        }

        return fouls[state.Random.NextWeightedIndex(weights)].Infraction;
    }

    private PenaltyKind DrawKind(Infraction infraction)
    {
        var majorChance = infraction switch
        {
            Infraction.Boarding or Infraction.Charging or Infraction.Elbowing => MatchTuning.DangerousHitMajorChance,
            Infraction.CrossChecking or Infraction.Slashing or Infraction.HighSticking => MatchTuning.StickFoulMajorChance,
            _ => 0,
        };
        if (majorChance > 0 && state.Random.Chance(majorChance))
        {
            return PenaltyKind.Major;
        }

        return infraction == Infraction.HighSticking && state.Random.Chance(MatchTuning.DoubleMinorHighStickingChance)
            ? PenaltyKind.DoubleMinor
            : PenaltyKind.Minor;
    }
}