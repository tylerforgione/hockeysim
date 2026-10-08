using HockeySim.Domain;
using HockeySim.Simulation.Events;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Simulation.Play;

/// <summary>
/// The transient state of one match being played. Play runs on a game clock in whole seconds as
/// a sequence of possession steps: in each step the team with the puck tries to move it up the
/// ice and create a shot, and the defending team tries to win it back. Line changes happen on the
/// fly and at stoppages, and every faceoff, shot attempt, goal, hit, takeaway, and giveaway is
/// recorded in the play-by-play with the players on the ice.
/// </summary>
/// <remarks>
/// Penalties come from the play: defenders who are beaten hook and trip, attackers interfere and
/// slash, illegal hits, scrums, and fights follow hits, and tired or undisciplined skaters foul
/// more. A foul by the team with the puck stops play at once. A foul by the other team is a
/// delayed penalty: the team with the puck pulls its goalie for an extra attacker until the
/// offenders touch the puck or play stops, and a goal in that time wipes out a minor. Each team
/// plays a skater short for every penalty it is serving, up to two, and the strength state picks
/// the units on the ice.
/// <para>
/// Late in the third period a team trailing by one or two goals pulls its goalie for an extra
/// attacker, on top of whatever strength state the penalties allow, and the leading team shoots
/// at the empty net, from distance if it must.
/// </para>
/// </remarks>
internal sealed class MatchPlay
{
    private readonly ControlledRandom _random;
    private readonly OvertimeFormat _overtime;
    private readonly MatchSide _home;
    private readonly MatchSide _away;
    private readonly PenaltyBox _penaltyBox = new();
    private readonly List<MatchEvent> _events = [];
    private readonly List<CalledPenalty> _delayedPenalties = [];

    private OnIcePlayers? _onIce;
    private int _period;
    private int _clock;
    private int _playingSeconds;
    private bool _openIce;

    // Regular-season overtime starts from three skaters a side, and a penalty adds a skater to the
    // other team rather than taking one away.
    private bool _threeOnThreeBase;
    private int _homeGoals;
    private int _awayGoals;

    // The puck: who has it, where, and whether a rush or a rebound is on.
    private MatchSide _possessor;
    private Zone _zone;
    private bool _rush;
    private bool _rebound;

    // A stoppage waiting for its faceoff: at centre ice, or in the named team's defensive zone.
    private bool _faceoffPending;
    private MatchSide? _faceoffZoneOwner;

    // The team that has committed a delayed penalty, while play continues.
    private MatchSide? _delayedAgainst;

    public MatchPlay(Match match, OvertimeFormat overtime, RandomState randomState)
    {
        _random = new ControlledRandom(randomState);
        _overtime = overtime;
        _home = new MatchSide(match.Home, _penaltyBox);
        _away = new MatchSide(match.Away, _penaltyBox);
        _possessor = _home;
    }

    public MatchResult Play()
    {
        for (var period = 1; period <= MatchResult.RegulationPeriodCount; period++)
        {
            PlayPeriod(period, MatchTuning.RegulationPeriodSeconds, threeOnThree: false, suddenDeath: false);
        }

        if (_homeGoals != _awayGoals)
        {
            return CreateResult(MatchDecision.Regulation, shootout: null);
        }

        if (_overtime == OvertimeFormat.Playoff)
        {
            // Sudden-death periods continue until someone scores, however long that takes.
            for (var period = MatchResult.OvertimePeriod; _homeGoals == _awayGoals; period++)
            {
                PlayPeriod(period, MatchTuning.PlayoffOvertimePeriodSeconds, threeOnThree: false, suddenDeath: true);
            }

            return CreateResult(MatchDecision.Overtime, shootout: null);
        }

        PlayPeriod(MatchResult.OvertimePeriod, MatchTuning.RegularSeasonOvertimeSeconds, threeOnThree: true, suddenDeath: true);
        if (_homeGoals != _awayGoals)
        {
            return CreateResult(MatchDecision.Overtime, shootout: null);
        }

        return CreateResult(MatchDecision.Shootout, new ShootoutPlay(_home, _away, _random).Play());
    }

    private void PlayPeriod(int period, int periodSeconds, bool threeOnThree, bool suddenDeath)
    {
        _period = period;
        _clock = 0;
        _openIce = threeOnThree;
        _threeOnThreeBase = threeOnThree;
        if (period > 1)
        {
            _home.RestForIntermission();
            _away.RestForIntermission();
        }

        ApplyManpower(startOfPeriod: true);
        Stoppage(zoneOwner: null);

        while (true)
        {
            PullGoaliesToTieTheMatch(periodSeconds);
            if (_faceoffPending)
            {
                ChangeLines(side => side.ChangeAtStoppage());
                TakeFaceoff();
            }
            else
            {
                // A team keeps its players out while it is attacking, and nobody changes in the
                // middle of a rebound scramble.
                ChangeLines(side => !_rebound && (side != _possessor || _zone != Zone.Offensive) && side.ChangeOnTheFly());
            }

            var seconds = StepSeconds();

            // A penalty that ends during the step ends on time, and play carries on from there.
            if (_penaltyBox.SecondsUntilNextExpiry is { } expiry && expiry <= Math.Min(seconds, periodSeconds - _clock))
            {
                Elapse(expiry);
                ExpirePenalties();
                if (_clock == periodSeconds)
                {
                    EndPeriod();
                    return;
                }

                continue;
            }

            if (_clock + seconds >= periodSeconds)
            {
                Elapse(periodSeconds - _clock);
                EndPeriod();
                return;
            }

            Elapse(seconds);
            if (PlayStep() && suddenDeath)
            {
                return;
            }
        }
    }

