using HockeySim.Domain;

namespace HockeySim.Management.Saves;

/// <summary>
/// What a saved game's list entry shows about the managed team, captured when the game is saved
/// so a listing can show it without loading the game. It is written ahead of the game in the
/// save, derived from it, and never read back when loading.
/// </summary>
/// <param name="PrimaryColour">Written as #RRGGBB, as is <paramref name="SecondaryColour"/>.</param>
/// <param name="CurrentDate">The season's current date: the next league day to be played.</param>
/// <param name="Losses">Regulation losses.</param>
/// <param name="OvertimeLosses">Overtime and shootout losses, as the team banner counts them.</param>
/// <param name="DivisionRank">The team's rank in its division's regular-season standings.</param>
/// <param name="PlayoffRound">
/// The round the team is playing in or was eliminated in, or the final for the champion. Null
/// unless <paramref name="Playoffs"/> is not <see cref="SavedPlayoffProgress.NotInPlayoffs"/>.
/// </param>
public sealed record SavedGameDetails(
    string TeamName,
    string PrimaryColour,
    string SecondaryColour,
    DateOnly CurrentDate,
    SeasonPhase Phase,
    int Wins,
    int Losses,
    int OvertimeLosses,
    int Points,
    string DivisionName,
    int DivisionRank,
    SavedPlayoffProgress Playoffs,
    PlayoffRound? PlayoffRound);

/// <summary>Where the managed team stands in the playoffs.</summary>
public enum SavedPlayoffProgress
{
    /// <summary>The playoffs have not started, or the team missed them.</summary>
    NotInPlayoffs,

    /// <summary>The team is still playing.</summary>
    Alive,

    /// <summary>The team lost a series.</summary>
    Eliminated,

    /// <summary>The team won the final.</summary>
    Champion,
}