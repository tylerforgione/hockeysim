using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HockeySim.Desktop.Roster;

public sealed partial class GoalieTableView : UserControl
{
    public GoalieTableView()
    {
        InitializeComponent();
        RosterRowActivation.OpenProfileOnDoubleTap(this);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}