using HockeySim.Domain;
using HockeySim.Simulation.Play;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Simulation;

/// <summary>
/// Simulates a match as play-by-play hockey events from both teams' lineups at match start.
/// </summary>
/// <remarks>
/// The simulator reads the supplied teams and health but never changes them; the injuries and wear
/// it causes are in the result. All randomness comes from the
/// supplied <see cref="RandomState"/>, so the same teams and state always produce the same
/// result within an engine version.
/// </remarks>
public sealed class MatchSimulator : IMatchSimulator
{
    public MatchResult Simulate(Match match, OvertimeFormat overtime, MatchHealth health, RandomState randomState)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(health);
        if (!Enum.IsDefined(overtime))
        {
            throw new ArgumentOutOfRangeException(nameof(overtime), "The overtime format must be defined.");
        }

        return new MatchPlay(match, overtime, health, randomState).Play();
    }
}