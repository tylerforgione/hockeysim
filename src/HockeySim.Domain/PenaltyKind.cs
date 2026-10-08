namespace HockeySim.Domain;

/// <summary>
/// How a penalty is served. Minors, double minors, and majors leave the team a skater short
/// unless they are coincidental; misconducts and game misconducts never do.
/// </summary>
public enum PenaltyKind
{
    /// <summary>Two minutes, ended early by a power-play goal.</summary>
    Minor,

    /// <summary>Two consecutive minors; a power-play goal ends only the one being served.</summary>
    DoubleMinor,

    /// <summary>Five minutes, served in full whatever is scored.</summary>
    Major,

    /// <summary>Ten minutes off the ice for the player, without leaving the team short.</summary>
    Misconduct,

    /// <summary>The player is ejected for the rest of the match. Recorded as ten penalty minutes.</summary>
    GameMisconduct,

    /// <summary>A foul on a scoring chance, awarded as a penalty shot instead of time in the box.</summary>
    PenaltyShot,
}