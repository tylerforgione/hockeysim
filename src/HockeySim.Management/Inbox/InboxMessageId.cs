namespace HockeySim.Management.Inbox;

/// <summary>
/// Identifies an inbox message within one game. Identities are sequential so that creating
/// messages never consumes the game's controlled random state.
/// </summary>
public readonly record struct InboxMessageId(int Value);