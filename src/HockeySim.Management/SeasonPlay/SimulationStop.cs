namespace HockeySim.Management.SeasonPlay;

/// <summary>
/// Why simulating ahead stopped. Every stop but <see cref="Cancelled"/> comes after the day that
/// caused it; when the target is reached on a day that would also stop the run early, the target
/// is reported.
/// </summary>
public enum SimulationStop
{
    /// <summary>The run reached its <see cref="SimulationTarget"/>.</summary>
    TargetReached,

    /// <summary>The caller cancelled the run between days.</summary>
    Cancelled,

    /// <summary>
    /// A managed-team player picked up an injury that keeps them out. Knocks a player can play
    /// through do not stop a run; they are too common.
    /// </summary>
    ManagedPlayerInjured,

    /// <summary>The managed team plays today with dressed players who cannot play.</summary>
    PlayersToReplace,

    /// <summary>The season moved into its next phase.</summary>
    PhaseChanged,

    /// <summary>The playoffs crowned a champion, so no further days can be played.</summary>
    SeasonComplete,
}