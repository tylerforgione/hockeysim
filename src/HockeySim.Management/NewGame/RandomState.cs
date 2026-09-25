namespace HockeySim.Management.NewGame;

/// <summary>
/// Identifies the exact deterministic random stream position used by game workflows.
/// Preserve this value with game state so later operations can continue without rerolling.
/// </summary>
public readonly record struct RandomState(ulong Value);