using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HockeySim.Desktop.Home;

public sealed partial class TaleOfTheTapePanelView : UserControl
{
    public TaleOfTheTapePanelView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}