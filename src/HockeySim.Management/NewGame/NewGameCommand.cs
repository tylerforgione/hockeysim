namespace HockeySim.Management.NewGame;

/// <summary>
/// Supplies every external choice needed to create a reproducible fictional league.
/// </summary>
public sealed record NewGameCommand(
    int SeasonYear,
    RandomState RandomState,
    string ManagedTeamName);