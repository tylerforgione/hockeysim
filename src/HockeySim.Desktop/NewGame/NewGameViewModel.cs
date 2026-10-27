using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Players;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.NewGame;
using HockeySim.Simulation.Randomness;

using SaveNameRules = HockeySim.Management.Saves.SaveName;

namespace HockeySim.Desktop.NewGame;

/// <summary>
/// New-game setup in three steps: the league (and its world seed), the team, and the manager. The
/// team step shows a league generated from the seed without starting a game, and starting the
/// game then creates exactly that league.
/// </summary>
public sealed partial class NewGameViewModel : ObservableObject
{
    private const int InitialSeasonYear = 2026;
    private readonly GameManager _gameManager;
    private readonly Action<string> _gameCreated;
    private readonly Action _showStartup;
    private readonly IReadOnlyList<NewGameStepViewModel> _steps;

    private NewGamePreviewSnapshot? _preview;
    private RandomState _randomState;

    // The seed text the preview was generated from, so an unchanged seed keeps the preview.
    private string? _generatedSeedText;
    private bool _saveNameEdited;
    private bool _settingSaveName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLeagueStep))]
    [NotifyPropertyChangedFor(nameof(IsTeamStep))]
    [NotifyPropertyChangedFor(nameof(IsManagerStep))]
    [NotifyPropertyChangedFor(nameof(IsNextVisible))]
    [NotifyCanExecuteChangedFor(nameof(BackCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private NewGameStep _step = NewGameStep.League;

    [ObservableProperty]
    private string _worldSeedText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<ConferenceOptionViewModel> _conferences = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedTeam))]
    [NotifyPropertyChangedFor(nameof(SummaryTeam))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private TeamPreviewViewModel? _selectedTeam;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryManager))]
    private string _managerName = string.Empty;

    [ObservableProperty]
    private string _saveName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummarySeed))]
    private bool _hasPreview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public NewGameViewModel(
        GameManager gameManager,
        Action? showStartup = null,
        Action<string>? gameCreated = null)
    {
        ArgumentNullException.ThrowIfNull(gameManager);

        _gameManager = gameManager;
        _showStartup = showStartup ?? (() => { });
        _gameCreated = gameCreated ?? (_ => { });

        var options = gameManager.GetNewGameOptions();
        var teamCount = options.Conferences.Sum(conference => conference.Divisions.Sum(division => division.TeamNames.Count));
        var divisionCount = options.Conferences.Sum(conference => conference.Divisions.Count);
        LeagueSummary = $"{teamCount} teams · {options.Conferences.Count} conferences · {divisionCount} divisions · generated players";

        _steps =
        [
            new NewGameStepViewModel(1, "League", NewGameStep.League, GoToStep),
            new NewGameStepViewModel(2, "Team", NewGameStep.Team, GoToStep),
            new NewGameStepViewModel(3, "Manager", NewGameStep.Manager, GoToStep),
        ];
        RefreshSteps();
    }

    /// <summary>The League, Team, and Manager step headers, in order.</summary>
    public IReadOnlyList<NewGameStepViewModel> Steps => _steps;

    public bool IsLeagueStep => Step == NewGameStep.League;

    public bool IsTeamStep => Step == NewGameStep.Team;

    public bool IsManagerStep => Step == NewGameStep.Manager;

    /// <summary>Next stands in for Start game on the last step.</summary>
    public bool IsNextVisible => Step != NewGameStep.Manager;

    public int MaximumManagerNameLength => NewGameCommand.MaximumManagerNameLength;

    public int MaximumSaveNameLength => SaveNameRules.MaximumLength;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasSelectedTeam => SelectedTeam is not null;

    public string LeagueSummary { get; }

    public string SeasonLabel => PlayerDisplay.FormatSeason(InitialSeasonYear);

    public string SummaryLeague => $"Fictional · {SeasonLabel}";

    /// <summary>The seed the league is generated from, so a random one can be read and reused.</summary>
    public string SummarySeed => HasPreview ? _randomState.Value.ToString(CultureInfo.CurrentCulture) : string.Empty;

    public string SummaryTeam => SelectedTeam?.Name ?? string.Empty;

    public string SummaryManager => ManagerName.Trim();

    partial void OnStepChanged(NewGameStep value)
    {
        RefreshSteps();
    }

    partial void OnHasPreviewChanged(bool value)
    {
        RefreshSteps();
    }

    partial void OnSelectedTeamChanged(TeamPreviewViewModel? value)
    {
        RefreshSteps();
    }

    partial void OnSaveNameChanged(string value)
    {
        if (!_settingSaveName)
        {
            _saveNameEdited = true;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _showStartup();
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        ErrorMessage = null;
        Step = Step - 1;
    }

    private bool CanGoBack() => Step != NewGameStep.League;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        ErrorMessage = null;
        if (Step == NewGameStep.League && !TryGeneratePreview())
        {
            return;
        }

        Step = Step + 1;
    }

    private bool CanGoNext() => Step switch
    {
        NewGameStep.League => true,
        NewGameStep.Team => SelectedTeam is not null,
        _ => false,
    };

    [RelayCommand]
    private void StartGame()
    {
        ErrorMessage = null;

        var team = SelectedTeam;
        if (team is null || _preview is null)
        {
            ErrorMessage = "Select the team you want to manage.";
            return;
        }

        var managerName = ManagerName.Trim();
        if (managerName.Length == 0)
        {
            ErrorMessage = "Enter your name.";
            return;
        }

        // The name is the save dialog's first suggestion, so it must already be a valid save name.
        if (!SaveNameRules.TryParse(SaveName, out var saveName, out var error))
        {
            ErrorMessage = error;
            return;
        }

        try
        {
            _gameManager.StartNewGame(new NewGameCommand(InitialSeasonYear, _randomState, team.Name, managerName));
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = exception.Message;
            return;
        }

        SetSaveName(saveName.Value);
        _gameCreated(saveName.Value);
    }

    /// <summary>Moves to a step the user may reach; leaving the league step first builds the preview.</summary>
    private void GoToStep(NewGameStep target)
    {
        if (target == Step || !IsReachable(target))
        {
            return;
        }

        ErrorMessage = null;
        if (Step == NewGameStep.League && target != NewGameStep.League)
        {
            if (!TryGeneratePreview())
            {
                return;
            }

            // The preview may be new, and a team only carries over if it still exists.
            if (target == NewGameStep.Manager && SelectedTeam is null)
            {
                return;
            }
        }

        Step = target;
    }

    private bool IsReachable(NewGameStep target) => target switch
    {
        NewGameStep.League => true,
        NewGameStep.Team => HasPreview,
        _ => SelectedTeam is not null,
    };

    /// <summary>
    /// Generates the league for the world seed, unless the seed is unchanged since the last
    /// generation, in which case the existing preview and random state stay.
    /// </summary>
    private bool TryGeneratePreview()
    {
        var seedText = WorldSeedText.Trim();
        if (_preview is not null && seedText == _generatedSeedText)
        {
            return true;
        }

        RandomState randomState;
        if (seedText.Length == 0)
        {
            randomState = NewGameCommand.FreshRandomState();
        }
        else if (ulong.TryParse(seedText, NumberStyles.None, CultureInfo.InvariantCulture, out var seed))
        {
            randomState = new RandomState(seed);
        }
        else
        {
            ErrorMessage = $"The world seed must be a whole number from 0 to {ulong.MaxValue}.";
            return false;
        }

        _preview = _gameManager.PreviewNewGame(InitialSeasonYear, randomState);
        _randomState = randomState;
        _generatedSeedText = seedText;
        BuildConferences(_preview);
        HasPreview = true;
        OnPropertyChanged(nameof(SummarySeed));
        return true;
    }

    private void BuildConferences(NewGamePreviewSnapshot preview)
    {
        var previousSelection = SelectedTeam?.Name;
        Conferences = preview.Conferences
            .Select(conference => new ConferenceOptionViewModel(
                conference.Name,
                conference.Divisions.Select(division => new DivisionOptionViewModel(
                    division.Name,
                    division.Teams.Select(team => new TeamOptionViewModel(team, SelectTeam)).ToList()))
                    .ToList()))
            .ToList();

        // Team names are fixed data, so the chosen team carries over to the new league.
        var carriedOver = previousSelection is null
            ? null
            : preview.Teams.FirstOrDefault(team => team.Name == previousSelection);
        SelectedTeam = null;
        if (carriedOver is not null)
        {
            SelectTeam(carriedOver);
        }
    }

    private void SelectTeam(NewGamePreviewTeamSnapshot team)
    {
        ErrorMessage = null;
        SelectedTeam = new TeamPreviewViewModel(team);

        foreach (var option in Conferences
            .SelectMany(conference => conference.Divisions)
            .SelectMany(division => division.Teams))
        {
            option.IsSelected = string.Equals(option.Name, team.Name, StringComparison.Ordinal);
        }

        // The save name follows the team until the user types one of their own.
        if (!_saveNameEdited)
        {
            SetSaveName(team.Name);
        }
    }

    private void SetSaveName(string name)
    {
        _settingSaveName = true;
        SaveName = name;
        _settingSaveName = false;
    }

    private void RefreshSteps()
    {
        foreach (var step in _steps)
        {
            step.Update(Step, IsReachable(step.Target));
        }
    }
}

