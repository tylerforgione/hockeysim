using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Teams;
using HockeySim.Domain;

namespace HockeySim.Desktop.Lines;

/// <summary>
/// Shows any team's lineup and edits the managed team's: forward lines, defence pairs, goalies,
/// special-situation units, and extra attackers. Edits stay local until saved, when Management
/// validates and applies the whole lineup at once. Browsing another team keeps unsaved edits. The
/// same view model, fixed to one team, is the Lines tab of a team page.
/// </summary>
public sealed partial class LinesPageViewModel : ShellPageViewModel, ISelectablePage, IDisposable
{
    private readonly GameSession _session;
    private readonly Action<PlayerId>? _openPlayer;
    private readonly ManagedLineupEdits _edits;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle), nameof(IsEditable), nameof(OwnershipNote))]
    private TeamEntryViewModel _selectedTeam;

    [ObservableProperty]
    private LineupEditorViewModel _lineup;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEvenStrengthTab), nameof(IsPowerPlayTab), nameof(IsPenaltyKillTab), nameof(IsOtherTab))]
    private LinesTab _tab = LinesTab.EvenStrength;

    /// <param name="openPlayer">Opens a player's profile; without it the profile cannot be opened from here.</param>
    /// <param name="edits">
    /// The managed team's edits, shared with other pages showing them; the page keeps its own without it.
    /// </param>
    /// <param name="fixedTeam">
    /// The one team to show, with no team choice, as on a team page; the managed team is shown, with a choice, without it.
    /// </param>
    public LinesPageViewModel(GameSession session, Action<PlayerId>? openPlayer = null, ManagedLineupEdits? edits = null, TeamId? fixedTeam = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _openPlayer = openPlayer;
        _edits = edits ?? new ManagedLineupEdits(session);
        Teams = TeamEntryViewModel.ForLeague(session.Snapshot);
        ShowsTeamChoice = fixedTeam is null;
        _selectedTeam = fixedTeam is { } team ? Teams.Single(entry => entry.Id == team) : Teams.Single(entry => entry.IsManaged);
        _lineup = SelectedLineup();
        _edits.Changed += OnEditsChanged;
    }

    public event EventHandler? SelectionChanged;

    public override string Subtitle => SelectedTeam.Name;

    /// <summary>
    /// Gets every team in league order: by conference, then division.
    /// </summary>
    public IReadOnlyList<TeamEntryViewModel> Teams { get; }

    /// <summary>Gets whether any team can be chosen; a team page shows its own team only.</summary>
    public bool ShowsTeamChoice { get; }

    public bool IsEditable => SelectedTeam.IsManaged;

    public bool HasChanges => _edits.HasChanges;

    public string? ErrorMessage => _edits.ErrorMessage;

    public string? StatusMessage => _edits.StatusMessage;

    public string OwnershipNote => IsEditable
        ? "Your team"
        : "Read-only";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsEvenStrengthTab => Tab == LinesTab.EvenStrength;

    public bool IsPowerPlayTab => Tab == LinesTab.PowerPlay;

    public bool IsPenaltyKillTab => Tab == LinesTab.PenaltyKill;

    public bool IsOtherTab => Tab == LinesTab.Other;

    object ISelectablePage.Selection => SelectedTeam.Id;

    public void SelectTeam(TeamId teamId)
    {
        SelectedTeam = Teams.Single(team => team.Id == teamId);
    }

    void ISelectablePage.Select(object selection)
    {
        SelectedTeam = Teams.FirstOrDefault(team => selection is TeamId id && team.Id == id) ?? Teams.Single(team => team.IsManaged);
    }

    public override void Refresh()
    {
        // Keep unsaved edits when unrelated game state changes, such as reading a message.
        _edits.Refresh();
        ShowSelectedTeam();
    }

    [RelayCommand]
    private void SelectTab(LinesTab tab)
    {
        Tab = tab;
    }

    [RelayCommand(CanExecute = nameof(CanOpenPlayer))]
    private void OpenPlayer(PlayerOptionViewModel player)
    {
        _openPlayer!(player.Id);
    }

    private bool CanOpenPlayer() => _openPlayer is not null;

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void Save()
    {
        _edits.Save();
    }

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void Revert()
    {
        _edits.Revert();
    }

    /// <summary>Stops following the shared edits, once a team page showing this page is replaced.</summary>
    public void Dispose()
    {
        _edits.Changed -= OnEditsChanged;
    }

    partial void OnSelectedTeamChanged(TeamEntryViewModel value)
    {
        ShowSelectedTeam();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShowSelectedTeam()
    {
        Lineup = SelectedLineup();
    }

    private LineupEditorViewModel SelectedLineup() =>
        SelectedTeam.IsManaged
            ? _edits.Editor
            : new LineupEditorViewModel(_session.GetTeam(SelectedTeam.Id), isEditable: false, () => { });

    private void OnEditsChanged(object? sender, EventArgs e)
    {
        if (SelectedTeam.IsManaged)
        {
            ShowSelectedTeam();
        }

        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(StatusMessage));
        SaveCommand.NotifyCanExecuteChanged();
        RevertCommand.NotifyCanExecuteChanged();
    }
}

public enum LinesTab
{
    EvenStrength,
    PowerPlay,
    PenaltyKill,
    Other,
}