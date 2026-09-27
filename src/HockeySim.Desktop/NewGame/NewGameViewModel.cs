using System.Buffers.Binary;
using System.Security.Cryptography;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Management.GameManagement;
using HockeySim.Management.NewGame;

namespace HockeySim.Desktop.NewGame;

public sealed partial class NewGameViewModel : ObservableObject
{
    private const int InitialSeasonYear = 2026;
    private const int MaximumGameNameLength = 80;
    private readonly GameManager _gameManager;
    private readonly Action _gameCreated;
    private readonly Action _showStartup;

    [ObservableProperty]
    private string _gameName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTeamDisplayName))]
    private string? _selectedTeamName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSetupVisible))]
    private bool _isGameCreated;

    [ObservableProperty]
    private string _createdGameName = string.Empty;

    [ObservableProperty]
    private string _createdTeamName = string.Empty;

    public NewGameViewModel(
        GameManager gameManager,
        Action? showStartup = null,
        Action? gameCreated = null)
    {
        ArgumentNullException.ThrowIfNull(gameManager);

        _gameManager = gameManager;
        _showStartup = showStartup ?? (() => { });
        _gameCreated = gameCreated ?? (() => { });

        var options = gameManager.GetNewGameOptions();
        Conferences = options.Conferences
            .Select(conference => new ConferenceOptionViewModel(
                conference.Name,
                conference.Divisions.Select(division => new DivisionOptionViewModel(
                    division.Name,
                    division.TeamNames.Select(teamName => new TeamOptionViewModel(teamName, SelectTeam))
                        .ToList()))
                    .ToList()))
            .ToList();
    }

    public IReadOnlyList<ConferenceOptionViewModel> Conferences { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsSetupVisible => !IsGameCreated;

    public string SelectedTeamDisplayName => SelectedTeamName ?? "No team selected";

    [RelayCommand]
    private void Back()
    {
        _showStartup();
    }

    [RelayCommand]
    private void CreateGame()
    {
        ErrorMessage = null;
        IsGameCreated = false;

        var normalizedGameName = GameName.Trim();
        if (normalizedGameName.Length == 0)
        {
            ErrorMessage = "Enter a name for this game.";
            return;
        }

        if (normalizedGameName.Length > MaximumGameNameLength)
        {
            ErrorMessage = $"Game names cannot be longer than {MaximumGameNameLength} characters.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedTeamName))
        {
            ErrorMessage = "Select the team you want to manage.";
            return;
        }

        try
        {
            var snapshot = _gameManager.StartNewGame(
                new NewGameCommand(
                    InitialSeasonYear,
                    CreateRandomState(),
                    SelectedTeamName));
            var managedTeam = snapshot.League.Teams.Single(team => team.Id == snapshot.ManagedTeamId);

            GameName = normalizedGameName;
            CreatedGameName = normalizedGameName;
            CreatedTeamName = managedTeam.Name;
            IsGameCreated = true;
            _gameCreated();
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    private void SelectTeam(string teamName)
    {
        SelectedTeamName = teamName;
        ErrorMessage = null;

        foreach (var team in Conferences
            .SelectMany(conference => conference.Divisions)
            .SelectMany(division => division.Teams))
        {
            team.IsSelected = string.Equals(team.Name, teamName, StringComparison.Ordinal);
        }
    }

    private static RandomState CreateRandomState()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(bytes);
        return new RandomState(BinaryPrimitives.ReadUInt64LittleEndian(bytes));
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

public sealed partial class TeamOptionViewModel : ObservableObject
{
    private readonly Action<string> _selectTeam;

    [ObservableProperty]
    private bool _isSelected;

    public TeamOptionViewModel(string name, Action<string> selectTeam)
    {
        Name = name;
        _selectTeam = selectTeam;
    }

    public string Name { get; }

    [RelayCommand]
    private void Select()
    {
        _selectTeam(Name);
    }
}