using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Simulation;

/// <summary>
/// Calculates a match result from both teams' lineups and an explicit random state, without
/// changing the teams. Management depends on this so tests can substitute an engine when a
/// specific failure is needed.
/// </summary>
public interface IMatchSimulator
{
    MatchResult Simulate(Match match, RandomState randomState);
}