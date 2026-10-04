namespace HockeySim.Management.Inbox;

/// <summary>
/// Holds the messages delivered to the user, newest first.
/// </summary>
internal sealed class InboxMessages
{
    private readonly List<InboxMessage> _messages = [];

    public IReadOnlyList<InboxMessage> Messages => _messages;

    /// <summary>
    /// Rebuilds an inbox from its messages, newest first. Identities must run down from the
    /// message count to one, as delivery assigns them, so later deliveries stay unique.
    /// </summary>
    public static InboxMessages Restore(IEnumerable<InboxMessage> newestFirst)
    {
        ArgumentNullException.ThrowIfNull(newestFirst);

        var inbox = new InboxMessages();
        inbox._messages.AddRange(newestFirst);
        if (inbox._messages.Any(message => message is null)
            || !inbox._messages.Select(message => message.Id.Value).SequenceEqual(
                Enumerable.Range(1, inbox._messages.Count).Reverse()))
        {
            throw new ArgumentException(
                "Inbox messages must be numbered from the newest down to 1.",
                nameof(newestFirst));
        }

        return inbox;
    }

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