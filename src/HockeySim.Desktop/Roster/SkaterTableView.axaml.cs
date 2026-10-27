using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HockeySim.Desktop.Roster;

public sealed partial class SkaterTableView : UserControl
{
    public SkaterTableView()
    {
        InitializeComponent();
        RosterRowActivation.OpenProfileOnDoubleTap(this);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}