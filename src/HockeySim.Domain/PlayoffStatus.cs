namespace HockeySim.Domain;

/// <summary>
/// What a team's remaining schedule can no longer change about its playoff qualification. Each
/// status is reported only once it is guaranteed; a team with several reports the strongest,
/// which is the last listed.
/// </summary>
public enum PlayoffStatus
{
    /// <summary>The team can still both qualify and miss the playoffs.</summary>
    Undecided,

    /// <summary>The team cannot qualify for the playoffs (the standings "e").</summary>
    Eliminated,

    /// <summary>The team will qualify for the playoffs (the standings "x").</summary>
    ClinchedPlayoffSpot,

    /// <summary>The team will finish first in its division (the standings "y").</summary>
    ClinchedDivision,

    /// <summary>The team will finish first in its conference (the standings "z").</summary>
    ClinchedConference,

    /// <summary>The team will finish with the best record in the league (the standings "p").</summary>
    ClinchedBestRecord,
}