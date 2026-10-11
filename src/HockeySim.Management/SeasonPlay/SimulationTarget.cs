using HockeySim.Domain;

namespace HockeySim.Management.SeasonPlay;

/// <summary>
/// How far simulating ahead goes. Each target names the current date the run stops on, unless
/// something stops it earlier (see <see cref="SimulationStop"/>).
/// </summary>
public enum SimulationTarget
{
    /// <summary>One league day.</summary>
    NextDay,

    /// <summary>
    /// The managed team's next match day, left unplayed so the lineup can be checked first. A team
    /// with no match ahead runs until the phase changes or the season ends.
    /// </summary>
    NextMatch,

    /// <summary>Seven league days.</summary>
    OneWeek,

    /// <summary>To the same day of the next month, or that month's last day if it is shorter.</summary>
    OneMonth,

    /// <summary>Opening day: the day after the last preseason match.</summary>
    EndOfPreseason,

    /// <summary>The day after the final regular-season day, when the playoffs are seeded.</summary>
    EndOfRegularSeason,

    /// <summary>The day after the current playoff round's last game.</summary>
    EndOfRound,

    /// <summary>The day after the final's deciding game, which completes the season.</summary>
    EndOfPlayoffs,
}

/// <summary>The simulation targets each season phase offers.</summary>
public static class SimulationTargets
{
    private static readonly IReadOnlyList<SimulationTarget> Preseason =
        [SimulationTarget.NextDay, SimulationTarget.NextMatch, SimulationTarget.EndOfPreseason];

    private static readonly IReadOnlyList<SimulationTarget> RegularSeason =
        [SimulationTarget.NextDay, SimulationTarget.NextMatch, SimulationTarget.OneWeek, SimulationTarget.OneMonth, SimulationTarget.EndOfRegularSeason];

    private static readonly IReadOnlyList<SimulationTarget> Playoffs =
        [SimulationTarget.NextDay, SimulationTarget.NextMatch, SimulationTarget.EndOfRound, SimulationTarget.EndOfPlayoffs];

    /// <summary>The targets offered in <paramref name="phase"/>, nearest first.</summary>
    public static IReadOnlyList<SimulationTarget> AvailableIn(SeasonPhase phase) => phase switch
    {
        SeasonPhase.Preseason => Preseason,
        SeasonPhase.RegularSeason => RegularSeason,
        _ => Playoffs,
    };
}