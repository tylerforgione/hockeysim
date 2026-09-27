using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HockeySim.Desktop.Players;

public sealed partial class PlayerDetailView : UserControl
{
    public PlayerDetailView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}