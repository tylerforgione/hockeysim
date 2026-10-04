using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Simulation;

/// <summary>
/// Simulates a match statistically from both teams' lineups at match start.
/// </summary>
/// <remarks>
/// The simulator reads the supplied teams but never changes them. All randomness comes from the
/// supplied <see cref="RandomState"/>, so the same teams and state always produce the same
/// result within an engine version.
/// </remarks>
public sealed class MatchSimulator
{
    public MatchResult Simulate(Match match, RandomState randomState)
    {
        ArgumentNullException.ThrowIfNull(match);

        return new MatchPlay(match, randomState).Play();
    }

    /// <summary>
    /// The transient state of one match being played. Play is divided into fixed-length ticks;
    /// in each tick both teams send out a weighted-random unit and get one chance to shoot.
    /// </summary>
    private sealed class MatchPlay(Match match, RandomState randomState)
    {
        private const int TickSeconds = 30;
        private const int RegulationPeriodSeconds = 20 * 60;
        private const int OvertimeSeconds = 5 * 60;
        private const int ShootoutRounds = 3;

        // Tuned so evenly matched teams average about 30 shots and 3 goals each in regulation.
        private const double BaseShotChancePerTick = 0.25;
        private const double BaseGoalChancePerShot = 0.095;
        private const double BaseShootoutGoalChance = 0.32;

        // Change in log-odds per rating point of difference between the opposing strengths.
        private const double ShotStrengthSensitivity = 0.05;
        private const double FinishingSensitivity = 0.04;

        // Overtime is played with open ice, so chances come much more often than in regulation.
        private const double OvertimeShotRateMultiplier = 2.0;

        // Keep every event possible but never certain, however lopsided the ratings.
        private const double MinimumChance = 0.01;
        private const double MaximumChance = 0.99;
        private const double MinimumShootoutChance = 0.05;
        private const double MaximumShootoutChance = 0.95;

        private readonly ControlledRandom _random = new(randomState);
        private readonly MatchSide _home = new(match.Home);
        private readonly MatchSide _away = new(match.Away);
        private readonly List<GoalEvent> _goals = [];

        public MatchResult Play()
        {
            for (var period = 1; period <= MatchResult.RegulationPeriodCount; period++)
            {
                PlayPeriod(period, RegulationPeriodSeconds, shotRateMultiplier: 1.0, suddenDeath: false);
            }

            if (_home.Goals != _away.Goals)
            {
                return CreateResult(MatchDecision.Regulation, shootout: null);
            }

            PlayPeriod(MatchResult.OvertimePeriod, OvertimeSeconds, OvertimeShotRateMultiplier, suddenDeath: true);

            if (_home.Goals != _away.Goals)
            {
                return CreateResult(MatchDecision.Overtime, shootout: null);
            }

            return CreateResult(MatchDecision.Shootout, PlayShootout());
        }

        private void PlayPeriod(int period, int periodSeconds, double shotRateMultiplier, bool suddenDeath)
        {
            for (var tickStart = 0; tickStart < periodSeconds; tickStart += TickSeconds)
            {
                var homeUnit = _home.SelectUnit(_random);
                var awayUnit = _away.SelectUnit(_random);

                // Choose at random who attacks first so neither side gets a systematic
                // first chance in sudden death. Each attacker uses its own half of the tick,
                // which keeps goal times in chronological order.
                var homeFirst = _random.Chance(0.5);
                var (first, firstUnit, second, secondUnit) = homeFirst
                    ? (_home, homeUnit, _away, awayUnit)
                    : (_away, awayUnit, _home, homeUnit);

                var halfTick = TickSeconds / 2;
                var scored = AttemptShot(first, firstUnit, second, secondUnit, period, tickStart, halfTick, shotRateMultiplier);
                if (scored && suddenDeath)
                {
                    return;
                }

                scored = AttemptShot(second, secondUnit, first, firstUnit, period, tickStart + halfTick, halfTick, shotRateMultiplier);
                if (scored && suddenDeath)
                {
                    return;
                }
            }
        }

        private bool AttemptShot(
            MatchSide attacker,
            OnIceUnit attackingUnit,
            MatchSide defender,
            OnIceUnit defendingUnit,
            int period,
            int windowStartSeconds,
            int windowSeconds,
            double shotRateMultiplier)
        {
            var shotChance = Math.Clamp(
                shotRateMultiplier * Probability(
                    BaseShotChancePerTick,
                    ShotStrengthSensitivity * (attackingUnit.Offence - defendingUnit.Defence)),
                MinimumChance,
                MaximumChance);

            if (!_random.Chance(shotChance))
            {
                return false;
            }

            attacker.Shots++;
            var shooter = attackingUnit.SelectShooter(_random);
            var goalChance = Math.Clamp(
                Probability(
                    BaseGoalChancePerShot,
                    FinishingSensitivity * (PlayerStrength.Finishing(shooter) - defender.Goaltending)),
                MinimumChance,
                MaximumChance);

            if (!_random.Chance(goalChance))
            {
                return false;
            }

            attacker.Goals++;
            var (primaryAssist, secondaryAssist) = attackingUnit.SelectAssists(shooter, _random);
            var seconds = windowStartSeconds + _random.NextInt(0, windowSeconds);
            _goals.Add(new GoalEvent(
                attacker.TeamId,
                shooter.Id,
                primaryAssist,
                secondaryAssist,
                period,
                TimeSpan.FromSeconds(seconds)));
            return true;
        }

