using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Confirmation;
using HockeySim.Desktop.Game;
using HockeySim.Management.Saves;

namespace HockeySim.Desktop.Saves;

/// <summary>
/// Saves the running game under a name the user types or picks from the existing saves. Replacing
/// an existing save is confirmed first.
/// </summary>
public sealed partial class SaveGameViewModel : ObservableObject
{
    private readonly GameSession _session;
    private readonly ISavedGameLibrary _saves;
    private readonly ConfirmationViewModel _confirmation;
    private readonly Action _closed;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private SavedGameRowViewModel? _selectedSave;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public SaveGameViewModel(
        GameSession session,
        ISavedGameLibrary saves,
        ConfirmationViewModel confirmation,
        Action closed)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(saves);
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentNullException.ThrowIfNull(closed);

        _session = session;
        _saves = saves;
        _confirmation = confirmation;
        _closed = closed;
        _name = session.GameName;
        ExistingSaves = SavedGameRowViewModel.List(saves, out _errorMessage);
    }

    public IReadOnlyList<SavedGameRowViewModel> ExistingSaves { get; }

    public bool HasExistingSaves => ExistingSaves.Count > 0;

    public bool HasError => ErrorMessage is not null;

    public int MaximumNameLength => SaveName.MaximumLength;

    partial void OnSelectedSaveChanged(SavedGameRowViewModel? value)
    {
        if (value is not null)
        {
            Name = value.DisplayName;
        }
    }

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = null;
        if (!SaveName.TryParse(Name, out var name, out var error))
        {
            ErrorMessage = error;
            return;
        }

        bool exists;
        try
        {
            exists = _saves.Contains(name);
        }
        catch (GameSaveStorageException exception)
        {
            ErrorMessage = $"The game was not saved. {exception.Message}";
            return;
        }

        if (exists)
        {
            _confirmation.Request(
                "Overwrite saved game?",
                $"A game is already saved as '{name}'. Saving replaces it with the current game, and the game it holds will be lost.",
                "Overwrite",
                () => Write(name));
            return;
        }

        Write(name);
    }

    [RelayCommand]
    private void Cancel()
    {
        _closed();
    }

    private void Write(SaveName name)
    {
        try
        {
            _session.SaveGame(_saves.Open(name), name);
        }
        catch (Exception exception) when (exception is GameSaveException or InvalidOperationException)
        {
            ErrorMessage = $"The game was not saved. {exception.Message}";
            return;
        }

        _closed();
    }
}