using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Lineups;

namespace HockeySim.Desktop.Lines;

/// <summary>
/// One team's lineup as shown on the Lines page: forward lines, defence pairs, goalies,
/// special-situation units, and extra attackers. For the managed team, edits stay local until
/// the page saves them through Management; other teams' lineups are read-only.
/// </summary>
public sealed partial class LineupEditorViewModel : ObservableObject
{
    private static readonly SpecialSituation[] PowerPlaySituations =
        [SpecialSituation.PowerPlay5On4, SpecialSituation.PowerPlay5On3, SpecialSituation.PowerPlay4On3];

    private static readonly SpecialSituation[] PenaltyKillSituations =
        [SpecialSituation.PenaltyKill4On5, SpecialSituation.PenaltyKill3On5, SpecialSituation.PenaltyKill3On4];

    private static readonly SpecialSituation[] OtherSituations =
        [SpecialSituation.FourOnFour, SpecialSituation.ThreeOnThree];

    private readonly LineupSnapshot _savedLineup;
    private readonly IReadOnlyList<PlayerOptionViewModel> _rosterOptions;
    private readonly Dictionary<PlayerId, PlayerOptionViewModel> _optionsById;
    private readonly IReadOnlyList<PlayerOptionViewModel> _skaterOptions;
    private readonly Dictionary<LineupSlotViewModel, IReadOnlyList<LineupSlotViewModel>> _swapGroups = [];
    private readonly List<LineupSlotViewModel> _dressedSlots = [];
    private readonly List<SpecialUnitViewModel> _units = [];
    private readonly Action _edited;
    private bool _isApplyingChange;

    [ObservableProperty]
    private IReadOnlyList<PlayerOptionViewModel> _scratches = [];

    [ObservableProperty]
    private bool _hasChanges;

    /// <param name="edited">Called after every change the user makes.</param>
    public LineupEditorViewModel(TeamSnapshot team, bool isEditable, Action edited)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(edited);

        TeamId = team.Id;
        IsEditable = isEditable;
        _edited = edited;
        _savedLineup = team.Lineup;
        _rosterOptions = team.Roster
            .OrderBy(player => player.LastName, StringComparer.Ordinal)
            .Select(player => new PlayerOptionViewModel(player))
            .ToList();
        _optionsById = _rosterOptions.ToDictionary(option => option.Id);

        // Any skater may fill any skater slot, so skater choices list every skater by natural position.
        _skaterOptions = _rosterOptions
            .Where(option => option.Position != Position.Goalie)
            .OrderBy(option => option.Position)
            .ToList();

        ForwardLines = _savedLineup.ForwardLines
            .Select((line, index) => new ForwardLineRowViewModel(
                $"LINE {index + 1}",
                DressedSlot("LW", SkaterRole.Wing, SkaterSide.Left, line.LeftWingId),
                DressedSlot("C", SkaterRole.Centre, side: null, line.CentreId),
                DressedSlot("RW", SkaterRole.Wing, SkaterSide.Right, line.RightWingId)))
            .ToList();
        DefencePairs = _savedLineup.DefencePairs
            .Select((pair, index) => new DefencePairRowViewModel(
                $"PAIR {index + 1}",
                DressedSlot("LD", SkaterRole.Defence, SkaterSide.Left, pair.LeftDefenceId),
                DressedSlot("RD", SkaterRole.Defence, SkaterSide.Right, pair.RightDefenceId)))
            .ToList();
        StartingGoalie = GoalieSlot("STARTER", _savedLineup.StartingGoalieId);
        BackupGoalie = GoalieSlot("BACKUP", _savedLineup.BackupGoalieId);
        AddSwapGroup(_dressedSlots);

        PowerPlay = CreateGroups(PowerPlaySituations);
        PenaltyKill = CreateGroups(PenaltyKillSituations);
        OtherSituationUnits = CreateGroups(OtherSituations);
        // The extra attacker joins the forwards, so the match engine plays them as a wing without a side.
        ExtraAttackers = _savedLineup.ExtraAttackerIds
            .Select((id, index) => Slot(
                index == 0 ? "1ST CHOICE" : "2ND CHOICE",
                _skaterOptions,
                id,
                SkaterRole.Wing,
                side: null))
            .ToList();
        AddSwapGroup(ExtraAttackers);

