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
    [NotifyPropertyChangedFor(nameof(FitNote), nameof(HasFitNote), nameof(InjuryNote), nameof(HasInjuryNote), nameof(IsOutNote))]
    private PlayerOptionViewModel? _selectedPlayer;

    /// <param name="role">The skater role the slot plays, or none for a goalie slot.</param>
    public LineupSlotViewModel(
        string label,
        IReadOnlyList<PlayerOptionViewModel> options,
        PlayerOptionViewModel selectedPlayer,
        SkaterRole? role,
        bool isEditable,
        Action<LineupSlotViewModel, PlayerOptionViewModel?> selectionChanged)
    {
        Label = label;
        Options = options;
        _selectedPlayer = selectedPlayer;
        Role = role;
        IsEditable = isEditable;
        _selectionChanged = selectionChanged;
    }

    public string Label { get; }

    public SkaterRole? Role { get; }

    /// <summary>
    /// Gets a warning when the chosen skater plays out of position here, such as "Forward on defence",
    /// or nothing when the slot suits their position.
    /// </summary>
    public string? FitNote => Role is { } role && SelectedPlayer is { } player && player.Position != Position.Goalie
        ? FitNoteFor(player.Position, role)
        : null;

    public bool HasFitNote => FitNote is not null;

    /// <summary>
    /// Gets a warning when the chosen player is injured: one who cannot play must be replaced
    /// before the team's next match, and one playing through it plays below their ratings.
    /// </summary>
    public string? InjuryNote => SelectedPlayer switch
    {
        { IsOut: true } => "Injured · cannot play",
        { IsPlayingHurt: true } => "Playing through an injury",
        _ => null,
    };

    public bool HasInjuryNote => InjuryNote is not null;

    public bool IsOutNote => SelectedPlayer?.IsOut == true;

    /// <summary>
    /// Gets every roster player who can be chosen for this slot, dressed or scratched.
    /// </summary>
    public IReadOnlyList<PlayerOptionViewModel> Options { get; }

    /// <summary>
    /// Gets whether the user may change this slot; only the managed team's lineup is editable.
    /// </summary>
    public bool IsEditable { get; }

    private static string? FitNoteFor(Position position, SkaterRole role) => SkaterFit.For(position, role) switch
    {
        PositionFit.OtherForwardPosition => "Out of position",
        PositionFit.AcrossForwardsAndDefence => position == Position.Defence ? "Defenceman at forward" : "Forward on defence",
        _ => null,
    };

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