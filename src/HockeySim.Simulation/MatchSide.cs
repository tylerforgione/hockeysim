using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Simulation;

/// <summary>
/// One team's transient state during a simulated match. The lineup is captured once at match
/// start, so the eligible players stay fixed for the whole match.
/// </summary>
internal sealed class MatchSide
{
    // Fixed share of ice time per forward line and defence pair, roughly matching typical
    // even-strength deployment. Higher lines and pairs are involved in more of the play.
    private static readonly double[] ForwardLineUsage = [0.36, 0.30, 0.22, 0.12];
    private static readonly double[] DefencePairUsage = [0.40, 0.34, 0.26];

    private readonly OnIceUnit[,] _units;

    public MatchSide(Team team)
    {
        var lineup = team.Lineup;

        TeamId = team.Id;
        Goalie = lineup.StartingGoalie;
        Goaltending = PlayerStrength.Goaltending(Goalie);

        _units = new OnIceUnit[lineup.ForwardLines.Count, lineup.DefencePairs.Count];
        for (var line = 0; line < lineup.ForwardLines.Count; line++)
        {
            for (var pair = 0; pair < lineup.DefencePairs.Count; pair++)
            {
                _units[line, pair] = new OnIceUnit(lineup.ForwardLines[line], lineup.DefencePairs[pair]);
            }
        }

        // OrderByDescending is stable, so equally rated shooters keep their lineup order and
        // the shootout order is deterministic.
        ShootoutOrder = lineup.ForwardLines
            .SelectMany(line => line.Players)
            .Concat(lineup.DefencePairs.SelectMany(pair => pair.Players))
            .OrderByDescending(PlayerStrength.Shootout)
            .ToList();
    }

    public TeamId TeamId { get; }

    public Player Goalie { get; }

    public double Goaltending { get; }

    public IReadOnlyList<Player> ShootoutOrder { get; }

    public int Goals { get; set; }

    public int Shots { get; set; }

    public OnIceUnit SelectUnit(ControlledRandom random) =>
        _units[random.NextWeightedIndex(ForwardLineUsage), random.NextWeightedIndex(DefencePairUsage)];
}