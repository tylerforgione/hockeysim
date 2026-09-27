using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HockeySim.Desktop.Teams;

public sealed partial class TeamsPageView : UserControl
{
    public TeamsPageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}