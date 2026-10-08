using HockeySim.Domain;
using HockeySim.Simulation.Play;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Simulation;

/// <summary>
/// Simulates a match as play-by-play hockey events from both teams' lineups at match start.
/// </summary>
/// <remarks>
/// The simulator reads the supplied teams but never changes them. All randomness comes from the
/// supplied <see cref="RandomState"/>, so the same teams and state always produce the same
/// result within an engine version.
/// </remarks>
public sealed class MatchSimulator : IMatchSimulator
{
    public MatchResult Simulate(Match match, OvertimeFormat overtime, RandomState randomState)
    {
        ArgumentNullException.ThrowIfNull(match);
        if (!Enum.IsDefined(overtime))
        {
            throw new ArgumentOutOfRangeException(nameof(overtime), "The overtime format must be defined.");
        }

        return new MatchPlay(match, overtime, randomState).Play();
    }
}