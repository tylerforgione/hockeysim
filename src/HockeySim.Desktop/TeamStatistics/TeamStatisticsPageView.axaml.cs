using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HockeySim.Desktop.TeamStatistics;

public sealed partial class TeamStatisticsPageView : UserControl
{
    public TeamStatisticsPageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}