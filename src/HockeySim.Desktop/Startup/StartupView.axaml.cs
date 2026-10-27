using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HockeySim.Desktop.Startup;

public sealed partial class StartupView : UserControl
{
    /// <summary>The space kept around the rink graphic.</summary>
    public static readonly Thickness RinkMargin = new(24);

    // The rink graphic's drawn size, which it keeps as it scales.
    private const double RinkWidth = 900;
    private const double RinkHeight = 420;

    // How far inside the end boards, as a share of the rink's length, the recent games sit.
    private const double BoardsInset = 0.06;

    private readonly Grid _rinkArea;
    private readonly StackPanel _recentGamesArea;

    public StartupView()
    {
        InitializeComponent();
        _rinkArea = this.FindControl<Grid>("RinkArea")!;
        _recentGamesArea = this.FindControl<StackPanel>("RecentGamesArea")!;
        _rinkArea.SizeChanged += (_, _) => FitRecentGamesInsideRink();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Keeps the recent games between the rink's end boards. The rink scales to fit its area, so
    /// where its ends fall depends on the window's shape.
    /// </summary>
    private void FitRecentGamesInsideRink()
    {
        var available = _rinkArea.Bounds.Size.Deflate(RinkMargin);
        if (available.Width <= 0 || available.Height <= 0)
        {
            return;
        }

        var scale = Math.Min(available.Width / RinkWidth, available.Height / RinkHeight);
        var rinkLength = RinkWidth * scale;
        var side = RinkMargin.Left + ((available.Width - rinkLength) / 2) + (rinkLength * BoardsInset);
        _recentGamesArea.Margin = new Thickness(side, RinkMargin.Top, side, RinkMargin.Bottom);
    }
}