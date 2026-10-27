using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Game;

namespace HockeySim.Desktop.Players;

/// <summary>The profile's tabs that can be shown; Career waits for career history.</summary>
public enum PlayerProfileTab
{
    Overview,
    Ratings,
    Statistics,
    GameLog,
    Health,
}

/// <summary>A tab in the profile's tab row.</summary>
public sealed partial class PlayerProfileTabViewModel : ObservableObject, IPageTab
{
    [ObservableProperty]
    private bool _isActive;

    public PlayerProfileTabViewModel(string label, ICommand command)
    {
        Label = label;
        Command = command;
    }

    public string Label { get; }

    public ICommand Command { get; }
}