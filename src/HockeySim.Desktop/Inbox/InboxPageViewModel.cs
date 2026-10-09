using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Game;
using HockeySim.Management.Inbox;

namespace HockeySim.Desktop.Inbox;

/// <summary>
/// Lists messages and shows the selected one in a reading pane. Opening a message marks it read.
/// </summary>
public sealed partial class InboxPageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;
    private bool _isRefreshing;

    [ObservableProperty]
    private IReadOnlyList<InboxMessageViewModel> _messages = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedMessage))]
    private InboxMessageViewModel? _selectedMessage;

    public InboxPageViewModel(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        Refresh();
    }

    public override string Subtitle
    {
        get
        {
            var unread = Messages.Count(message => message.IsUnread);
            return unread == 0 ? $"{Messages.Count} messages" : $"{Messages.Count} messages · {unread} unread";
        }
    }

    public bool HasSelectedMessage => SelectedMessage is not null;

    public void OpenMessage(InboxMessageId messageId)
    {
        SelectedMessage = Messages.Single(message => message.Id == messageId);
    }

    public override void Refresh()
    {
        var selectedId = SelectedMessage?.Id;
        _isRefreshing = true;
        try
        {
            Messages = _session.Snapshot.Inbox.Select(message => new InboxMessageViewModel(message)).ToList();
            SelectedMessage = Messages.FirstOrDefault(message => message.Id == selectedId);
        }
        finally
        {
            _isRefreshing = false;
        }

        OnPropertyChanged(nameof(Subtitle));
    }

    partial void OnSelectedMessageChanged(InboxMessageViewModel? value)
    {
        if (!_isRefreshing && value is { IsUnread: true })
        {
            _session.MarkInboxMessageRead(value.Id);
        }
    }
}