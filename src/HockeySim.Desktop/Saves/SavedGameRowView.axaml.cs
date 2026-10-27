using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HockeySim.Desktop.Saves;

/// <summary>One row of the saved-games table: the save, its team, and where the team stands.</summary>
public sealed partial class SavedGameRowView : UserControl
{
    public SavedGameRowView()
    {
        InitializeComponent();
        this.FindControl<Grid>("RowGrid")!.ColumnDefinitions = ColumnDefinitions.Parse(SavedGameColumns.Widths);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}