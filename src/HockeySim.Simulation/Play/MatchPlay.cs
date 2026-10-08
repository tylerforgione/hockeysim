using HockeySim.Domain;
using HockeySim.Simulation.Events;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Simulation.Play;

/// <summary>
/// Plays one match. Play runs on a game clock in whole seconds as a sequence of possession steps,
/// with line changes on the fly and at stoppages, period by period and into overtime or a shootout.
/// Every faceoff, shot attempt, goal, hit, takeaway, giveaway, penalty, and injury is recorded in
/// the play-by-play with the players on the ice.
/// </summary>
/// <remarks>
/// This class runs the periods, the clock, and line changes, and coordinates the parts that play
/// the rest, all sharing one <see cref="MatchState"/>: <see cref="ZonePlay"/> (each step of
/// possession), <see cref="ShotPlay"/> (shots and goals), <see cref="FoulPlay"/> (fouls, scrums,
/// and fights), <see cref="PenaltyAssessment"/> (the penalty box and manpower),
/// <see cref="GoaliePulls"/> (pulling the goalie late to tie the match), <see cref="InjuryPlay"/>
/// (injuries from contacts and strains), <see cref="Possession"/> (puck changes, stoppages, and
/// faceoffs), and <see cref="SkillComparison"/>.
/// </remarks>
internal sealed class MatchPlay
{
    private readonly OvertimeFormat _overtime;
    private readonly MatchState _state;
    private readonly MatchInjuries _injuries;
    private readonly GoaliePulls _goaliePulls;
    private readonly InjuryPlay _injuryPlay;
    private readonly PenaltyAssessment _penalties;
    private readonly Possession _possession;
    private readonly ZonePlay _zonePlay;

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
        var skill = new SkillComparison(_state);
        _goaliePulls = new GoaliePulls(_state);
        _injuryPlay = new InjuryPlay(_state, _injuries);
        _penalties = new PenaltyAssessment(_state);
        _possession = new Possession(_state, _penalties);
        var shots = new ShotPlay(_state, _possession, _penalties, _goaliePulls, _injuryPlay, skill);
        var fouls = new FoulPlay(_state, _possession, _penalties, shots, _injuryPlay, skill);
        _zonePlay = new ZonePlay(_state, _possession, _penalties, shots, fouls, _injuryPlay, skill);
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
            if (_zonePlay.PlayStep() && suddenDeath)
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