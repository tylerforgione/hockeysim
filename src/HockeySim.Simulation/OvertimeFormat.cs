namespace HockeySim.Simulation;

/// <summary>How a match that is tied after regulation is decided.</summary>
public enum OvertimeFormat
{
    /// <summary>
    /// Five minutes of three-on-three sudden death, then a shootout if still tied.
    /// </summary>
    RegularSeason,

    /// <summary>
    /// As many twenty-minute five-on-five sudden-death periods as it takes; never a shootout.
    /// </summary>
    Playoff,
}