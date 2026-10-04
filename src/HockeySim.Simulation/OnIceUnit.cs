using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Simulation;

/// <summary>
/// The five skaters a team has on the ice for one stretch of play: a forward line and a
/// defence pair.
/// </summary>
internal sealed class OnIceUnit
{
    // Forwards take most shots; defence players shoot less often from the point.
    private const double ForwardShotShare = 2.0;
    private const double DefenceShotShare = 1.0;

    // Most goals are assisted, and most assisted goals have two assists.
    private const double PrimaryAssistChance = 0.9;
    private const double SecondaryAssistChance = 0.75;

    // Keeps every teammate eligible for an assist, even with a playmaking strength of zero.
    private const double MinimumAssistWeight = 1.0;

    private static readonly double[] ShooterWeights =
    [
        ForwardShotShare,
        ForwardShotShare,
        ForwardShotShare,
        DefenceShotShare,
        DefenceShotShare,
    ];

    private readonly Player[] _skaters;

    public OnIceUnit(ForwardLine line, DefencePair pair)
    {
        _skaters = [.. line.Players, .. pair.Players];
        Offence = _skaters.Average(PlayerStrength.Offence);
        Defence = _skaters.Average(PlayerStrength.Defence);
    }

    public double Offence { get; }

    public double Defence { get; }

    public Player SelectShooter(ControlledRandom random) => _skaters[random.NextWeightedIndex(ShooterWeights)];

    /// <summary>
    /// Credits up to two of the scorer's linemates on this unit, favouring better playmakers.
    /// A secondary assist is only awarded alongside a primary assist, and nobody is credited twice.
    /// </summary>
    public (PlayerId? Primary, PlayerId? Secondary) SelectAssists(Player scorer, ControlledRandom random)
    {
        if (!random.Chance(PrimaryAssistChance))
        {
            return (null, null);
        }

        var candidates = _skaters.Where(skater => skater != scorer).ToList();
        var primary = SelectPlaymaker(candidates, random);

        if (!random.Chance(SecondaryAssistChance))
        {
            return (primary.Id, null);
        }

        candidates.Remove(primary);
        return (primary.Id, SelectPlaymaker(candidates, random).Id);
    }

    private static Player SelectPlaymaker(List<Player> candidates, ControlledRandom random)
    {
        var weights = candidates.Select(player => MinimumAssistWeight + PlayerStrength.Playmaking(player)).ToList();
        return candidates[random.NextWeightedIndex(weights)];
    }
}