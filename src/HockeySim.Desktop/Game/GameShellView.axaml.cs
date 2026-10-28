using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace HockeySim.Desktop.Game;

public sealed partial class GameShellView : UserControl
{
    public GameShellView()
    {
        InitializeComponent();

        // Tunnelled and heard even if handled, so a page's controls cannot swallow the mouse's
        // back and forward buttons.
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not GameShellViewModel shell)
        {
            return;
        }

        var command = e.GetCurrentPoint(this).Properties.PointerUpdateKind switch
        {
            PointerUpdateKind.XButton1Pressed => shell.GoBackCommand,
            PointerUpdateKind.XButton2Pressed => shell.GoForwardCommand,
            _ => null,
        };
        if (command is null)
        {
            return;
        }

        if (command.CanExecute(null))
        {
            command.Execute(null);
        }

        e.Handled = true;
    }
}