public enum NewGameStep
{
    League,
    Team,
    Manager,
}

/// <summary>A step header: its number (a tick once done), title, and whether it can be opened.</summary>
public sealed partial class NewGameStepViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    private bool _isDone;

    [ObservableProperty]
    private string _marker;

    [ObservableProperty]
    private bool _isReachable;

    private readonly int _number;

    public NewGameStepViewModel(int number, string title, NewGameStep target, Action<NewGameStep> open)
    {
        _number = number;
        _marker = number.ToString(CultureInfo.CurrentCulture);
        Title = title;
        Target = target;
        OpenCommand = new RelayCommand(() => open(target), () => IsReachable);
    }

    public string Title { get; }

    public NewGameStep Target { get; }

    public RelayCommand OpenCommand { get; }

    internal void Update(NewGameStep current, bool isReachable)
    {
        IsCurrent = current == Target;
        IsDone = Target < current;
        Marker = IsDone ? "✓" : _number.ToString(CultureInfo.CurrentCulture);
        IsReachable = isReachable;
        OpenCommand.NotifyCanExecuteChanged();
    }
}

public sealed class ConferenceOptionViewModel
{
    public ConferenceOptionViewModel(string name, IReadOnlyList<DivisionOptionViewModel> divisions)
    {
        Name = name;
        Divisions = divisions;
    }

