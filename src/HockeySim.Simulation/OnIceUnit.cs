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
}