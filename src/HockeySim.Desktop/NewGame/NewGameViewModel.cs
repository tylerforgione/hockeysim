using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Management.GameManagement;
using HockeySim.Management.NewGame;

namespace HockeySim.Desktop.NewGame;

public sealed partial class NewGameViewModel : ObservableObject
{
    private const int MaximumSeasonYear = 9999;
    private readonly GameManager _gameManager;

    [ObservableProperty]
    private string _seasonYear = "2026";

    [ObservableProperty]
    private string _worldSeed = "2026";

    [ObservableProperty]
    private string? _selectedTeamName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSetupPreviewVisible))]
    private bool _isGameCreated;

    [ObservableProperty]
    private string _createdTeamName = string.Empty;

    [ObservableProperty]
    private string _createdSeason = string.Empty;

    [ObservableProperty]
    private int _createdTeamCount;

    [ObservableProperty]
    private int _createdPlayerCount;

    public NewGameViewModel(GameManager gameManager)
    {
        ArgumentNullException.ThrowIfNull(gameManager);

        _gameManager = gameManager;
        TeamNames = gameManager.GetNewGameOptions().TeamNames;
        _selectedTeamName = TeamNames.FirstOrDefault();
    }

    public IReadOnlyList<string> TeamNames { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsSetupPreviewVisible => !IsGameCreated;

    [RelayCommand]
    private void CreateGame()
    {
        ErrorMessage = null;
        IsGameCreated = false;

        if (!int.TryParse(SeasonYear, NumberStyles.None, CultureInfo.InvariantCulture, out var seasonYear)
            || seasonYear is < 1 or > MaximumSeasonYear)
        {
            ErrorMessage = $"Enter a season year from 1 to {MaximumSeasonYear}.";
            return;
        }

        if (!ulong.TryParse(WorldSeed, NumberStyles.None, CultureInfo.InvariantCulture, out var seed))
        {
            ErrorMessage = "Enter a whole-number world seed from 0 to 18,446,744,073,709,551,615.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedTeamName))
        {
            ErrorMessage = "Choose the team you want to manage.";
            return;
        }

        try
        {
            var snapshot = _gameManager.StartNewGame(
                new NewGameCommand(seasonYear, new RandomState(seed), SelectedTeamName));
            var managedTeam = snapshot.League.Teams.Single(team => team.Id == snapshot.ManagedTeamId);

            CreatedTeamName = managedTeam.Name;
            CreatedSeason = $"{snapshot.League.SeasonYear} season";
            CreatedTeamCount = snapshot.League.Teams.Count;
            CreatedPlayerCount = snapshot.League.Teams.Sum(team => team.Roster.Count);
            IsGameCreated = true;
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = exception.Message;
        }
    }
}