    /// <summary>
    /// A delayed penalty still pending at the end of a period is called as it ends, and a goalie
    /// pulled to tie the match goes back to the bench with the rest of the team.
    /// </summary>
    private void EndPeriod()
    {
        if (_delayedAgainst is not null)
        {
            EndDelayedPenalty(_delayedPenalties.ToList());
        }

        ReturnGoaliesPulledToTieTheMatch();
    }

    /// <summary>
    /// Late in the third period a team trailing by one or two goals pulls its goalie, earlier the
    /// further behind it is. It pulls once it has the puck out of its own zone, or for a faceoff in
    /// the attacking zone; the goalie comes back for a faceoff in the team's own zone and goes out
    /// again once the rule allows. A goal puts both goalies back (see <see cref="ScoreGoal"/>).
    /// </summary>
    private void PullGoaliesToTieTheMatch(int periodSeconds)
    {
        if (_period != MatchResult.RegulationPeriodCount)
        {
            return;
        }

        var secondsLeft = periodSeconds - _clock;
        PullOrReturnGoalie(_home, _awayGoals - _homeGoals, secondsLeft);
        PullOrReturnGoalie(_away, _homeGoals - _awayGoals, secondsLeft);
    }

    private void PullOrReturnGoalie(MatchSide side, int deficit, int secondsLeft)
    {
        var shouldPull = deficit switch
        {
            1 => secondsLeft <= MatchTuning.PullGoalieOneGoalDownSeconds,
            2 => secondsLeft <= MatchTuning.PullGoalieTwoGoalsDownSeconds,
            _ => false,
        };

        var pulled = side.IsGoaliePulledToTieTheMatch;
        bool pull;
        if (!shouldPull)
        {
            pull = false;
        }
        else if (_faceoffPending)
        {
            pull = pulled ? _faceoffZoneOwner != side : _faceoffZoneOwner == Opponent(side);
        }
        else
        {
            pull = pulled || (_possessor == side && _zone != Zone.Defensive);
        }

        if (pull != pulled)
        {
            side.PullGoalieToTieTheMatch(pull);
            _onIce = null;
        }
    }

    private void ReturnGoaliesPulledToTieTheMatch()
    {
        _home.PullGoalieToTieTheMatch(false);
        _away.PullGoalieToTieTheMatch(false);
        _onIce = null;
    }

    private void ChangeLines(Func<MatchSide, bool> change)
    {
        // Evaluate both sides; a change by either replaces the players on the ice.
        var homeChanged = change(_home);
        var awayChanged = change(_away);
        if (homeChanged || awayChanged)
        {
            _onIce = null;
        }
    }

    private void Elapse(int seconds)
    {
        _home.Elapse(seconds);
        _away.Elapse(seconds);
        _penaltyBox.Elapse(seconds);
        _clock += seconds;
        _playingSeconds += seconds;
    }

    private int StepSeconds()
    {
        if (_rebound)
        {
            return _random.NextInt(1, MatchTuning.ReboundStepMaximum + 1);
        }

        return _zone switch
        {
            Zone.Defensive => _random.NextInt(MatchTuning.DefensiveZoneStepMinimum, MatchTuning.DefensiveZoneStepMaximum + 1),
            Zone.Neutral => _random.NextInt(MatchTuning.NeutralZoneStepMinimum, MatchTuning.NeutralZoneStepMaximum + 1),
            _ => _random.NextInt(MatchTuning.OffensiveZoneStepMinimum, MatchTuning.OffensiveZoneStepMaximum + 1),
        };
    }

    /// <summary>Plays one step of possession. Returns whether a goal was scored.</summary>
    private bool PlayStep()
    {
        if (_rebound)
        {
            return PlayRebound();
        }

        if (TryFoul(out var scored))
        {
            return scored;
        }

        // Facing an empty net, a team that has the puck short of the attacking zone may shoot for
        // it from distance.
        if (_zone != Zone.Offensive
            && Opponent(_possessor).IsGoaliePulled
            && _random.Chance(MatchTuning.LongEmptyNetShotChance))
        {
            return Shoot(rush: false, isRebound: false, fromDistance: true);
        }

        return _zone switch
        {
            Zone.Defensive => PlayDefensiveZone(),
            Zone.Neutral => PlayNeutralZone(),
            _ => PlayOffensiveZone(),
        };
    }

