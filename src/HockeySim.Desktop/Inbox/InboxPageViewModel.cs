using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Game;
using HockeySim.Management.Inbox;

namespace HockeySim.Desktop.Inbox;

/// <summary>
/// Lists messages and shows the selected one in a reading pane. Opening a message marks it read.
/// </summary>
public sealed partial class InboxPageViewModel : ShellPageViewModel, ISelectablePage
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

    public event EventHandler? SelectionChanged;

    public override string Subtitle
    {
        get
        {
            var unread = Messages.Count(message => message.IsUnread);
            return unread == 0 ? $"{Messages.Count} messages" : $"{Messages.Count} messages · {unread} unread";
        }
    }

    public bool HasSelectedMessage => SelectedMessage is not null;

    object ISelectablePage.Selection => new InboxSelection(SelectedMessage?.Id);

    void ISelectablePage.Select(object selection)
    {
        var messageId = (selection as InboxSelection)?.Message;
        SelectedMessage = Messages.FirstOrDefault(message => message.Id == messageId);
    }

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
        if (_isRefreshing)
        {
            return;
        }

        // Announced before the message is marked read, which refreshes the shell's pages.
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        if (value is { IsUnread: true })
        {
            _session.MarkInboxMessageRead(value.Id);
        }
    }
}

/// <summary>The inbox's history selection: the message open, or none.</summary>
public sealed record InboxSelection(InboxMessageId? Message);