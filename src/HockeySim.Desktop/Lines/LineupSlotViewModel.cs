using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Domain;

namespace HockeySim.Desktop.Lines;

/// <summary>
/// One position in the lineup, such as the first line's left wing or a power-play unit's centre.
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
        bool isEditable,
        Action<LineupSlotViewModel, PlayerOptionViewModel?> selectionChanged)
    {
        Label = label;
        Options = options;
        _selectedPlayer = selectedPlayer;
        IsEditable = isEditable;
        _selectionChanged = selectionChanged;
    }

    public string Label { get; }

    /// <summary>
    /// Gets every roster player who can be chosen for this slot, dressed or scratched.
    /// </summary>
    public IReadOnlyList<PlayerOptionViewModel> Options { get; }

    /// <summary>
    /// Gets whether the user may change this slot; only the managed team's lineup is editable.
    /// </summary>
    public bool IsEditable { get; }

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

/// <summary>
/// One special-situation unit, laid out with its forwards in front of its defence.
/// </summary>
/// <param name="Slots">Every slot in the order of the situation's format.</param>
public sealed record SpecialUnitViewModel(
    SpecialSituation Situation,
    string Label,
    IReadOnlyList<LineupSlotViewModel> Slots,
    IReadOnlyList<LineupSlotViewModel> Forwards,
    IReadOnlyList<LineupSlotViewModel> Defence);

public sealed record SpecialSituationGroupViewModel(
    string Title,
    string Caption,
    IReadOnlyList<SpecialUnitViewModel> Units);