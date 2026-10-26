using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HockeySim.Desktop.Home;

public sealed partial class UpcomingGamesPanelView : UserControl
{
    public UpcomingGamesPanelView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}