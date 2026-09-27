using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HockeySim.Desktop.Home;

public sealed partial class HomePageView : UserControl
{
    public HomePageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}