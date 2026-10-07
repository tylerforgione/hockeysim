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
internal sealed class MatchPlay
{
    private readonly ControlledRandom _random;
    private readonly OvertimeFormat _overtime;
    private readonly MatchSide _home;
    private readonly MatchSide _away;
    private readonly List<MatchEvent> _events = [];

    private OnIcePlayers? _onIce;
    private int _period;
    private int _clock;
    private int _playingSeconds;
    private bool _openIce;
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

    public MatchPlay(Match match, OvertimeFormat overtime, RandomState randomState)
    {
        _random = new ControlledRandom(randomState);
        _overtime = overtime;
        _home = new MatchSide(match.Home);
        _away = new MatchSide(match.Away);
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
        _home.StartPeriod(threeOnThree, afterIntermission: period > 1);
        _away.StartPeriod(threeOnThree, afterIntermission: period > 1);
        _onIce = null;
        Stoppage(zoneOwner: null);

        while (true)
        {
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
            if (_clock + seconds >= periodSeconds)
            {
                Elapse(periodSeconds - _clock);
                return;
            }

            Elapse(seconds);
            if (PlayStep() && suddenDeath)
            {
                return;
            }
        }
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
                Stoppage(zoneOwner: _possessor);
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
                return Shoot(rush, isRebound: false);
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
            return Shoot(rush: false, isRebound: true);
        }

        // Nobody got a stick on it; the scramble goes either way.
        if (_random.Chance(MatchTuning.ReboundScrambleRecoveryChance))
        {
            GiveTo(Opponent(_possessor), Zone.Defensive);
        }

