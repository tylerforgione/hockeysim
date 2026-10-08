using HockeySim.Domain;
using HockeySim.Simulation.Events;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Simulation.Play;

/// <summary>
/// Plays one match. Play runs on a game clock in whole seconds as a sequence of possession steps:
/// in each step the team with the puck tries to move it up the ice and create a shot, and the
/// defending team tries to win it back. Line changes happen on the fly and at stoppages, and every
/// faceoff, shot attempt, goal, hit, takeaway, and giveaway is recorded in the play-by-play with
/// the players on the ice.
/// </summary>
/// <remarks>
/// This class runs the periods and the possession steps through the zones and coordinates the
/// parts that play the rest, all sharing one <see cref="MatchState"/>: <see cref="ShotPlay"/>
/// (shots and goals), <see cref="FoulPlay"/> (fouls, scrums, and fights), <see cref="PenaltyAssessment"/>
/// (the penalty box and manpower), <see cref="GoaliePulls"/> (pulling the goalie late to tie the
/// match), <see cref="InjuryPlay"/> (injuries from contacts and strains), <see cref="Possession"/>
/// (puck changes, stoppages, and faceoffs), and <see cref="SkillComparison"/>.
/// </remarks>
internal sealed class MatchPlay
{
    private readonly OvertimeFormat _overtime;
    private readonly MatchState _state;
    private readonly MatchInjuries _injuries;
    private readonly SkillComparison _skill;
    private readonly GoaliePulls _goaliePulls;
    private readonly InjuryPlay _injuryPlay;
    private readonly PenaltyAssessment _penalties;
    private readonly Possession _possession;
    private readonly ShotPlay _shots;
    private readonly FoulPlay _fouls;

    public MatchPlay(Match match, OvertimeFormat overtime, MatchHealth health, RandomState randomState)
    {
        var random = new ControlledRandom(randomState);
        var penaltyBox = new PenaltyBox();
        _overtime = overtime;
        _injuries = new MatchInjuries(health, random);
        _state = new MatchState(
            new MatchSide(match.Home, penaltyBox, health),
            new MatchSide(match.Away, penaltyBox, health),
            penaltyBox,
            random);
        _skill = new SkillComparison(_state);
        _goaliePulls = new GoaliePulls(_state);
        _injuryPlay = new InjuryPlay(_state, _injuries);
        _penalties = new PenaltyAssessment(_state);
        _possession = new Possession(_state, _penalties);
        _shots = new ShotPlay(_state, _possession, _penalties, _goaliePulls, _injuryPlay, _skill);
        _fouls = new FoulPlay(_state, _possession, _penalties, _shots, _injuryPlay, _skill);
    }

    public MatchResult Play()
    {
        for (var period = 1; period <= MatchResult.RegulationPeriodCount; period++)
        {
            PlayPeriod(period, MatchTuning.RegulationPeriodSeconds, threeOnThree: false, suddenDeath: false);
        }

        if (_state.HomeGoals != _state.AwayGoals)
        {
            return CreateResult(MatchDecision.Regulation, shootout: null);
        }

        if (_overtime == OvertimeFormat.Playoff)
        {
            // Sudden-death periods continue until someone scores, however long that takes.
            for (var period = MatchResult.OvertimePeriod; _state.HomeGoals == _state.AwayGoals; period++)
            {
                PlayPeriod(period, MatchTuning.PlayoffOvertimePeriodSeconds, threeOnThree: false, suddenDeath: true);
            }

            return CreateResult(MatchDecision.Overtime, shootout: null);
        }

        PlayPeriod(MatchResult.OvertimePeriod, MatchTuning.RegularSeasonOvertimeSeconds, threeOnThree: true, suddenDeath: true);
        if (_state.HomeGoals != _state.AwayGoals)
        {
            return CreateResult(MatchDecision.Overtime, shootout: null);
        }

        return CreateResult(MatchDecision.Shootout, new ShootoutPlay(_state.Home, _state.Away, _state.Random).Play());
    }