    private bool PlayDefensiveZone()
    {
        var edge = AttackingEdgeFactor();
        switch (_random.NextWeightedIndex(
        [
            MatchTuning.DefensiveZoneExitWeight * edge,
            MatchTuning.DefensiveZoneTurnoverWeight / edge,
            MatchTuning.DefensiveZoneHitWeight * HitRateFactor(),
            MatchTuning.IcingWeight,
            MatchTuning.DefensiveZoneHoldWeight,
        ]))
        {
            case 0:
                _zone = Zone.Neutral;
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
                if (Manpower(_possessor) < Manpower(Opponent(_possessor)))
                {
                    GiveTo(Opponent(_possessor), Zone.Defensive);
                }
                else
                {
                    Stoppage(zoneOwner: _possessor);
                }

                break;
        }

        return false;
    }

    private bool PlayNeutralZone()
    {
        var edge = AttackingEdgeFactor();
        switch (_random.NextWeightedIndex(
        [
            MatchTuning.CarryInWeight * edge * (_openIce ? MatchTuning.OpenIceCarryInMultiplier : 1),
            MatchTuning.DumpInWeight,
            MatchTuning.NeutralZoneTurnoverWeight / edge,
            MatchTuning.NeutralZoneHitWeight * HitRateFactor(),
            MatchTuning.OffsideWeight,
            MatchTuning.RegroupWeight,
        ]))
        {
            case 0:
                _zone = Zone.Offensive;
                _rush = true;
                break;
            case 1:
                if (_random.Chance(MatchTuning.DumpInRecoveryChance))
                {
                    _zone = Zone.Offensive;
                }
                else
                {
                    GiveTo(Opponent(_possessor), Zone.Defensive);
                }

                break;
            case 2:
                Turnover(Zone.Neutral);
                break;
            case 3:
                Hit(Zone.Neutral);
                break;
            case 4:
                Stoppage(zoneOwner: null);
                break;
            default:
                _zone = Zone.Defensive;
                break;
        }

        return false;
    }

    private bool PlayOffensiveZone()
    {
        var rush = _rush;
        _rush = false;
        var edge = AttackingEdgeFactor();
        switch (_random.NextWeightedIndex(
        [
            MatchTuning.ShotAttemptWeight * edge
                * (rush ? MatchTuning.RushShotMultiplier : 1)
                * (_openIce ? MatchTuning.OpenIceShotMultiplier : 1),
            MatchTuning.OffensiveZoneTurnoverWeight / edge,
            MatchTuning.OffensiveZoneHitWeight * HitRateFactor(),
            MatchTuning.ClearedWeight,
            MatchTuning.OffensiveZoneStoppageWeight,
            MatchTuning.CycleWeight,
        ]))
        {
            case 0:
                return Shoot(rush, isRebound: false, fromDistance: false);
            case 1:
                Turnover(Zone.Defensive);
                break;
            case 2:
                Hit(Zone.Defensive);
                break;
            case 3:
                GiveTo(Opponent(_possessor), Zone.Defensive);
                break;
            case 4:
                Stoppage(zoneOwner: Opponent(_possessor));
                break;
        }

        return false;
    }

    private bool PlayRebound()
    {
        _rebound = false;
        if (_random.Chance(MatchTuning.ReboundShotChance))
        {
            return Shoot(rush: false, isRebound: true, fromDistance: false);
        }

        // Nobody got a stick on it; the scramble goes either way.
        if (_random.Chance(MatchTuning.ReboundScrambleRecoveryChance))
        {
            GiveTo(Opponent(_possessor), Zone.Defensive);
        }

        return false;
    }