    public string Name { get; }

    public IReadOnlyList<DivisionOptionViewModel> Divisions { get; }
}

public sealed class DivisionOptionViewModel
{
    public DivisionOptionViewModel(string name, IReadOnlyList<TeamOptionViewModel> teams)
    {
        Name = name;
        Teams = teams;
    }

    public string Name { get; }

    public IReadOnlyList<TeamOptionViewModel> Teams { get; }
}

/// <summary>A team's row in the league list: its colours, name, overall, and outlook.</summary>
public sealed partial class TeamOptionViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public TeamOptionViewModel(NewGamePreviewTeamSnapshot team, Action<NewGamePreviewTeamSnapshot> selectTeam)
    {
        Name = team.Name;
        Colours = team.Colours;
        Overall = team.Overall;
        OutlookText = TeamPreviewViewModel.OutlookName(team.Outlook);
        SelectCommand = new RelayCommand(() => selectTeam(team));
    }

    public string Name { get; }

    public TeamColours Colours { get; }

    public int Overall { get; }

    public string OutlookText { get; }

    public RelayCommand SelectCommand { get; }
}

/// <summary>The preview panel for the selected team, painted in its colours.</summary>
public sealed class TeamPreviewViewModel
{
    public TeamPreviewViewModel(NewGamePreviewTeamSnapshot team)
    {
        Name = team.Name;
        Initials = PlayerDisplay.TeamInitials(team.Name);
        Colours = team.Colours;
        Location = $"{team.DivisionName} · {team.ConferenceName}";
        Overall = team.Overall;
        OutlookText = OutlookName(team.Outlook);
        AverageAgeText = team.AverageAge.ToString("0.0", CultureInfo.CurrentCulture);
        KeyPlayers = team.KeyPlayers.Select(player => new KeyPlayerViewModel(player)).ToList();
        Ratings =
        [
            new RatingBarViewModel("Forwards", team.Forwards),
            new RatingBarViewModel("Defence", team.Defence),
            new RatingBarViewModel("Goalies", team.Goalies),
        ];
    }

    public string Name { get; }

    public string Initials { get; }

    public TeamColours Colours { get; }

    public Colour PrimaryColour => Colours.Primary;

    public Colour SecondaryColour => Colours.Secondary;

    /// <summary>Division and conference, such as "Pacific Division · Western Conference".</summary>
    public string Location { get; }

    public int Overall { get; }

    public string OutlookText { get; }

    public string AverageAgeText { get; }

    public IReadOnlyList<KeyPlayerViewModel> KeyPlayers { get; }

    public IReadOnlyList<RatingBarViewModel> Ratings { get; }

    public static string OutlookName(TeamOutlook outlook) => outlook switch
    {
        TeamOutlook.Contender => "Contender",
        TeamOutlook.PlayoffBubble => "Playoff bubble",
        TeamOutlook.Middle => "Middle",
        TeamOutlook.Rebuild => "Rebuild",
        _ => throw new ArgumentOutOfRangeException(nameof(outlook), outlook, "Unknown outlook."),
    };
}

public sealed record KeyPlayerViewModel(string Position, string Number, string Name, int Overall)
{
    public KeyPlayerViewModel(NewGamePreviewPlayerSnapshot player)
        : this(
            PlayerDisplay.PositionAbbreviation(player.Position),
            player.Number.ToString(CultureInfo.CurrentCulture),
            $"{player.FirstName} {player.LastName}",
            player.Overall)
    {
    }
}

public sealed record RatingBarViewModel(string Label, int Value);