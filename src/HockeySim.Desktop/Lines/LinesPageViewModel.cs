using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Teams;
using HockeySim.Domain;

namespace HockeySim.Desktop.Lines;

/// <summary>
/// Shows any team's lineup and edits the managed team's: forward lines, defence pairs, goalies,
/// special-situation units, and extra attackers. Edits stay local until saved, when Management
/// validates and applies the whole lineup at once. Browsing another team keeps unsaved edits.
/// </summary>
public sealed partial class LinesPageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;
    private LineupEditorViewModel _managedLineup;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle), nameof(IsEditable), nameof(OwnershipNote))]
    private TeamEntryViewModel _selectedTeam;

    [ObservableProperty]
    private LineupEditorViewModel _lineup;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEvenStrengthTab), nameof(IsPowerPlayTab), nameof(IsPenaltyKillTab), nameof(IsOtherTab))]
    private LinesTab _tab = LinesTab.EvenStrength;

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
        Teams = session.Snapshot.League.Conferences
            .SelectMany(conference => conference.Divisions)
            .SelectMany(division => division.Teams.Select(team => new TeamEntryViewModel(
                team.Id,
                team.Name,
                division.Name,
                team.Id == session.Snapshot.ManagedTeamId)))
            .ToList();
        _selectedTeam = Teams.Single(team => team.IsManaged);
        _managedLineup = CreateManagedLineup();
        _lineup = _managedLineup;
    }

    public override string Title => "Lines";

    public override string Subtitle => SelectedTeam.Name;

    /// <summary>
    /// Gets every team in league order: by conference, then division.
    /// </summary>
    public IReadOnlyList<TeamEntryViewModel> Teams { get; }

    public bool IsEditable => SelectedTeam.IsManaged;

    public string OwnershipNote => IsEditable
        ? "Your team"
        : "Read-only";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsEvenStrengthTab => Tab == LinesTab.EvenStrength;

    public bool IsPowerPlayTab => Tab == LinesTab.PowerPlay;

    public bool IsPenaltyKillTab => Tab == LinesTab.PenaltyKill;

    public bool IsOtherTab => Tab == LinesTab.Other;

    public void SelectTeam(TeamId teamId)
    {
        SelectedTeam = Teams.Single(team => team.Id == teamId);
    }

    public override void Refresh()
    {
        // Keep unsaved edits when unrelated game state changes, such as reading a message.
        if (!HasChanges)
        {
            _managedLineup = CreateManagedLineup();
        }

        ShowSelectedTeam();
    }

    [RelayCommand]
    private void SelectTab(LinesTab tab)
    {
        Tab = tab;
    }

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void Save()
    {
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            _session.SetLineup(_managedLineup.CreateCommand());
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = $"The lineup was not saved. {exception.Message}";
            return;
        }

        ReloadManagedLineup();
        StatusMessage = "Lineup saved.";
    }

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void Revert()
    {
        ReloadManagedLineup();
        StatusMessage = "Changes discarded.";
    }

    partial void OnSelectedTeamChanged(TeamEntryViewModel value)
    {
        ShowSelectedTeam();
    }

    private void ReloadManagedLineup()
    {
        _managedLineup = CreateManagedLineup();
        ErrorMessage = null;
        ShowSelectedTeam();
    }

    private void ShowSelectedTeam()
    {
        Lineup = SelectedTeam.IsManaged
            ? _managedLineup
            : new LineupEditorViewModel(_session.GetTeam(SelectedTeam.Id), isEditable: false, () => { });
    }

    private LineupEditorViewModel CreateManagedLineup()
    {
        var lineup = new LineupEditorViewModel(_session.ManagedTeam, isEditable: true, OnManagedLineupEdited);
        HasChanges = false;
        return lineup;
    }

    private void OnManagedLineupEdited()
    {
        StatusMessage = null;
        ErrorMessage = null;
        HasChanges = _managedLineup.HasChanges;
    }
}

public enum LinesTab
{
    EvenStrength,
    PowerPlay,
    PenaltyKill,
    Other,
}