    /// <summary>
    /// Plays a shot attempt. Returns whether it scored.
    /// </summary>
    /// <param name="fromDistance">
    /// A shot at an empty net from short of the attacking zone. It reaches the net less often, and
    /// a miss from the team's own zone is icing.
    /// </param>
    private bool Shoot(bool rush, bool isRebound, bool fromDistance)
    {
        var attacker = _possessor;
        var defender = Opponent(attacker);
        var emptyNet = defender.IsGoaliePulled;
        var context = isRebound ? new ShotContext(ShotDanger.High, IsRebound: true, IsRush: false)
            : fromDistance ? new ShotContext(ShotDanger.Low, IsRebound: false, IsRush: false)
            : new ShotContext(DrawDanger(rush), IsRebound: false, IsRush: rush);
        context = context with { IsEmptyNet = emptyNet };
        var shooterSlot = ChooseShooter(attacker, context.Danger);
        var shooter = shooterSlot.Skater;

        var blocker = Choose(defender, slot =>
            (slot.IsDefence ? MatchTuning.DefenceBlockerWeight : MatchTuning.ForwardBlockerWeight)
            * (0.5 + (slot.Skater.ShotBlocking / 100)));
        var blockChance = Probability.Adjust(
            BaseBlockChance(context),
            MatchTuning.BlockingSensitivity * ((blocker.ShotBlocking * blocker.Performance) - MatchTuning.ReferenceRating));
        if (_random.Chance(blockChance))
        {
            Record(new ShotAttemptEvent(_period, Now, OnIce(), attacker.TeamId, shooter.Id, context, ShotOutcome.Blocked, blocker.Id, null));
            if (_random.Chance(MatchTuning.BlockedShotRecoveryChance))
            {
                GiveTo(defender, Zone.Defensive);
            }

            return false;
        }

        double? expectedGoals = emptyNet ? null : ExpectedGoalsModel.ExpectedGoals(context);
        var onNetBase = fromDistance ? MatchTuning.LongEmptyNetShotOnNetChance : ExpectedGoalsModel.OnNetChance(context);
        var onNetChance = Probability.Adjust(
            onNetBase,
            MatchTuning.AccuracySensitivity * ((shooter.Accuracy * shooter.Performance) - MatchTuning.ReferenceRating));
        if (!_random.Chance(onNetChance))
        {
            Record(new ShotAttemptEvent(_period, Now, OnIce(), attacker.TeamId, shooter.Id, context, ShotOutcome.Missed, null, expectedGoals));
            if (fromDistance)
            {
                MissFromDistance(attacker, defender);
            }
            else if (_random.Chance(MatchTuning.MissedShotStoppageChance))
            {
                Stoppage(zoneOwner: defender);
            }
            else if (_random.Chance(MatchTuning.MissedShotRecoveryChance))
            {
                GiveTo(defender, Zone.Defensive);
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
        if (_random.Chance(goalChance))
        {
            ScoreGoal(attacker, shooter, context, expectedGoals);
            return true;
        }

        Record(new ShotAttemptEvent(_period, Now, OnIce(), attacker.TeamId, shooter.Id, context, ShotOutcome.Saved, null, expectedGoals));
        var reboundChance = Probability.Adjust(
            MatchTuning.BaseReboundChance,
            -MatchTuning.ReboundControlSensitivity * (defender.ReboundControl - MatchTuning.ReferenceRating));
        if (_random.Chance(reboundChance))
        {
            _rebound = true;
        }
        else if (_random.Chance(MatchTuning.FrozenPuckChance))
        {
            Stoppage(zoneOwner: defender);
        }
        else
        {
            GiveTo(defender, Zone.Defensive);
        }

        return false;
    }

    /// <summary>
    /// A long shot at an empty net that misses is icing from the team's own zone, unless the team
    /// is killing a penalty; otherwise the defenders retrieve it in their zone.
    /// </summary>
    private void MissFromDistance(MatchSide attacker, MatchSide defender)
    {
        if (_zone == Zone.Defensive && Manpower(attacker) >= Manpower(defender))
        {
            Stoppage(zoneOwner: attacker);
        }
        else
        {
            GiveTo(defender, Zone.Defensive);
        }
    }

    private void ScoreGoal(MatchSide attacker, SkaterState scorer, ShotContext context, double? expectedGoals)
    {
        var defender = Opponent(attacker);
        var situation = context.IsPenaltyShot ? GoalSituation.PenaltyShot
            : Manpower(attacker) > Manpower(defender) ? GoalSituation.PowerPlay
            : Manpower(attacker) < Manpower(defender) ? GoalSituation.Shorthanded
            : GoalSituation.EvenStrength;

        // A penalty shot is unassisted.
        PlayerId? primary = null;
        PlayerId? secondary = null;
        if (!context.IsPenaltyShot && _random.Chance(MatchTuning.PrimaryAssistChance))
        {
            var primaryAssist = ChooseAssist(attacker, scorer, excluded: null);
            primary = primaryAssist.Id;
            if (_random.Chance(MatchTuning.SecondaryAssistChance))
            {
                secondary = ChooseAssist(attacker, scorer, primaryAssist).Id;
            }
        }

        Record(new GoalEvent(_period, Now, OnIce(), attacker.TeamId, scorer.Id, primary, secondary, context, expectedGoals, situation));
        if (attacker == _home)
        {
            _homeGoals++;
        }
        else
        {
            _awayGoals++;
        }

        // Both goalies go back to their nets for the faceoff; a team still trailing late pulls
        // its goalie again once it has the puck.
        ReturnGoaliesPulledToTieTheMatch();

        if (situation == GoalSituation.PowerPlay && _penaltyBox.EndMinorAfterPowerPlayGoal(defender))
        {
            ApplyManpower(startOfPeriod: false);
        }

        // A goal during a delayed penalty wipes out a minor, and one of the minors of a double
        // minor; anything more serious is still assessed.
        if (_delayedAgainst is not null)
        {
            EndDelayedPenalty(_delayedPenalties
                .Where(penalty => penalty.Kind != PenaltyKind.Minor)
                .Select(penalty => penalty.Kind == PenaltyKind.DoubleMinor ? penalty with { Kind = PenaltyKind.Minor } : penalty)
                .ToList());
        }

        Stoppage(zoneOwner: null);
    }

    private SkaterState ChooseAssist(MatchSide attacker, SkaterState scorer, SkaterState? excluded) =>
        Choose(attacker, slot => slot.Skater == scorer || slot.Skater == excluded
            ? 0
            : MatchTuning.MinimumAssistWeight + (slot.Skater.Playmaking * slot.Skater.Performance));

    private void Turnover(Zone opponentZone)
    {
        var attacker = _possessor;
        var defender = Opponent(attacker);
        switch (_random.NextWeightedIndex(
        [
            MatchTuning.TakeawayShare,
            MatchTuning.GiveawayShare,
            1 - MatchTuning.TakeawayShare - MatchTuning.GiveawayShare,
        ]))
        {
            case 0:
                var taker = Choose(defender, slot => 0.5 + (slot.Skater.StickChecking / 100));
                Record(new TakeawayEvent(_period, Now, OnIce(), defender.TeamId, taker.Id));
                break;
            case 1:
                var carrier = Choose(attacker, slot => 1.5 - (slot.Skater.PuckControl / 100));
                Record(new GiveawayEvent(_period, Now, OnIce(), attacker.TeamId, carrier.Id));
                break;
        }

        GiveTo(defender, opponentZone);
    }

    private void Hit(Zone opponentZoneIfTurnedOver)
    {
        var attacker = _possessor;
        var defender = Opponent(attacker);
        var hitter = Choose(defender, slot =>
            (slot.IsDefence ? MatchTuning.DefenceHitterWeight : MatchTuning.ForwardHitterWeight)
            * Math.Max(0.1, 0.5 + (slot.Skater.Physicality / 100)));
        var carrierSlot = attacker.OnIce[_random.NextInt(0, attacker.OnIce.Count)];
        var carrier = carrierSlot.Skater;
        Record(new HitEvent(_period, Now, OnIce(), defender.TeamId, hitter.Id, carrier.Id));

        var turnoverChance = Probability.Adjust(
            MatchTuning.HitTurnoverChance,
            MatchTuning.HitTurnoverSensitivity
            * ((hitter.Physicality * hitter.Performance) - (carrierSlot.PuckProtection * carrier.Performance)));
        if (_random.Chance(turnoverChance))
        {
            GiveTo(defender, opponentZoneIfTurnedOver);
        }

        if (_delayedAgainst is null && !_faceoffPending)
        {
            AfterHit(defender, hitter, attacker, carrier);
        }
    }

    private void TakeFaceoff()
    {
        _faceoffPending = false;
        var homeCentre = _home.Centre;
        var awayCentre = _away.Centre;
        var homeWins = _random.Chance(Probability.Adjust(
            0.5,
            MatchTuning.FaceoffSensitivity * (homeCentre.Faceoffs - awayCentre.Faceoffs)));
        var (home, away) = (homeCentre.Skater, awayCentre.Skater);
        var (winner, winnerCentre, loserCentre) = homeWins ? (_home, home, away) : (_away, away, home);
        Record(new FaceoffEvent(_period, Now, OnIce(), winner.TeamId, winnerCentre.Id, loserCentre.Id, _faceoffZoneOwner?.TeamId));

        var zone = _faceoffZoneOwner is null
            ? Zone.Neutral
            : _faceoffZoneOwner == winner ? Zone.Defensive : Zone.Offensive;
        GiveTo(winner, zone);
    }

    /// <summary>
    /// Decides whether a skater commits a penalty in this step instead of playing it: the defenders
    /// more when they are being beaten, and either side more when its skaters on the ice are
    /// undisciplined or tired. Returns whether a foul was committed, and whether a penalty shot
    /// awarded for it scored.
    /// </summary>
    private bool TryFoul(out bool scored)
    {
        scored = false;
        if (_delayedAgainst is not null)
        {
            return false;
        }

        var attacker = _possessor;
        var defender = Opponent(attacker);
        var defendingChance = CanPenalize(defender)
            ? MatchTuning.DefendingPenaltyChance
                * Math.Exp(MatchTuning.PenaltyEdgeSensitivity * SkillEdge())
                * IndisciplineFactor(defender)
            : 0;
        var attackingChance = CanPenalize(attacker)
            ? MatchTuning.AttackingPenaltyChance * IndisciplineFactor(attacker)
            : 0;
        if (!_random.Chance(defendingChance + attackingChance))
        {
            return false;
        }

        if (_random.Chance(defendingChance / (defendingChance + attackingChance)))
        {
            var offender = ChooseOffender(defender);
            var onTheRush = _rush && _zone == Zone.Offensive;

            // A foul from behind with the net empty is penalized like any other; awarded goals are
            // not modelled.
            if (onTheRush && !defender.IsGoaliePulled && _random.Chance(MatchTuning.PenaltyShotShare))
            {
                scored = TakePenaltyShot(defender, offender, DrawInfraction(MatchTuning.RushFouls));
                return true;
            }

            var fouls = onTheRush || _zone == Zone.Neutral ? MatchTuning.RushFouls
                : _zone == Zone.Offensive ? MatchTuning.DefendingZoneFouls
                : MatchTuning.ForecheckingFouls;
            CommitFoul(defender, offender, DrawInfraction(fouls));
        }
        else
        {
            var fouls = _zone == Zone.Defensive ? MatchTuning.OwnZoneFoulsWithThePuck : MatchTuning.AttackingFouls;
            CommitFoul(attacker, ChooseOffender(attacker), DrawInfraction(fouls));
        }

        return true;
    }

    /// <summary>
    /// After a hit, the hitter may be penalized for it, the two players may square off in a
    /// scrum, or the toughest of the hit team's skaters may answer with a fight. Fights are likelier
    /// between tough players and in lopsided matches; neither fights nor scrums happen in overtime.
    /// </summary>
    private void AfterHit(MatchSide hitterSide, SkaterState hitter, MatchSide hitSide, SkaterState hitPlayer)
    {
        if (!CanPenalize(hitterSide) || !CanPenalize(hitSide))
        {
            return;
        }

        var overtime = _period > MatchResult.RegulationPeriodCount;
        var responder = hitSide.OnIce.MaxBy(slot => slot.Skater.Toughness).Skater;
        var blowout = Math.Abs(_homeGoals - _awayGoals) >= MatchTuning.BlowoutGoalDifference;

        var illegalHitChance = MatchTuning.IllegalHitChance * Indiscipline(hitter);
        var scrumChance = overtime
            ? 0
            : MatchTuning.ScrumChance * Math.Sqrt(Indiscipline(hitter) * Indiscipline(hitPlayer));
        var fightChance = overtime
            ? 0
            : MatchTuning.FightChance
                * Math.Exp(MatchTuning.FightToughnessSensitivity * (((hitter.Toughness + responder.Toughness) / 2) - MatchTuning.ReferenceRating))
                * (blowout ? MatchTuning.BlowoutFightMultiplier : 1);
        if (!_random.Chance(illegalHitChance + scrumChance + fightChance))
        {
            return;
        }

        switch (_random.NextWeightedIndex([illegalHitChance, scrumChance, fightChance]))
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
                    if (_random.Chance(MatchTuning.ScrumMisconductChance))
                    {
                        scrum.Add(new CalledPenalty(side, player, Infraction.Roughing, PenaltyKind.Misconduct));
                    }
                }

                AssessPenalties(scrum);
                Stoppage(zoneOwner: null);
                break;
            default:
                AssessPenalties(
                [
                    new CalledPenalty(hitterSide, hitter, Infraction.Fighting, PenaltyKind.Major),
                    new CalledPenalty(hitSide, responder, Infraction.Fighting, PenaltyKind.Major),
                ]);
                Stoppage(zoneOwner: null);
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
        var penalties = new List<CalledPenalty> { new(side, offender, infraction, kind) };
        if (kind == PenaltyKind.Major && infraction != Infraction.Fighting)
        {
            penalties.Add(new CalledPenalty(side, offender, infraction, PenaltyKind.GameMisconduct));
        }

        _rush = false;
        if (side == _possessor)
        {
            AssessPenalties(penalties);
            Stoppage(zoneOwner: side);
            return;
        }

        _delayedAgainst = side;
        _delayedPenalties.AddRange(penalties);
        Opponent(side).PullGoalieForDelayedPenalty(true);
        _onIce = null;
    }

    /// <summary>
    /// A foul from behind on a scoring chance: the fouled team's shooter goes alone against the
    /// goalie, like a shootout attempt. Returns whether it scored.
    /// </summary>
    private bool TakePenaltyShot(MatchSide defender, SkaterState offender, Infraction infraction)
    {
        var attacker = _possessor;
        _rush = false;
        AssessPenalties([new CalledPenalty(defender, offender, infraction, PenaltyKind.PenaltyShot)]);

        var shooter = ChooseShooter(attacker, ShotDanger.High).Skater;
        var context = new ShotContext(ShotDanger.High, IsRebound: false, IsRush: false, IsPenaltyShot: true);
        var expectedGoals = ExpectedGoalsModel.ExpectedGoals(context);
        if (_random.Chance(ShootoutPlay.OneOnOneGoalChance(shooter.Player, defender)))
        {
            ScoreGoal(attacker, shooter, context, expectedGoals);
            return true;
        }

        Record(new ShotAttemptEvent(_period, Now, OnIce(), attacker.TeamId, shooter.Id, context, ShotOutcome.Saved, null, expectedGoals));
        Stoppage(zoneOwner: defender);
        return false;
    }

    /// <summary>Ends a delayed penalty: the goalie returns and the penalties still standing are assessed.</summary>
    private void EndDelayedPenalty(List<CalledPenalty> penalties)
    {
        var offenders = _delayedAgainst!;
        _delayedAgainst = null;
        _delayedPenalties.Clear();
        Opponent(offenders).PullGoalieForDelayedPenalty(false);
        _onIce = null;
        AssessPenalties(penalties);
    }

    /// <summary>
    /// Records penalties called at the same moment and sends the players to the box. Penalties on
    /// both teams at once are coincidental and leave neither team short, except that one minor
    /// each at full strength, with no other penalties, plays four-on-four.
    /// </summary>
    private void AssessPenalties(List<CalledPenalty> penalties)
    {
        foreach (var penalty in penalties)
        {
            Record(new PenaltyEvent(_period, Now, OnIce(), penalty.Side.TeamId, penalty.Player.Id, penalty.Infraction, penalty.Kind));
        }

        var coincidental = penalties.Select(penalty => penalty.Side).Distinct().Count() > 1;
        var timed = penalties.Where(penalty => penalty.Kind is PenaltyKind.Minor or PenaltyKind.DoubleMinor or PenaltyKind.Major).ToList();
        var fourOnFour = coincidental
            && !_threeOnThreeBase
            && !_penaltyBox.HasManpowerPenalties
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
                    _penaltyBox.Add(new ServedPenalty(penalty.Side, penalty.Player, penalty.Kind, affectsManpower: !coincidental || fourOnFour));
                    break;
            }
        }

