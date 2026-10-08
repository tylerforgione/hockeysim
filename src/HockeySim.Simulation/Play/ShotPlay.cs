using HockeySim.Domain;
using HockeySim.Simulation.Events;

namespace HockeySim.Simulation.Play;

/// <summary>
/// Plays shot attempts and goals: the attempt's context and shooter, blocks, misses, saves,
/// rebounds, assists, and what a goal does to pulled goalies and penalties. A penalty shot is
/// taken here too, once <see cref="FoulPlay"/> has awarded it.
/// </summary>
internal sealed class ShotPlay(
    MatchState state,
    Possession possession,
    PenaltyAssessment penalties,
    GoaliePulls goaliePulls,
    InjuryPlay injuries,
    SkillComparison skill)
{
    /// <summary>
    /// Plays a shot attempt. Returns whether it scored.
    /// </summary>
    /// <param name="fromDistance">
    /// A shot at an empty net from short of the attacking zone. It reaches the net less often, and
    /// a miss from the team's own zone is icing.
    /// </param>
    public bool Shoot(bool rush, bool isRebound, bool fromDistance)
    {
        var attacker = state.Possessor;
        var defender = state.Opponent(attacker);
        var emptyNet = defender.IsGoaliePulled;
        var context = isRebound ? new ShotContext(ShotDanger.High, IsRebound: true, IsRush: false)
            : fromDistance ? new ShotContext(ShotDanger.Low, IsRebound: false, IsRush: false)
            : new ShotContext(DrawDanger(rush), IsRebound: false, IsRush: rush);
        context = context with { IsEmptyNet = emptyNet };
        var shooterSlot = ChooseShooter(attacker, context.Danger);
        var shooter = shooterSlot.Skater;

        var blocker = state.Choose(defender, slot =>
            (slot.IsDefence ? MatchTuning.DefenceBlockerWeight : MatchTuning.ForwardBlockerWeight)
            * (0.5 + (slot.Skater.ShotBlocking / 100)));
        var blockChance = Probability.Adjust(
            BaseBlockChance(context),
            MatchTuning.BlockingSensitivity * ((blocker.ShotBlocking * blocker.Performance) - MatchTuning.ReferenceRating));
        if (state.Random.Chance(blockChance))
        {
            state.Record(new ShotAttemptEvent(state.Period, state.Now, state.OnIce(), attacker.TeamId, shooter.Id, context, ShotOutcome.Blocked, blocker.Id, null));
            injuries.Contact(defender, blocker, InjuryCause.BlockedShot);
            if (state.Random.Chance(MatchTuning.BlockedShotRecoveryChance))
            {
                possession.GiveTo(defender, Zone.Defensive);
            }

            return false;
        }

        double? expectedGoals = emptyNet ? null : ExpectedGoalsModel.ExpectedGoals(context);
        var onNetBase = fromDistance ? MatchTuning.LongEmptyNetShotOnNetChance : ExpectedGoalsModel.OnNetChance(context);
        var onNetChance = Probability.Adjust(
            onNetBase,
            MatchTuning.AccuracySensitivity * ((shooter.Accuracy * shooter.Performance) - MatchTuning.ReferenceRating));
        if (!state.Random.Chance(onNetChance))
        {
            state.Record(new ShotAttemptEvent(state.Period, state.Now, state.OnIce(), attacker.TeamId, shooter.Id, context, ShotOutcome.Missed, null, expectedGoals));
            if (fromDistance)
            {
                MissFromDistance(attacker, defender);
            }
            else if (state.Random.Chance(MatchTuning.MissedShotStoppageChance))
            {
                possession.Stoppage(zoneOwner: defender);
            }
            else if (state.Random.Chance(MatchTuning.MissedShotRecoveryChance))
            {
                possession.GiveTo(defender, Zone.Defensive);
            }

            return false;
        }

        // With nobody in net, every shot that reaches it scores.
        if (emptyNet)
        {
            ScoreGoal(attacker, shooter, context, expectedGoals);
            return true;
        }

        // Dividing by the reference on-net chance makes a reference shooter against a reference
        // goalie score exactly at the expected-goal rate.
        var goalChance = Probability.Adjust(
            expectedGoals!.Value / onNetBase,
            (MatchTuning.FinishingSensitivity * ((shooterSlot.Finishing * shooter.Performance) - MatchTuning.ReferenceRating))
            - (MatchTuning.GoaltendingSensitivity * (defender.Saving - MatchTuning.ReferenceRating)));
        if (state.Random.Chance(goalChance))
        {
            ScoreGoal(attacker, shooter, context, expectedGoals);
            return true;
        }

        state.Record(new ShotAttemptEvent(state.Period, state.Now, state.OnIce(), attacker.TeamId, shooter.Id, context, ShotOutcome.Saved, null, expectedGoals));
        var reboundChance = Probability.Adjust(
            MatchTuning.BaseReboundChance,
            -MatchTuning.ReboundControlSensitivity * (defender.ReboundControl - MatchTuning.ReferenceRating));
        if (state.Random.Chance(reboundChance))
        {
            state.Rebound = true;
        }
        else if (state.Random.Chance(MatchTuning.FrozenPuckChance))
        {
            possession.Stoppage(zoneOwner: defender);
        }
        else
        {
            possession.GiveTo(defender, Zone.Defensive);
        }

        return false;
    }

    /// <summary>
    /// A foul from behind on a scoring chance: the fouled team's shooter goes alone against the
    /// goalie, like a shootout attempt. Returns whether it scored.
    /// </summary>
    public bool TakePenaltyShot(MatchSide defender, SkaterState offender, Infraction infraction)
    {
        var attacker = state.Possessor;
        state.Rush = false;
        penalties.AssessPenalties([new CalledPenalty(defender, offender, infraction, PenaltyKind.PenaltyShot)]);

        var shooter = ChooseShooter(attacker, ShotDanger.High).Skater;
        var context = new ShotContext(ShotDanger.High, IsRebound: false, IsRush: false, IsPenaltyShot: true);
        var expectedGoals = ExpectedGoalsModel.ExpectedGoals(context);
        if (state.Random.Chance(ShootoutPlay.OneOnOneGoalChance(shooter, defender)))
        {
            ScoreGoal(attacker, shooter, context, expectedGoals);
            return true;
        }

        state.Record(new ShotAttemptEvent(state.Period, state.Now, state.OnIce(), attacker.TeamId, shooter.Id, context, ShotOutcome.Saved, null, expectedGoals));
        possession.Stoppage(zoneOwner: defender);
        return false;
    }

    /// <summary>
    /// Records a goal and its assists. Both goalies go back to their nets, a power-play goal ends a
    /// minor, and a goal during a delayed penalty wipes out a minor.
    /// </summary>
    public void ScoreGoal(MatchSide attacker, SkaterState scorer, ShotContext context, double? expectedGoals)
    {
        var defender = state.Opponent(attacker);
        var situation = context.IsPenaltyShot ? GoalSituation.PenaltyShot
            : penalties.Manpower(attacker) > penalties.Manpower(defender) ? GoalSituation.PowerPlay
            : penalties.Manpower(attacker) < penalties.Manpower(defender) ? GoalSituation.Shorthanded
            : GoalSituation.EvenStrength;

        // A penalty shot is unassisted.
        PlayerId? primary = null;
        PlayerId? secondary = null;
        if (!context.IsPenaltyShot && state.Random.Chance(MatchTuning.PrimaryAssistChance))
        {
            var primaryAssist = ChooseAssist(attacker, scorer, excluded: null);
            primary = primaryAssist.Id;
            if (state.Random.Chance(MatchTuning.SecondaryAssistChance))
            {
                secondary = ChooseAssist(attacker, scorer, primaryAssist).Id;
            }
        }

        state.Record(new GoalEvent(state.Period, state.Now, state.OnIce(), attacker.TeamId, scorer.Id, primary, secondary, context, expectedGoals, situation));
        state.CountGoal(attacker);

        // Both goalies go back to their nets for the faceoff; a team still trailing late pulls
        // its goalie again once it has the puck.
        goaliePulls.ReturnGoaliesPulledToTieTheMatch();

        if (situation == GoalSituation.PowerPlay && state.PenaltyBox.EndMinorAfterPowerPlayGoal(defender))
        {
            penalties.ApplyManpower(startOfPeriod: false);
        }

        // A goal during a delayed penalty wipes out a minor, and one of the minors of a double
        // minor; anything more serious is still assessed.
        if (state.DelayedAgainst is not null)
        {
            penalties.EndDelayedPenalty(state.DelayedPenalties
                .Where(penalty => penalty.Kind != PenaltyKind.Minor)
                .Select(penalty => penalty.Kind == PenaltyKind.DoubleMinor ? penalty with { Kind = PenaltyKind.Minor } : penalty)
                .ToList());
        }

        possession.Stoppage(zoneOwner: null);
    }

    /// <summary>
    /// A long shot at an empty net that misses is icing from the team's own zone, unless the team
    /// is killing a penalty; otherwise the defenders retrieve it in their zone.
    /// </summary>
    private void MissFromDistance(MatchSide attacker, MatchSide defender)
    {
        if (state.Zone == Zone.Defensive && penalties.Manpower(attacker) >= penalties.Manpower(defender))
        {
            possession.Stoppage(zoneOwner: attacker);
        }
        else
        {
            possession.GiveTo(defender, Zone.Defensive);
        }
    }

    private SkaterState ChooseAssist(MatchSide attacker, SkaterState scorer, SkaterState? excluded) =>
        state.Choose(attacker, slot => slot.Skater == scorer || slot.Skater == excluded
            ? 0
            : MatchTuning.MinimumAssistWeight + (slot.Skater.Playmaking * slot.Skater.Performance));

    private ShotDanger DrawDanger(bool rush)
    {
        if (rush)
        {
            return (ShotDanger)state.Random.NextWeightedIndex(
            [
                MatchTuning.RushLowDangerWeight,
                MatchTuning.RushMediumDangerWeight,
                MatchTuning.RushHighDangerWeight * (state.OpenIce ? MatchTuning.OpenIceHighDangerMultiplier : 1),
            ]);
        }

        // Better attackers get to better ice; better defenders keep them to the outside.
        var edge = Math.Exp(MatchTuning.DangerEdgeSensitivity * skill.AttackingEdge());
        return (ShotDanger)state.Random.NextWeightedIndex(
        [
            MatchTuning.LowDangerWeight / edge,
            MatchTuning.MediumDangerWeight,
            MatchTuning.HighDangerWeight * edge * (state.OpenIce ? MatchTuning.OpenIceHighDangerMultiplier : 1),
        ]);
    }

    private OnIceSkater ChooseShooter(MatchSide attacker, ShotDanger danger) =>
        state.ChooseSlot(attacker, slot => ShooterRoleWeight(slot.IsDefence, danger) * (0.5 + (slot.Offence / 100)));

    private static double ShooterRoleWeight(bool isDefence, ShotDanger danger) => (isDefence, danger) switch
    {
        (false, ShotDanger.Low) => MatchTuning.ForwardLowDangerShooterWeight,
        (false, ShotDanger.Medium) => MatchTuning.ForwardMediumDangerShooterWeight,
        (false, _) => MatchTuning.ForwardHighDangerShooterWeight,
        (true, ShotDanger.Low) => MatchTuning.DefenceLowDangerShooterWeight,
        (true, ShotDanger.Medium) => MatchTuning.DefenceMediumDangerShooterWeight,
        (true, _) => MatchTuning.DefenceHighDangerShooterWeight,
    };

    private static double BaseBlockChance(ShotContext context)
    {
        if (context.IsRebound)
        {
            return MatchTuning.ReboundBlockChance;
        }

        var chance = context.Danger switch
        {
            ShotDanger.Low => MatchTuning.LowDangerBlockChance,
            ShotDanger.Medium => MatchTuning.MediumDangerBlockChance,
            _ => MatchTuning.HighDangerBlockChance,
        };
        return context.IsRush ? chance * MatchTuning.RushBlockMultiplier : chance;
    }
}