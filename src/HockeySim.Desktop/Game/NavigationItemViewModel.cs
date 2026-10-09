using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HockeySim.Desktop.Game;

/// <summary>A page tab in the strip under the team banner.</summary>
public sealed partial class NavigationItemViewModel : ObservableObject
{
    private readonly Action<ShellPage> _navigate;

    [ObservableProperty]
    private bool _isActive;

    public NavigationItemViewModel(ShellPage page, string label, bool isAvailable, Action<ShellPage> navigate)
    {
        Page = page;
        Label = label;
        IsAvailable = isAvailable;
        _navigate = navigate;
    }

    public ShellPage Page { get; }

    public string Label { get; }

    /// <summary>Gets whether the page exists yet; planned pages are listed but disabled.</summary>
    public bool IsAvailable { get; }

    [RelayCommand(CanExecute = nameof(IsAvailable))]
    private void Navigate()
    {
        _navigate(Page);
    }
}

/// <summary>A section's pages, in tab order.</summary>
public sealed record NavigationSectionViewModel(ShellSection Section, IReadOnlyList<NavigationItemViewModel> Items);