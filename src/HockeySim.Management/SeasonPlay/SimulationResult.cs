using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Management.SeasonPlay;

/// <summary>How a run of simulated days ended.</summary>
/// <param name="Stop">Why the run stopped.</param>
/// <param name="DaysPlayed">The league days played, possibly none if the run was cancelled at once.</param>
/// <param name="Snapshot">The game after the run's last day.</param>
public sealed record SimulationResult(SimulationStop Stop, int DaysPlayed, GameSnapshot Snapshot);