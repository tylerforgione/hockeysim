using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Lineups;

namespace HockeySim.Desktop.Lines;

/// <summary>
/// Edits the managed team's forward lines, defence pairs, and goalies. Edits stay local until
/// saved, when Management validates and applies the whole lineup at once.
/// </summary>
public sealed partial class LinesPageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;
    private bool _isApplyingSwap;
    private LineupSnapshot _savedLineup;
    private IReadOnlyList<PlayerOptionViewModel> _rosterOptions = [];

    [ObservableProperty]
    private IReadOnlyList<ForwardLineRowViewModel> _forwardLines = [];

    [ObservableProperty]
    private IReadOnlyList<DefencePairRowViewModel> _defencePairs = [];

    [ObservableProperty]
    private LineupSlotViewModel? _startingGoalie;

    [ObservableProperty]
    private LineupSlotViewModel? _backupGoalie;

    [ObservableProperty]
    private IReadOnlyList<PlayerOptionViewModel> _scratches = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(RevertCommand))]
    private bool _hasChanges;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    public LinesPageViewModel(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _savedLineup = session.ManagedTeam.Lineup;
        Load();
    }

    public override string Title => "Lines";

    public override string Subtitle => $"{_session.ManagedTeam.Name} · 4 forward lines · 3 defence pairs · 2 goalies";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    private IEnumerable<LineupSlotViewModel> Slots =>
        ForwardLines.SelectMany(line => new[] { line.LeftWing, line.Centre, line.RightWing })
            .Concat(DefencePairs.SelectMany(pair => new[] { pair.LeftDefence, pair.RightDefence }))
            .Concat(new[] { StartingGoalie, BackupGoalie }.OfType<LineupSlotViewModel>());

    public override void Refresh()
    {
        // Keep unsaved edits when unrelated game state changes, such as reading a message.
        if (!HasChanges)
        {
            Load();
        }
    }

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void Save()
    {
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            _session.SetLineup(CreateCommand());
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = $"The lineup was not saved. {exception.Message}";
            return;
        }

        Load();
        StatusMessage = "Lineup saved.";
    }

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void Revert()
    {
        Load();
        StatusMessage = "Changes discarded.";
    }

    private void Load()
    {
        var team = _session.ManagedTeam;
        _savedLineup = team.Lineup;
        var options = team.Roster
            .OrderBy(player => player.LastName, StringComparer.Ordinal)
            .Select(player => new PlayerOptionViewModel(player))
            .ToList();
        _rosterOptions = options;
        var byId = options.ToDictionary(option => option.Id);
        IReadOnlyList<PlayerOptionViewModel> OptionsFor(Position position) =>
            options.Where(option => option.Position == position).ToList();
        var centres = OptionsFor(Position.Centre);
        var wings = OptionsFor(Position.Wing);
        var defence = OptionsFor(Position.Defence);
        var goalies = OptionsFor(Position.Goalie);

        LineupSlotViewModel Slot(string label, IReadOnlyList<PlayerOptionViewModel> slotOptions, PlayerId id) =>
            new(label, slotOptions, byId[id], OnSlotChanged);

        ForwardLines = _savedLineup.ForwardLines
            .Select((line, index) => new ForwardLineRowViewModel(
                $"LINE {index + 1}",
                Slot("LW", wings, line.LeftWingId),
                Slot("C", centres, line.CentreId),
                Slot("RW", wings, line.RightWingId)))
            .ToList();
        DefencePairs = _savedLineup.DefencePairs
            .Select((pair, index) => new DefencePairRowViewModel(
                $"PAIR {index + 1}",
                Slot("LD", defence, pair.LeftDefenceId),
                Slot("RD", defence, pair.RightDefenceId)))
            .ToList();
        StartingGoalie = Slot("STARTER", goalies, _savedLineup.StartingGoalieId);
        BackupGoalie = Slot("BACKUP", goalies, _savedLineup.BackupGoalieId);

        ErrorMessage = null;
        HasChanges = false;
        UpdateScratches();
    }

    /// <summary>
    /// Choosing a player who already fills another slot swaps the two, so the lineup never
    /// dresses the same player twice.
    /// </summary>
    private void OnSlotChanged(LineupSlotViewModel changedSlot, PlayerOptionViewModel? previousPlayer)
    {
        if (_isApplyingSwap)
        {
            return;
        }

        var newPlayer = changedSlot.SelectedPlayer;
        var otherSlot = Slots.FirstOrDefault(slot => slot != changedSlot && slot.SelectedPlayer == newPlayer);
        if (otherSlot is not null && newPlayer is not null)
        {
            _isApplyingSwap = true;
            try
            {
                otherSlot.SelectedPlayer = previousPlayer;
            }
            finally
            {
                _isApplyingSwap = false;
            }
        }

        StatusMessage = null;
        ErrorMessage = null;
        HasChanges = !Slots.Select(slot => slot.SelectedPlayer?.Id)
            .SequenceEqual(SlotOrder(_savedLineup).Select(id => (PlayerId?)id));
        UpdateScratches();
    }

    private void UpdateScratches()
    {
        var dressed = Slots.Select(slot => slot.SelectedPlayer).ToHashSet();
        Scratches = _rosterOptions.Where(player => !dressed.Contains(player))
            .OrderBy(player => player.Position)
            .ToList();
    }

    private SetLineupCommand CreateCommand()
    {
        static PlayerId Id(LineupSlotViewModel slot) =>
            slot.SelectedPlayer?.Id ?? throw new ArgumentException($"Choose a player for every {slot.Label} slot.");

        return new SetLineupCommand(
            ForwardLines
                .Select(line => new ForwardLineSelection(Id(line.LeftWing), Id(line.Centre), Id(line.RightWing)))
                .ToList(),
            DefencePairs
                .Select(pair => new DefencePairSelection(Id(pair.LeftDefence), Id(pair.RightDefence)))
                .ToList(),
            Id(StartingGoalie!),
            Id(BackupGoalie!));
    }

    // Matches the order of the Slots property.
    private static IEnumerable<PlayerId> SlotOrder(LineupSnapshot lineup) =>
        lineup.ForwardLines.SelectMany(line => new[] { line.LeftWingId, line.CentreId, line.RightWingId })
            .Concat(lineup.DefencePairs.SelectMany(pair => new[] { pair.LeftDefenceId, pair.RightDefenceId }))
            .Append(lineup.StartingGoalieId)
            .Append(lineup.BackupGoalieId);
}