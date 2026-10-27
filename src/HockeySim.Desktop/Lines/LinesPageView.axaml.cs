using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace HockeySim.Desktop.Lines;

public sealed partial class LinesPageView : UserControl
{
    // Marks a player's name as a link to their profile.
    private const string PlayerLinkClass = "player-link";

    public LinesPageView()
    {
        InitializeComponent();
        LineupDragDrop.Attach(this);
        AddHandler(Button.ClickEvent, OnPlayerLinkClick);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // The links sit in item templates and the slot ComboBox's template, so the page opens the profile
    // for whichever player the link's slot or scratch row holds.
    private void OnPlayerLinkClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button button
            || !button.Classes.Contains(PlayerLinkClass)
            || DataContext is not LinesPageViewModel lines)
        {
            return;
        }

        var player = button.DataContext switch
        {
            LineupSlotViewModel slot => slot.SelectedPlayer,
            PlayerOptionViewModel option => option,
            _ => null,
        };
        if (player is not null && lines.OpenPlayerCommand.CanExecute(player))
        {
            lines.OpenPlayerCommand.Execute(player);
        }
    }
}