        UpdateScratches();
    }

    public TeamId TeamId { get; }

    public bool IsEditable { get; }

    public IReadOnlyList<ForwardLineRowViewModel> ForwardLines { get; }

    public IReadOnlyList<DefencePairRowViewModel> DefencePairs { get; }

    public LineupSlotViewModel StartingGoalie { get; }

    public LineupSlotViewModel BackupGoalie { get; }

    /// <summary>Gets the 5-on-4, 5-on-3, and 4-on-3 units.</summary>
    public IReadOnlyList<SpecialSituationGroupViewModel> PowerPlay { get; }

    /// <summary>Gets the 4-on-5, 3-on-5, and 3-on-4 units.</summary>
    public IReadOnlyList<SpecialSituationGroupViewModel> PenaltyKill { get; }

    /// <summary>Gets the 4-on-4 and 3-on-3 units.</summary>
    public IReadOnlyList<SpecialSituationGroupViewModel> OtherSituationUnits { get; }

    /// <summary>Gets the first- and second-choice extra attackers.</summary>
    public IReadOnlyList<LineupSlotViewModel> ExtraAttackers { get; }

    private IEnumerable<LineupSlotViewModel> UnitSlots => _units.SelectMany(unit => unit.Slots);

    // Every slot in the same order as SavedSlotOrder. The tabs list situations in enum order, so
    // units are in the snapshot's order.
    private IEnumerable<LineupSlotViewModel> Slots => _dressedSlots.Concat(UnitSlots).Concat(ExtraAttackers);

    /// <summary>
    /// Builds the command that sets this lineup.
    /// </summary>
    /// <exception cref="ArgumentException">A slot is empty, or a unit holds a scratched skater.</exception>
    public SetLineupCommand CreateCommand()
    {
        static PlayerId Id(LineupSlotViewModel slot) =>
            slot.SelectedPlayer?.Id ?? throw new ArgumentException($"Choose a player for every {slot.Label} slot.");

        var scratchedInUnit = UnitSlots.Concat(ExtraAttackers)
            .Select(slot => slot.SelectedPlayer)
            .FirstOrDefault(player => player?.IsScratched == true);
        if (scratchedInUnit is not null)
        {
            throw new ArgumentException(
                $"{scratchedInUnit.Name} is scratched. Only dressed skaters can play on special-situation units.");
        }

        return new SetLineupCommand(
            ForwardLines
                .Select(line => new ForwardLineSelection(Id(line.LeftWing), Id(line.Centre), Id(line.RightWing)))
                .ToList(),
            DefencePairs
                .Select(pair => new DefencePairSelection(Id(pair.LeftDefence), Id(pair.RightDefence)))
                .ToList(),
            Id(StartingGoalie),
            Id(BackupGoalie),
            _units
                .Select(unit => new SpecialSituationUnitSelection(
                    unit.Situation,
                    unit.Slots.Select(Id).ToList()))
                .ToList(),
            ExtraAttackers.Select(Id).ToList());
    }

    private LineupSlotViewModel Slot(
        string label,
        IReadOnlyList<PlayerOptionViewModel> options,
        PlayerId id,
        SkaterRole? role,
        SkaterSide? side) =>
        new(label, options, _optionsById[id], role, side, IsEditable, OnSlotChanged);

    private LineupSlotViewModel DressedSlot(string label, SkaterRole role, SkaterSide? side, PlayerId id)
    {
        var slot = Slot(label, _skaterOptions, id, role, side);
        _dressedSlots.Add(slot);
        return slot;
    }

    private LineupSlotViewModel GoalieSlot(string label, PlayerId id)
    {
        var options = _rosterOptions.Where(option => option.Position == Position.Goalie).ToList();
        var slot = Slot(label, options, id, role: null, side: null);
        _dressedSlots.Add(slot);
        return slot;
    }

    private IReadOnlyList<SpecialSituationGroupViewModel> CreateGroups(IEnumerable<SpecialSituation> situations) =>
        situations.Select(situation =>
        {
            var units = _savedLineup.UnitsFor(situation)
                .Select((unit, index) => CreateUnit(situation, index, unit))
                .ToList();
            return new SpecialSituationGroupViewModel(SituationTitle(situation), units);
        }).ToList();

    private SpecialUnitViewModel CreateUnit(SpecialSituation situation, int index, SpecialSituationUnitSnapshot unit)
    {
        var format = SpecialSituationFormat.For(situation);
        var roles = format.Roles;
        var slots = unit.PlayerIds
            .Select((id, slot) => Slot(
                SlotLabel(roles[slot], format.Sides[slot]),
                _skaterOptions,
                id,
                roles[slot],
                format.Sides[slot]))
            .ToList();
        var view = new SpecialUnitViewModel(
            situation,
            $"UNIT {index + 1}",
            slots,
            slots.Where((_, slot) => roles[slot] != SkaterRole.Defence).ToList(),
            slots.Where((_, slot) => roles[slot] == SkaterRole.Defence).ToList());
        _units.Add(view);
        AddSwapGroup(slots);
        return view;
    }

    private void AddSwapGroup(IReadOnlyList<LineupSlotViewModel> slots)
    {
        var group = slots.ToList();
        foreach (var slot in group)
        {
            _swapGroups[slot] = group;
        }
    }

    /// <summary>
    /// Choosing a player who already fills another slot of the same group (the dressed lineup, one
    /// unit, or the extra attackers) swaps the two, so no group holds a player twice. Dressing a
    /// scratched player in place of another also gives them the replaced player's unit slots, so
    /// the units keep only dressed skaters.
    /// </summary>
    private void OnSlotChanged(LineupSlotViewModel changedSlot, PlayerOptionViewModel? previousPlayer)
    {
        if (_isApplyingChange)
        {
            return;
        }

        var newPlayer = changedSlot.SelectedPlayer;
        var group = _swapGroups[changedSlot];
        _isApplyingChange = true;
        try
        {
            var otherSlot = group.FirstOrDefault(slot => slot != changedSlot && slot.SelectedPlayer == newPlayer);
            if (otherSlot is not null && newPlayer is not null)
            {
                otherSlot.SelectedPlayer = previousPlayer;
            }
            else if (group == _swapGroups[StartingGoalie] && previousPlayer is not null && newPlayer is not null)
            {
                ReplaceInUnits(previousPlayer, newPlayer);
            }
        }
        finally
        {
            _isApplyingChange = false;
        }

        HasChanges = !Slots.Select(slot => slot.SelectedPlayer?.Id)
            .SequenceEqual(SavedSlotOrder().Select(id => (PlayerId?)id));
        UpdateScratches();
        _edited();
    }

    private void ReplaceInUnits(PlayerOptionViewModel scratched, PlayerOptionViewModel dressed)
    {
        var groups = _units.Select(unit => unit.Slots).Append(ExtraAttackers);
        foreach (var slots in groups.Where(slots => slots.All(slot => slot.SelectedPlayer != dressed)))
        {
            foreach (var slot in slots.Where(slot => slot.SelectedPlayer == scratched))
            {
                slot.SelectedPlayer = dressed;
            }
        }
    }

    private void UpdateScratches()
    {
        var dressed = _dressedSlots.Select(slot => slot.SelectedPlayer).ToHashSet();
        foreach (var option in _rosterOptions)
        {
            option.IsScratched = !dressed.Contains(option);
        }

        Scratches = _rosterOptions.Where(player => player.IsScratched)
            .OrderBy(player => player.Position)
            .ToList();
    }

    // Matches the order of the Slots property.
    private IEnumerable<PlayerId> SavedSlotOrder() =>
        _savedLineup.ForwardLines.SelectMany(line => new[] { line.LeftWingId, line.CentreId, line.RightWingId })
            .Concat(_savedLineup.DefencePairs.SelectMany(pair => new[] { pair.LeftDefenceId, pair.RightDefenceId }))
            .Append(_savedLineup.StartingGoalieId)
            .Append(_savedLineup.BackupGoalieId)
            .Concat(_savedLineup.SpecialSituationUnits.SelectMany(unit => unit.PlayerIds))
            .Concat(_savedLineup.ExtraAttackerIds);

    /// <summary>
    /// Names a unit slot by its role, with its side when the unit has two wings or two defence players.
    /// </summary>
    private static string SlotLabel(SkaterRole role, SkaterSide? side) => (role, side) switch
    {
        (SkaterRole.Centre, _) => "C",
        (SkaterRole.Wing, SkaterSide.Left) => "LW",
        (SkaterRole.Wing, SkaterSide.Right) => "RW",
        (SkaterRole.Wing, _) => "W",
        (_, SkaterSide.Left) => "LD",
        (_, SkaterSide.Right) => "RD",
        _ => "D",
    };

    private static string SituationTitle(SpecialSituation situation) => situation switch
    {
        SpecialSituation.PowerPlay5On4 => "5 ON 4",
        SpecialSituation.PowerPlay5On3 => "5 ON 3",
        SpecialSituation.PowerPlay4On3 => "4 ON 3",
        SpecialSituation.PenaltyKill4On5 => "4 ON 5",
        SpecialSituation.PenaltyKill3On5 => "3 ON 5",
        SpecialSituation.PenaltyKill3On4 => "3 ON 4",
        SpecialSituation.FourOnFour => "4 ON 4",
        SpecialSituation.ThreeOnThree => "3 ON 3",
        _ => throw new ArgumentOutOfRangeException(nameof(situation)),
    };
}