using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace HockeySim.Desktop.Tables;

/// <summary>
/// A table header cell that sorts by its column when clicked, with an arrow beside the label while
/// the table is sorted by this column. Bind <see cref="Command"/> to a command taking the column
/// key, and <see cref="Sort"/> to the table's sort.
/// </summary>
public sealed class SortHeader : Button
{
    public static readonly StyledProperty<string> ColumnProperty = AvaloniaProperty.Register<SortHeader, string>(nameof(Column), string.Empty);

    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<SortHeader, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<TableSort?> SortProperty = AvaloniaProperty.Register<SortHeader, TableSort?>(nameof(Sort));

    private readonly TextBlock _label = new();
    private readonly TextBlock _arrow = new() { FontSize = 8, Margin = new Thickness(1, 0, 0, 0) };

    public SortHeader()
    {
        Classes.Add("sort-header");
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _label, _arrow },
        };
    }

    /// <summary>The key of the column this header sorts, passed to the command when clicked.</summary>
    public string Column
    {
        get => GetValue(ColumnProperty);
        set => SetValue(ColumnProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>The table's current sort; the arrow shows when it is on this column.</summary>
    public TableSort? Sort
    {
        get => GetValue(SortProperty);
        set => SetValue(SortProperty, value);
    }

    /// <summary>The arrow shown beside the label, or empty when the table is not sorted by this column.</summary>
    public string Indicator => Sort is { } sort && sort.Column == Column
        ? (sort.Direction == SortDirection.Ascending ? "▲" : "▼")
        : string.Empty;

    protected override Type StyleKeyOverride => typeof(Button);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColumnProperty)
        {
            CommandParameter = Column;
        }

        if (change.Property == LabelProperty)
        {
            _label.Text = Label;
        }

        if (change.Property == ColumnProperty || change.Property == SortProperty)
        {
            _arrow.Text = Indicator;
        }
    }
}