        return false;
    }

    private bool Shoot(bool rush, bool isRebound)
    {
        var attacker = _possessor;
        var defender = Opponent(attacker);
        var context = isRebound
            ? new ShotContext(ShotDanger.High, IsRebound: true, IsRush: false)
            : new ShotContext(DrawDanger(rush), IsRebound: false, IsRush: rush);
        var shooter = ChooseShooter(attacker, context.Danger);

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

        var expectedGoals = ExpectedGoalsModel.ExpectedGoals(context);
        var onNetBase = ExpectedGoalsModel.OnNetChance(context);
        var onNetChance = Probability.Adjust(
            onNetBase,
            MatchTuning.AccuracySensitivity * ((shooter.Accuracy * shooter.Performance) - MatchTuning.ReferenceRating));
        if (!_random.Chance(onNetChance))
        {
            Record(new ShotAttemptEvent(_period, Now, OnIce(), attacker.TeamId, shooter.Id, context, ShotOutcome.Missed, null, expectedGoals));
            if (_random.Chance(MatchTuning.MissedShotStoppageChance))
            {
                Stoppage(zoneOwner: defender);
            }
            else if (_random.Chance(MatchTuning.MissedShotRecoveryChance))
            {
                GiveTo(defender, Zone.Defensive);
            }

            return false;
        }

        // Dividing by the reference on-net chance makes a reference shooter against a reference
        // goalie score exactly at the expected-goal rate.
        var goalChance = Probability.Adjust(
            expectedGoals / onNetBase,
            (MatchTuning.FinishingSensitivity * ((shooter.Finishing * shooter.Performance) - MatchTuning.ReferenceRating))
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

    private void ScoreGoal(MatchSide attacker, SkaterState scorer, ShotContext context, double expectedGoals)
    {
        PlayerId? primary = null;
        PlayerId? secondary = null;
        if (_random.Chance(MatchTuning.PrimaryAssistChance))
        {
            var primaryAssist = ChooseAssist(attacker, scorer, excluded: null);
            primary = primaryAssist.Id;
            if (_random.Chance(MatchTuning.SecondaryAssistChance))
            {
                secondary = ChooseAssist(attacker, scorer, primaryAssist).Id;
            }
        }

        Record(new GoalEvent(_period, Now, OnIce(), attacker.TeamId, scorer.Id, primary, secondary, context, expectedGoals));
        if (attacker == _home)
        {
            _homeGoals++;
        }
        else
        {
            _awayGoals++;
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
        var carrier = attacker.OnIce[_random.NextInt(0, attacker.OnIce.Count)].Skater;
        Record(new HitEvent(_period, Now, OnIce(), defender.TeamId, hitter.Id, carrier.Id));

        var turnoverChance = Probability.Adjust(
            MatchTuning.HitTurnoverChance,
            MatchTuning.HitTurnoverSensitivity
            * ((hitter.Physicality * hitter.Performance) - (carrier.PuckProtection * carrier.Performance)));
        if (_random.Chance(turnoverChance))
        {
            GiveTo(defender, opponentZoneIfTurnedOver);
        }
    }

    private void TakeFaceoff()
    {
        _faceoffPending = false;
        var home = _home.Centre.Skater;
        var away = _away.Centre.Skater;
        var homeWins = _random.Chance(Probability.Adjust(0.5, MatchTuning.FaceoffSensitivity * (home.Faceoffs - away.Faceoffs)));
        var (winner, winnerCentre, loserCentre) = homeWins ? (_home, home, away) : (_away, away, home);
        Record(new FaceoffEvent(_period, Now, OnIce(), winner.TeamId, winnerCentre.Id, loserCentre.Id));

        var zone = _faceoffZoneOwner is null
            ? Zone.Neutral
            : _faceoffZoneOwner == winner ? Zone.Defensive : Zone.Offensive;
        GiveTo(winner, zone);
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

    private SkaterState ChooseShooter(MatchSide attacker, ShotDanger danger) =>
        Choose(attacker, slot => ShooterRoleWeight(slot.IsDefence, danger) * (0.5 + (slot.Skater.Offence / 100)));

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
    private double AttackingEdge() =>
        _possessor.MeanOnIce(skater => skater.Offence) - Opponent(_possessor).MeanOnIce(skater => skater.Defence);

    /// <summary>Above one when the attackers are stronger, below one when the defenders are.</summary>
    private double AttackingEdgeFactor() => Math.Exp(MatchTuning.PlayEdgeSensitivity * AttackingEdge());

    /// <summary>More physical defending skaters throw more hits.</summary>
    private double HitRateFactor() =>
        Math.Exp(MatchTuning.HitRateSensitivity
            * (Opponent(_possessor).MeanOnIce(skater => skater.Physicality) - MatchTuning.ReferenceRating))
        * (_openIce ? MatchTuning.OpenIceHitMultiplier : 1);

    private SkaterState Choose(MatchSide side, Func<OnIceSkater, double> weight)
    {
        var onIce = side.OnIce;
        var weights = new double[onIce.Count];
        for (var index = 0; index < onIce.Count; index++)
        {
            weights[index] = Math.Max(0, weight(onIce[index]));
        }

        return onIce[_random.NextWeightedIndex(weights)].Skater;
    }

    private void GiveTo(MatchSide side, Zone zone)
    {
        _possessor = side;
        _zone = zone;
        _rush = false;
        _rebound = false;
    }

    private void Stoppage(MatchSide? zoneOwner)
    {
        _faceoffPending = true;
        _faceoffZoneOwner = zoneOwner;
        _rush = false;
        _rebound = false;
    }

    private MatchSide Opponent(MatchSide side) => side == _home ? _away : _home;

    private TimeSpan Now => TimeSpan.FromSeconds(_clock);

    private OnIcePlayers OnIce() => _onIce ??= new OnIcePlayers(
        _home.OnIce.Select(slot => slot.Skater.Id),
        _home.Goalie.Id,
        _away.OnIce.Select(slot => slot.Skater.Id),
        _away.Goalie.Id);

    private void Record(MatchEvent matchEvent) => _events.Add(matchEvent);

    private MatchResult CreateResult(MatchDecision decision, ShootoutResult? shootout)
    {
        // The shootout winner is credited a single deciding goal that no player scored.
        var homeBonus = shootout?.WinnerId == _home.TeamId ? 1 : 0;
        var awayBonus = shootout?.WinnerId == _away.TeamId ? 1 : 0;
        var playingTime = TimeSpan.FromSeconds(_playingSeconds);
        var statistics = new MatchStatisticsBuilder(_events, _home.TeamId);

        return new MatchResult(
            statistics.TeamResult(_home, _away, _homeGoals + homeBonus, playingTime),
            statistics.TeamResult(_away, _home, _awayGoals + awayBonus, playingTime),
            decision,
            _events,
            playingTime,
            shootout,
            _random.State);
    }
}