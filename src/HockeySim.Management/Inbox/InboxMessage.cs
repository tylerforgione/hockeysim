namespace HockeySim.Management.Inbox;

internal sealed class InboxMessage
{
    public InboxMessage(
        InboxMessageId id,
        InboxSenderRole senderRole,
        string senderName,
        string subject,
        string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(senderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        Id = id;
        SenderRole = senderRole;
        SenderName = senderName;
        Subject = subject;
        Body = body;
    }

    public InboxMessageId Id { get; }

    public InboxSenderRole SenderRole { get; }

    public string SenderName { get; }

    public string Subject { get; }

    public string Body { get; }

    public bool IsRead { get; private set; }

    public void MarkRead()
    {
        IsRead = true;
    }
}