using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HockeySim.Desktop.Game;

public sealed partial class NavigationItemViewModel : ObservableObject
{
    private readonly Action<ShellPage> _navigate;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBadge))]
    private int _badgeCount;

    public NavigationItemViewModel(
        ShellPage page,
        string label,
        Action<ShellPage> navigate,
        string? unavailableReason = null)
    {
        Page = page;
        Label = label;
        _navigate = navigate;
        UnavailableReason = unavailableReason;
    }

    public ShellPage Page { get; }

    public string Label { get; }

    public bool IsAvailable => UnavailableReason is null;

    public bool IsUnavailable => !IsAvailable;

    /// <summary>
    /// Explains why a planned page cannot be opened yet; null when the page is available.
    /// </summary>
    public string? UnavailableReason { get; }

    public bool HasBadge => BadgeCount > 0;

    [RelayCommand(CanExecute = nameof(IsAvailable))]
    private void Navigate()
    {
        _navigate(Page);
    }
}

public sealed record NavigationSectionViewModel(string Title, IReadOnlyList<NavigationItemViewModel> Items);