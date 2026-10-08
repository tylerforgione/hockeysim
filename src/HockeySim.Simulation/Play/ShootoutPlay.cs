using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Simulation.Play;

/// <summary>
/// Plays three rounds, stopping once one side cannot be caught, then sudden-death rounds until one
/// side scores and the other does not. Shooters go in order of shootout strength and nobody shoots
/// twice until every dressed skater has shot. Each attempt compares the shooter's shootout
/// strength with the goalie's goaltending.
/// </summary>
internal sealed class ShootoutPlay(MatchSide home, MatchSide away, ControlledRandom random)
{
    private readonly List<ShootoutAttempt> _attempts = [];

    public ShootoutResult Play()
    {
        var homeGoals = 0;
        var awayGoals = 0;
        const int Rounds = MatchTuning.ShootoutRounds;

        for (var round = 0; round < Rounds; round++)
        {
            homeGoals += TakeAttempt(home, away, round);
            if (IsDecided(homeGoals, awayGoals, homeRemaining: Rounds - round - 1, awayRemaining: Rounds - round))
            {
                return CreateResult(homeGoals, awayGoals);
            }

            awayGoals += TakeAttempt(away, home, round);
            if (IsDecided(homeGoals, awayGoals, homeRemaining: Rounds - round - 1, awayRemaining: Rounds - round - 1))
            {
                return CreateResult(homeGoals, awayGoals);
            }
        }

        for (var round = Rounds; homeGoals == awayGoals; round++)
        {
            homeGoals += TakeAttempt(home, away, round);
            awayGoals += TakeAttempt(away, home, round);
        }

        return CreateResult(homeGoals, awayGoals);
    }

    /// <summary>
    /// The chance a shooter alone against the goalie scores, in a shootout or on a penalty shot: a
    /// reference shooter against a reference goalie scores at the base rate.
    /// </summary>
    public static double OneOnOneGoalChance(Player shooter, MatchSide defending) =>
        Math.Clamp(
            Probability.Logistic(
                Probability.Logit(MatchTuning.BaseShootoutGoalChance)
                + (MatchTuning.FinishingSensitivity * (PlayerStrength.Shootout(shooter) - defending.Goaltending))),
            MatchTuning.MinimumShootoutChance,
            MatchTuning.MaximumShootoutChance);

    private static bool IsDecided(int homeGoals, int awayGoals, int homeRemaining, int awayRemaining) =>
        homeGoals > awayGoals + awayRemaining || awayGoals > homeGoals + homeRemaining;

    private int TakeAttempt(MatchSide shooting, MatchSide defending, int round)
    {
        var shooter = shooting.ShootoutOrder[round % shooting.ShootoutOrder.Count];
        var scored = random.Chance(OneOnOneGoalChance(shooter, defending));
        _attempts.Add(new ShootoutAttempt(shooting.TeamId, shooter.Id, defending.Goalie.Id, scored));
        return scored ? 1 : 0;
    }

    private ShootoutResult CreateResult(int homeGoals, int awayGoals) =>
        new(_attempts, homeGoals > awayGoals ? home.TeamId : away.TeamId);
}