        ApplyManpower(startOfPeriod: false);
    }

    private void ExpirePenalties()
    {
        if (_penaltyBox.ExpirePenalties())
        {
            ApplyManpower(startOfPeriod: false);
        }
    }

    /// <summary>
    /// Puts both teams in the strength state the penalties being served call for, and counts a
    /// power-play opportunity for each penalty the first time it gives the other team the advantage.
    /// </summary>
    private void ApplyManpower(bool startOfPeriod)
    {
        var home = Manpower(_home);
        var away = Manpower(_away);
        _home.SetStrength(home, away, startOfPeriod);
        _away.SetStrength(away, home, startOfPeriod);
        CountPowerPlay(_home, home > away);
        CountPowerPlay(_away, away > home);
        _onIce = null;
    }

    private void CountPowerPlay(MatchSide side, bool hasAdvantage)
    {
        if (!hasAdvantage)
        {
            return;
        }

        foreach (var penalty in _penaltyBox.RunningManpowerPenalties(Opponent(side)))
        {
            if (!penalty.CountedAsPowerPlay)
            {
                penalty.CountedAsPowerPlay = true;
                side.AddPowerPlayOpportunity();
            }
        }
    }

    /// <summary>
    /// The skaters the penalties allow a side, not counting an extra attacker. Each penalty being
    /// served takes one away in regulation; in three-on-three overtime it gives the other team one
    /// more instead, so a team never has fewer than three.
    /// </summary>
    private int Manpower(MatchSide side)
    {
        var shorthanded = _penaltyBox.ShorthandedBy(side);
        if (!_threeOnThreeBase)
        {
            return 5 - shorthanded;
        }

        return 3 + Math.Max(0, _penaltyBox.ShorthandedBy(Opponent(side)) - shorthanded);
    }

    /// <summary>A team down to its last few available skaters is not penalized further.</summary>
    private static bool CanPenalize(MatchSide side) => side.AvailableSkaterCount >= MatchTuning.MinimumAvailableSkaters;

    /// <summary>Above one when a side's skaters on the ice are less disciplined than the reference, or tired.</summary>
    private static double IndisciplineFactor(MatchSide side) =>
        Math.Exp(-MatchTuning.DisciplineSensitivity * (side.MeanOnIce(slot => slot.Skater.Discipline) - MatchTuning.ReferenceRating));

    private static double Indiscipline(SkaterState skater) =>
        Math.Exp(-MatchTuning.DisciplineSensitivity * ((skater.Discipline * skater.Performance) - MatchTuning.ReferenceRating));

    private SkaterState ChooseOffender(MatchSide side) =>
        Choose(side, slot => MatchTuning.MinimumIndisciplineWeight + 100 - (slot.Skater.Discipline * slot.Skater.Performance));

    private Infraction DrawInfraction((Infraction Infraction, double Weight)[] fouls)
    {
        var weights = new double[fouls.Length];
        for (var index = 0; index < fouls.Length; index++)
        {
            weights[index] = fouls[index].Weight;
        }

        return fouls[_random.NextWeightedIndex(weights)].Infraction;
    }

    private PenaltyKind DrawKind(Infraction infraction)
    {
        var majorChance = infraction switch
        {
            Infraction.Boarding or Infraction.Charging or Infraction.Elbowing => MatchTuning.DangerousHitMajorChance,
            Infraction.CrossChecking or Infraction.Slashing or Infraction.HighSticking => MatchTuning.StickFoulMajorChance,
            _ => 0,
        };
        if (majorChance > 0 && _random.Chance(majorChance))
        {
            return PenaltyKind.Major;
        }

        return infraction == Infraction.HighSticking && _random.Chance(MatchTuning.DoubleMinorHighStickingChance)
            ? PenaltyKind.DoubleMinor
            : PenaltyKind.Minor;
    }

    private ShotDanger DrawDanger(bool rush)
    {
        if (rush)
        {
            return (ShotDanger)_random.NextWeightedIndex(
            [
                MatchTuning.RushLowDangerWeight,
                MatchTuning.RushMediumDangerWeight,
                MatchTuning.RushHighDangerWeight * (_openIce ? MatchTuning.OpenIceHighDangerMultiplier : 1),
            ]);
        }

        // Better attackers get to better ice; better defenders keep them to the outside.
        var edge = Math.Exp(MatchTuning.DangerEdgeSensitivity * AttackingEdge());
        return (ShotDanger)_random.NextWeightedIndex(
        [
            MatchTuning.LowDangerWeight / edge,
            MatchTuning.MediumDangerWeight,
            MatchTuning.HighDangerWeight * edge * (_openIce ? MatchTuning.OpenIceHighDangerMultiplier : 1),
        ]);
    }

    private OnIceSkater ChooseShooter(MatchSide attacker, ShotDanger danger) =>
        ChooseSlot(attacker, slot => ShooterRoleWeight(slot.IsDefence, danger) * (0.5 + (slot.Offence / 100)));

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

    /// <summary>
    /// The attacking skaters' offence less the defending skaters' defence, in rating points, as
    /// they perform at their current energy.
    /// </summary>
    /// <remarks>Each extra skater the attackers have on the ice adds to their edge, and each one fewer takes from it.</remarks>
    private double AttackingEdge() =>
        SkillEdge() + (MatchTuning.ManpowerEdgePerSkater * (_possessor.OnIce.Count - Opponent(_possessor).OnIce.Count));

    /// <summary>The attacking edge from the skaters' ratings alone, whatever the strength state.</summary>
    private double SkillEdge() =>
        _possessor.MeanOnIce(slot => slot.Offence) - Opponent(_possessor).MeanOnIce(slot => slot.Defence);

    /// <summary>Above one when the attackers are stronger, below one when the defenders are.</summary>
    private double AttackingEdgeFactor() => Math.Exp(MatchTuning.PlayEdgeSensitivity * AttackingEdge());

    /// <summary>More physical defending skaters throw more hits.</summary>
    private double HitRateFactor() =>
        Math.Exp(MatchTuning.HitRateSensitivity
            * (Opponent(_possessor).MeanOnIce(slot => slot.Skater.Physicality) - MatchTuning.ReferenceRating))
        * (_openIce ? MatchTuning.OpenIceHitMultiplier : 1);

    private SkaterState Choose(MatchSide side, Func<OnIceSkater, double> weight) => ChooseSlot(side, weight).Skater;

    private OnIceSkater ChooseSlot(MatchSide side, Func<OnIceSkater, double> weight)
    {
        var onIce = side.OnIce;
        var weights = new double[onIce.Count];
        for (var index = 0; index < onIce.Count; index++)
        {
            weights[index] = Math.Max(0, weight(onIce[index]));
        }

        return onIce[_random.NextWeightedIndex(weights)];
    }

    private void GiveTo(MatchSide side, Zone zone)
    {
        // The offenders touching the puck stops play for a delayed penalty.
        if (side == _delayedAgainst)
        {
            Stoppage(zoneOwner: side);
            return;
        }

        _possessor = side;
        _zone = zone;
        _rush = false;
        _rebound = false;
    }

    private void Stoppage(MatchSide? zoneOwner)
    {
        // A stoppage during a delayed penalty calls it, with the faceoff in the offenders' zone.
        if (_delayedAgainst is { } offenders)
        {
            zoneOwner = offenders;
            EndDelayedPenalty(_delayedPenalties.ToList());
        }

        _faceoffPending = true;
        _faceoffZoneOwner = zoneOwner;
        _rush = false;
        _rebound = false;
    }

    private MatchSide Opponent(MatchSide side) => side == _home ? _away : _home;

    private TimeSpan Now => TimeSpan.FromSeconds(_clock);

    private OnIcePlayers OnIce() => _onIce ??= new OnIcePlayers(
        _home.OnIce.Select(slot => slot.Skater.Id),
        _home.IsGoaliePulled ? null : _home.Goalie.Id,
        _away.OnIce.Select(slot => slot.Skater.Id),
        _away.IsGoaliePulled ? null : _away.Goalie.Id);

    private void Record(MatchEvent matchEvent) => _events.Add(matchEvent);

    private MatchResult CreateResult(MatchDecision decision, ShootoutResult? shootout)
    {
        // The shootout winner is credited a single deciding goal that no player scored.
        var homeBonus = shootout?.WinnerId == _home.TeamId ? 1 : 0;
        var awayBonus = shootout?.WinnerId == _away.TeamId ? 1 : 0;
        var playingTime = TimeSpan.FromSeconds(_playingSeconds);
        var statistics = new MatchStatisticsBuilder(_events, _home.TeamId, _away.TeamId);

        return new MatchResult(
            statistics.TeamResult(_home, _away, _homeGoals + homeBonus),
            statistics.TeamResult(_away, _home, _awayGoals + awayBonus),
            decision,
            _events,
            playingTime,
            shootout,
            _random.State);
    }

    /// <summary>A penalty called on a skater, before it is assessed.</summary>
    private sealed record CalledPenalty(MatchSide Side, SkaterState Player, Infraction Infraction, PenaltyKind Kind);
}