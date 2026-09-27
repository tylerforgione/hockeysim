using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HockeySim.Desktop.NewGame;

public sealed partial class NewGameView : UserControl
{
    public NewGameView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}