        /// <summary>
        /// Plays three rounds, stopping once one side cannot be caught, then sudden-death rounds
        /// until one side scores and the other does not. Shooters go in order of shootout
        /// strength and nobody shoots twice until every dressed skater has shot.
        /// </summary>
        private ShootoutResult PlayShootout()
        {
            var attempts = new List<ShootoutAttempt>();
            var homeGoals = 0;
            var awayGoals = 0;

            for (var round = 0; round < ShootoutRounds; round++)
            {
                homeGoals += TakeShootoutAttempt(_home, _away, round, attempts);
                if (IsShootoutDecided(homeGoals, awayGoals, homeRemaining: ShootoutRounds - round - 1, awayRemaining: ShootoutRounds - round))
                {
                    return CreateShootoutResult(homeGoals, awayGoals, attempts);
                }

                awayGoals += TakeShootoutAttempt(_away, _home, round, attempts);
                if (IsShootoutDecided(homeGoals, awayGoals, homeRemaining: ShootoutRounds - round - 1, awayRemaining: ShootoutRounds - round - 1))
                {
                    return CreateShootoutResult(homeGoals, awayGoals, attempts);
                }
            }

            for (var round = ShootoutRounds; homeGoals == awayGoals; round++)
            {
                homeGoals += TakeShootoutAttempt(_home, _away, round, attempts);
                awayGoals += TakeShootoutAttempt(_away, _home, round, attempts);
            }

            return CreateShootoutResult(homeGoals, awayGoals, attempts);
        }

        private static bool IsShootoutDecided(int homeGoals, int awayGoals, int homeRemaining, int awayRemaining) =>
            homeGoals > awayGoals + awayRemaining || awayGoals > homeGoals + homeRemaining;

        private int TakeShootoutAttempt(MatchSide shooting, MatchSide defending, int round, List<ShootoutAttempt> attempts)
        {
            var shooter = shooting.ShootoutOrder[round % shooting.ShootoutOrder.Count];
            var goalChance = Math.Clamp(
                Probability(
                    BaseShootoutGoalChance,
                    FinishingSensitivity * (PlayerStrength.Shootout(shooter) - defending.Goaltending)),
                MinimumShootoutChance,
                MaximumShootoutChance);

            var scored = _random.Chance(goalChance);
            attempts.Add(new ShootoutAttempt(shooting.TeamId, shooter.Id, defending.Goalie.Id, scored));
            return scored ? 1 : 0;
        }

        private ShootoutResult CreateShootoutResult(int homeGoals, int awayGoals, List<ShootoutAttempt> attempts) =>
            new(attempts, homeGoals > awayGoals ? _home.TeamId : _away.TeamId);

        private MatchResult CreateResult(MatchDecision decision, ShootoutResult? shootout)
        {
            // The shootout winner is credited a single deciding goal that no player scored.
            var homeBonus = shootout?.WinnerId == _home.TeamId ? 1 : 0;
            var awayBonus = shootout?.WinnerId == _away.TeamId ? 1 : 0;

            return new MatchResult(
                CreateTeamResult(_home, _away, homeBonus),
                CreateTeamResult(_away, _home, awayBonus),
                decision,
                _goals,
                shootout,
                _random.State);
        }

        /// <summary>
        /// Derives individual statistics from the goal events and shot totals, so they always
        /// reconcile with the score. Shootout attempts never reach the goal events or shot totals.
        /// </summary>
        private MatchTeamResult CreateTeamResult(MatchSide side, MatchSide opponent, int shootoutBonus)
        {
            var teamGoals = _goals.Where(goal => goal.TeamId == side.TeamId).ToList();
            var skaters = side.Skaters.Select(skater => new SkaterMatchStatistics(
                skater.Id,
                Goals: teamGoals.Count(goal => goal.ScorerId == skater.Id),
                Assists: teamGoals.Count(goal => goal.PrimaryAssistId == skater.Id || goal.SecondaryAssistId == skater.Id)));
            var goalie = new GoalieMatchStatistics(side.Goalie.Id, ShotsAgainst: opponent.Shots, GoalsAgainst: opponent.Goals);

            return new MatchTeamResult(side.TeamId, side.Goals + shootoutBonus, side.Shots, skaters, goalie);
        }

        /// <summary>
        /// Shifts a base probability by an adjustment in log-odds, so equal strengths give the
        /// base probability and differences move it smoothly toward zero or one.
        /// </summary>
        private static double Probability(double baseProbability, double logOddsAdjustment)
        {
            var logOdds = Math.Log(baseProbability / (1 - baseProbability)) + logOddsAdjustment;
            return 1 / (1 + Math.Exp(-logOdds));
        }
    }
}