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
    [NotifyPropertyChangedFor(nameof(FitNote), nameof(HasFitNote))]
    private PlayerOptionViewModel? _selectedPlayer;

    /// <param name="role">The skater role the slot plays, or none for a goalie slot.</param>
    /// <param name="side">The side of the ice for a wing or defence slot that has one.</param>
    public LineupSlotViewModel(
        string label,
        IReadOnlyList<PlayerOptionViewModel> options,
        PlayerOptionViewModel selectedPlayer,
        SkaterRole? role,
        SkaterSide? side,
        bool isEditable,
        Action<LineupSlotViewModel, PlayerOptionViewModel?> selectionChanged)
    {
        Label = label;
        Options = options;
        _selectedPlayer = selectedPlayer;
        Role = role;
        Side = side;
        IsEditable = isEditable;
        _selectionChanged = selectionChanged;
    }

    public string Label { get; }

    public SkaterRole? Role { get; }

    public SkaterSide? Side { get; }

    /// <summary>
    /// Gets a warning when the chosen skater plays out of position or on their off-hand side here,
    /// such as "Out of position · off-hand side", or nothing when the slot suits them.
    /// </summary>
    public string? FitNote => Role is { } role && SelectedPlayer is { } player && player.Position != Position.Goalie
        ? FitNoteFor(player, role, Side)
        : null;

    public bool HasFitNote => FitNote is not null;

    /// <summary>
    /// Gets every roster player who can be chosen for this slot, dressed or scratched.
    /// </summary>
    public IReadOnlyList<PlayerOptionViewModel> Options { get; }

    /// <summary>
    /// Gets whether the user may change this slot; only the managed team's lineup is editable.
    /// </summary>
    public bool IsEditable { get; }

    private static string? FitNoteFor(PlayerOptionViewModel player, SkaterRole role, SkaterSide? side)
    {
        var notes = new List<string>(2);
        switch (SkaterFit.For(player.Position, role))
        {
            case PositionFit.OtherForwardPosition:
                notes.Add("Out of position");
                break;
            case PositionFit.AcrossForwardsAndDefence:
                notes.Add(player.Position == Position.Defence ? "Defenceman at forward" : "Forward on defence");
                break;
        }

        if (side is { } onSide && SkaterFit.IsOffHand(player.Handedness, onSide))
        {
            notes.Add(notes.Count == 0 ? "Off-hand side" : "off-hand side");
        }

        return notes.Count == 0 ? null : string.Join(" · ", notes);
    }

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
    IReadOnlyList<SpecialUnitViewModel> Units);