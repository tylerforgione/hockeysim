using System.Buffers.Binary;
using System.Security.Cryptography;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Players;
using HockeySim.Management.GameManagement;
using HockeySim.Management.NewGame;

namespace HockeySim.Desktop.NewGame;

public sealed partial class NewGameViewModel : ObservableObject
{
    private const int InitialSeasonYear = 2026;
    private const int MaximumGameNameLength = 80;
    private readonly GameManager _gameManager;
    private readonly Action<string> _gameCreated;
    private readonly Action _showStartup;

    [ObservableProperty]
    private string _gameName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTeamDisplayName))]
    [NotifyPropertyChangedFor(nameof(SelectedTeamInitials))]
    [NotifyPropertyChangedFor(nameof(SelectedTeamDivision))]
    [NotifyPropertyChangedFor(nameof(HasSelectedTeam))]
    private string? _selectedTeamName;

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

    public string SelectedTeamDisplayName => SelectedTeamName ?? "No team selected";

    public bool HasSelectedTeam => SelectedTeamName is not null;

    public string SelectedTeamInitials => SelectedTeamName is null ? "?" : PlayerDisplay.TeamInitials(SelectedTeamName);

    public string SelectedTeamDivision
    {
        get
        {
            foreach (var conference in Conferences)
            {
                var division = conference.Divisions.FirstOrDefault(
                    division => division.Teams.Any(team => team.Name == SelectedTeamName));
                if (division is not null)
                {
                    return $"{division.Name} · {conference.Name}";
                }
            }

            return "Choose a club from the league";
        }
    }

    public string SeasonLabel => $"{PlayerDisplay.FormatSeason(InitialSeasonYear)} season";

    public string LeagueSummary =>
        $"{Conferences.Sum(conference => conference.Divisions.Sum(division => division.Teams.Count))} teams · "
        + $"{Conferences.Count} conferences · {Conferences.Sum(conference => conference.Divisions.Count)} divisions";

    [RelayCommand]
    private void Back()
    {
        _showStartup();
    }

    [RelayCommand]
    private void CreateGame()
    {
        ErrorMessage = null;

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
            _gameManager.StartNewGame(
                new NewGameCommand(
                    InitialSeasonYear,
                    CreateRandomState(),
                    SelectedTeamName));
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = exception.Message;
            return;
        }

        GameName = normalizedGameName;
        _gameCreated(normalizedGameName);
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