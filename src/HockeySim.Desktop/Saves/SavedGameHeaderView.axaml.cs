using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HockeySim.Desktop.Saves;

/// <summary>The header of the saved-games table, over <see cref="SavedGameRowView"/> rows.</summary>
public sealed partial class SavedGameHeaderView : UserControl
{
    public SavedGameHeaderView()
    {
        InitializeComponent();
        this.FindControl<Grid>("HeaderGrid")!.ColumnDefinitions = ColumnDefinitions.Parse(SavedGameColumns.Widths);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}