using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Inbox;

namespace HockeySim.Desktop.Inbox;

public sealed class InboxMessageViewModel
{
    private const int PreviewLength = 90;

    public InboxMessageViewModel(InboxMessageSnapshot message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Id = message.Id;
        SenderName = message.SenderName;
        SenderRole = DescribeRole(message.SenderRole);
        Subject = message.Subject;
        Body = message.Body;
        IsUnread = !message.IsRead;

        var firstParagraph = message.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0 && !line.EndsWith(',')) ?? string.Empty;
        Preview = firstParagraph.Length > PreviewLength ? $"{firstParagraph[..PreviewLength].TrimEnd()}…" : firstParagraph;
    }

    public InboxMessageId Id { get; }

    public string SenderName { get; }

    public string SenderRole { get; }

    public string Subject { get; }

    public string Body { get; }

    public string Preview { get; }

    public bool IsUnread { get; }

    public string SenderInitials => string.Concat(
        SenderName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => word[0]));

    private static string DescribeRole(InboxSenderRole role) => role switch
    {
        InboxSenderRole.Owner => "Ownership",
        InboxSenderRole.AssistantGeneralManager => "Front office",
        InboxSenderRole.HeadScout => "Scouting",
        InboxSenderRole.Captain => "Player",
        InboxSenderRole.HeadTrainer => "Medical staff",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown sender role."),
    };
}