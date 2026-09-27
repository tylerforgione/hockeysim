using CommunityToolkit.Mvvm.ComponentModel;

namespace HockeySim.Desktop.Lines;

/// <summary>
/// One position in the lineup, such as the first line's left wing.
/// </summary>
public sealed partial class LineupSlotViewModel : ObservableObject
{
    private readonly Action<LineupSlotViewModel, PlayerOptionViewModel?> _selectionChanged;

    [ObservableProperty]
    private PlayerOptionViewModel? _selectedPlayer;

    public LineupSlotViewModel(
        string label,
        IReadOnlyList<PlayerOptionViewModel> options,
        PlayerOptionViewModel selectedPlayer,
        Action<LineupSlotViewModel, PlayerOptionViewModel?> selectionChanged)
    {
        Label = label;
        Options = options;
        _selectedPlayer = selectedPlayer;
        _selectionChanged = selectionChanged;
    }

    public string Label { get; }

    /// <summary>
    /// Gets every roster player who can legally fill this slot, dressed or scratched.
    /// </summary>
    public IReadOnlyList<PlayerOptionViewModel> Options { get; }

    partial void OnSelectedPlayerChanged(PlayerOptionViewModel? oldValue, PlayerOptionViewModel? newValue)
    {
        _selectionChanged(this, oldValue);
    }
}

public sealed record ForwardLineRowViewModel(
    string Label,
    LineupSlotViewModel LeftWing,
    LineupSlotViewModel Centre,
    LineupSlotViewModel RightWing);

public sealed record DefencePairRowViewModel(
    string Label,
    LineupSlotViewModel LeftDefence,
    LineupSlotViewModel RightDefence);