    private void PlayPeriod(int period, int periodSeconds, bool threeOnThree, bool suddenDeath)
    {
        _state.StartPeriod(period);
        _state.OpenIce = threeOnThree;
        _state.ThreeOnThreeBase = threeOnThree;
        if (period > 1)
        {
            _state.Home.RestForIntermission();
            _state.Away.RestForIntermission();
        }

        _penalties.ApplyManpower(startOfPeriod: true);
        _possession.Stoppage(zoneOwner: null);

        while (true)
        {
            _goaliePulls.PullGoaliesToTieTheMatch(periodSeconds);
            if (_state.FaceoffPending)
            {
                ChangeLines(side => side.ChangeAtStoppage());
                _possession.TakeFaceoff();
            }
            else
            {
                // A team keeps its players out while it is attacking, and nobody changes in the
                // middle of a rebound scramble.
                ChangeLines(side => !_state.Rebound && (side != _state.Possessor || _state.Zone != Zone.Offensive) && side.ChangeOnTheFly());
            }

            var seconds = StepSeconds();

            // A penalty that ends during the step ends on time, and play carries on from there.
            if (_state.PenaltyBox.SecondsUntilNextExpiry is { } expiry && expiry <= Math.Min(seconds, periodSeconds - _state.Clock))
            {
                _state.Elapse(expiry);
                _penalties.ExpirePenalties();
                if (_state.Clock == periodSeconds)
                {
                    EndPeriod();
                    return;
                }

                continue;
            }

            if (_state.Clock + seconds >= periodSeconds)
            {
                _state.Elapse(periodSeconds - _state.Clock);
                EndPeriod();
                return;
            }

            _state.Elapse(seconds);
            _injuryPlay.Strain(seconds);
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
        if (_state.DelayedAgainst is not null)
        {
            _penalties.EndDelayedPenalty(_state.DelayedPenalties.ToList());
        }

        _goaliePulls.ReturnGoaliesPulledToTieTheMatch();
    }

    private void ChangeLines(Func<MatchSide, bool> change)
    {
        // Evaluate both sides; a change by either replaces the players on the ice.
        var homeChanged = change(_state.Home);
        var awayChanged = change(_state.Away);
        if (homeChanged || awayChanged)
        {
            _state.ForgetOnIce();
        }
    }

    private int StepSeconds()
    {
        var random = _state.Random;
        if (_state.Rebound)
        {
            return random.NextInt(1, MatchTuning.ReboundStepMaximum + 1);
        }

        return _state.Zone switch
        {
            Zone.Defensive => random.NextInt(MatchTuning.DefensiveZoneStepMinimum, MatchTuning.DefensiveZoneStepMaximum + 1),
            Zone.Neutral => random.NextInt(MatchTuning.NeutralZoneStepMinimum, MatchTuning.NeutralZoneStepMaximum + 1),
            _ => random.NextInt(MatchTuning.OffensiveZoneStepMinimum, MatchTuning.OffensiveZoneStepMaximum + 1),
        };
    }

    /// <summary>Plays one step of possession. Returns whether a goal was scored.</summary>
    private bool PlayStep()
    {
        if (_state.Rebound)
        {
            return PlayRebound();
        }

        if (_fouls.TryFoul(out var scored))
        {
            return scored;
        }

        // Facing an empty net, a team that has the puck short of the attacking zone may shoot for
        // it from distance.
        if (_state.Zone != Zone.Offensive
            && _state.Opponent(_state.Possessor).IsGoaliePulled
            && _state.Random.Chance(MatchTuning.LongEmptyNetShotChance))
        {
            return _shots.Shoot(rush: false, isRebound: false, fromDistance: true);
        }

        return _state.Zone switch
        {
            Zone.Defensive => PlayDefensiveZone(),
            Zone.Neutral => PlayNeutralZone(),
            _ => PlayOffensiveZone(),
        };
    }

    private bool PlayDefensiveZone()
    {
        var edge = _skill.AttackingEdgeFactor();
        switch (_state.Random.NextWeightedIndex(
        [
            MatchTuning.DefensiveZoneExitWeight * edge,
            MatchTuning.DefensiveZoneTurnoverWeight / edge,
            MatchTuning.DefensiveZoneHitWeight * _skill.HitRateFactor(),
            MatchTuning.IcingWeight,
            MatchTuning.DefensiveZoneHoldWeight,
        ]))
        {
            case 0:
                _state.Zone = Zone.Neutral;
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
                var possessor = _state.Possessor;
                if (_penalties.Manpower(possessor) < _penalties.Manpower(_state.Opponent(possessor)))
                {
                    _possession.GiveTo(_state.Opponent(possessor), Zone.Defensive);
                }
                else
                {
                    _possession.Stoppage(zoneOwner: possessor);
                }

                break;
        }

        return false;
    }

    private bool PlayNeutralZone()
    {
        var edge = _skill.AttackingEdgeFactor();
        switch (_state.Random.NextWeightedIndex(
        [
            MatchTuning.CarryInWeight * edge * (_state.OpenIce ? MatchTuning.OpenIceCarryInMultiplier : 1),
            MatchTuning.DumpInWeight,
            MatchTuning.NeutralZoneTurnoverWeight / edge,
            MatchTuning.NeutralZoneHitWeight * _skill.HitRateFactor(),
            MatchTuning.OffsideWeight,
            MatchTuning.RegroupWeight,
        ]))
        {
            case 0:
                _state.Zone = Zone.Offensive;
                _state.Rush = true;
                break;
            case 1:
                if (_state.Random.Chance(MatchTuning.DumpInRecoveryChance))
                {
                    _state.Zone = Zone.Offensive;
                }
                else
                {
                    _possession.GiveTo(_state.Opponent(_state.Possessor), Zone.Defensive);
                }

                break;
            case 2:
                Turnover(Zone.Neutral);
                break;
            case 3:
                Hit(Zone.Neutral);
                break;
            case 4:
                _possession.Stoppage(zoneOwner: null);
                break;
            default:
                _state.Zone = Zone.Defensive;
                break;
        }

        return false;
    }

    private bool PlayOffensiveZone()
    {
        var rush = _state.Rush;
        _state.Rush = false;
        var edge = _skill.AttackingEdgeFactor();
        switch (_state.Random.NextWeightedIndex(
        [
            MatchTuning.ShotAttemptWeight * edge
                * (rush ? MatchTuning.RushShotMultiplier : 1)
                * (_state.OpenIce ? MatchTuning.OpenIceShotMultiplier : 1),
            MatchTuning.OffensiveZoneTurnoverWeight / edge,
            MatchTuning.OffensiveZoneHitWeight * _skill.HitRateFactor(),
            MatchTuning.ClearedWeight,
            MatchTuning.OffensiveZoneStoppageWeight,
            MatchTuning.CycleWeight,
        ]))
        {
            case 0:
                return _shots.Shoot(rush, isRebound: false, fromDistance: false);
            case 1:
                Turnover(Zone.Defensive);
                break;
            case 2:
                Hit(Zone.Defensive);
                break;
            case 3:
                _possession.GiveTo(_state.Opponent(_state.Possessor), Zone.Defensive);
                break;
            case 4:
                _possession.Stoppage(zoneOwner: _state.Opponent(_state.Possessor));
                break;
        }

        return false;
    }

    private bool PlayRebound()
    {
        _state.Rebound = false;
        if (_state.Random.Chance(MatchTuning.ReboundShotChance))
        {
            return _shots.Shoot(rush: false, isRebound: true, fromDistance: false);
        }

        // Nobody got a stick on it; the scramble goes either way.
        if (_state.Random.Chance(MatchTuning.ReboundScrambleRecoveryChance))
        {
            _possession.GiveTo(_state.Opponent(_state.Possessor), Zone.Defensive);
        }

        return false;
    }

    private void Turnover(Zone opponentZone)
    {
        var attacker = _state.Possessor;
        var defender = _state.Opponent(attacker);
        switch (_state.Random.NextWeightedIndex(
        [
            MatchTuning.TakeawayShare,
            MatchTuning.GiveawayShare,
            1 - MatchTuning.TakeawayShare - MatchTuning.GiveawayShare,
        ]))
        {
            case 0:
                var taker = _state.Choose(defender, slot => 0.5 + (slot.Skater.StickChecking / 100));
                _state.Record(new TakeawayEvent(_state.Period, _state.Now, _state.OnIce(), defender.TeamId, taker.Id));
                break;
            case 1:
                var carrier = _state.Choose(attacker, slot => 1.5 - (slot.Skater.PuckControl / 100));
                _state.Record(new GiveawayEvent(_state.Period, _state.Now, _state.OnIce(), attacker.TeamId, carrier.Id));
                break;
        }

        _possession.GiveTo(defender, opponentZone);
    }

    private void Hit(Zone opponentZoneIfTurnedOver)
    {
        var attacker = _state.Possessor;
        var defender = _state.Opponent(attacker);
        var hitter = _state.Choose(defender, slot =>
            (slot.IsDefence ? MatchTuning.DefenceHitterWeight : MatchTuning.ForwardHitterWeight)
            * Math.Max(0.1, 0.5 + (slot.Skater.Physicality / 100)));
        var carrierSlot = attacker.OnIce[_state.Random.NextInt(0, attacker.OnIce.Count)];
        var carrier = carrierSlot.Skater;
        _state.Record(new HitEvent(_state.Period, _state.Now, _state.OnIce(), defender.TeamId, hitter.Id, carrier.Id));

        var turnoverChance = Probability.Adjust(
            MatchTuning.HitTurnoverChance,
            MatchTuning.HitTurnoverSensitivity
            * ((hitter.Physicality * hitter.Performance) - (carrierSlot.PuckProtection * carrier.Performance)));
        if (_state.Random.Chance(turnoverChance))
        {
            _possession.GiveTo(defender, opponentZoneIfTurnedOver);
        }

        if (_state.DelayedAgainst is null && !_state.FaceoffPending)
        {
            _fouls.AfterHit(defender, hitter, attacker, carrier);
        }

        _injuryPlay.Contact(attacker, carrier, InjuryCause.Hit);
        _injuryPlay.Contact(defender, hitter, InjuryCause.Collision);
    }

    private MatchResult CreateResult(MatchDecision decision, ShootoutResult? shootout)
    {
        var home = _state.Home;
        var away = _state.Away;

        // The shootout winner is credited a single deciding goal that no player scored.
        var homeBonus = shootout?.WinnerId == home.TeamId ? 1 : 0;
        var awayBonus = shootout?.WinnerId == away.TeamId ? 1 : 0;
        var playingTime = TimeSpan.FromSeconds(_state.PlayingSeconds);
        var statistics = new MatchStatisticsBuilder(_state.Events, home.TeamId, away.TeamId);

        return new MatchResult(
            statistics.TeamResult(home, away, _state.HomeGoals + homeBonus),
            statistics.TeamResult(away, home, _state.AwayGoals + awayBonus),
            decision,
            _state.Events,
            playingTime,
            shootout,
            _injuries.Wear,
            _state.Random.State);
    }
}