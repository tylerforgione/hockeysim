using HockeySim.Management.Inbox;

namespace HockeySim.Management.GameManagement.Snapshots;

public sealed record InboxMessageSnapshot(
    InboxMessageId Id,
    InboxSenderRole SenderRole,
    string SenderName,
    string Subject,
    string Body,
    bool IsRead)
{
    internal static InboxMessageSnapshot Create(InboxMessage message) =>
        new(
            message.Id,
            message.SenderRole,
            message.SenderName,
            message.Subject,
            message.Body,
            message.IsRead);
}