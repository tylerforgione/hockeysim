namespace HockeySim.Management.Inbox;

/// <summary>
/// Holds the messages delivered to the user, newest first.
/// </summary>
internal sealed class InboxMessages
{
    private readonly List<InboxMessage> _messages = [];

    public IReadOnlyList<InboxMessage> Messages => _messages;

    public void Deliver(InboxSenderRole senderRole, string senderName, string subject, string body)
    {
        var id = new InboxMessageId(_messages.Count + 1);
        _messages.Insert(0, new InboxMessage(id, senderRole, senderName, subject, body));
    }

    public void MarkRead(InboxMessageId id)
    {
        var message = _messages.SingleOrDefault(message => message.Id == id)
            ?? throw new ArgumentException($"Inbox message '{id.Value}' does not exist.", nameof(id));

        message.MarkRead();
    }
}