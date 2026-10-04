using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HockeySim.Desktop.Confirmation;

/// <summary>
/// Asks the user to confirm an action that would lose something, such as replacing a save or
/// discarding unsaved progress. The main window shows the open request over every screen.
/// </summary>
public sealed partial class ConfirmationViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    private ConfirmationRequest? _current;

    public bool IsOpen => Current is not null;

    /// <summary>
    /// Shows the question; <paramref name="confirmed"/> runs only if the user confirms.
    /// </summary>
    public void Request(string title, string message, string confirmLabel, Action confirmed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmLabel);
        ArgumentNullException.ThrowIfNull(confirmed);

        Current = new ConfirmationRequest(title, message, confirmLabel, confirmed);
    }

    [RelayCommand]
    private void Confirm()
    {
        var request = Current;
        Current = null;
        request?.Confirmed();
    }

    [RelayCommand]
    private void Cancel()
    {
        Current = null;
    }
}

public sealed record ConfirmationRequest(string Title, string Message, string ConfirmLabel, Action Confirmed);