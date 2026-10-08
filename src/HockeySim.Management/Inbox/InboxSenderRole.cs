namespace HockeySim.Management.Inbox;

/// <summary>
/// Describes who in the game world wrote an inbox message, independent of their name.
/// </summary>
public enum InboxSenderRole
{
    Owner,
    AssistantGeneralManager,
    HeadScout,
    Captain,
    HeadTrainer,
}