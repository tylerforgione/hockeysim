using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Confirmation;
using HockeySim.Management.Saves;

namespace HockeySim.Desktop.Saves;

/// <summary>
/// Lists the saved games, loads the chosen one in place of any game in progress, and deletes saves.
/// </summary>
public sealed partial class LoadGameViewModel : ObservableObject
{
    private readonly ISavedGameLibrary _library;
    private readonly SavedGameLoader _loader;
    private readonly ConfirmationViewModel _confirmation;
    private readonly Action _back;
    private readonly Action<SaveName> _saveDeleted;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private SavedGameRowViewModel? _selectedSave;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSaves))]
    private IReadOnlyList<SavedGameRowViewModel> _saves;

    /// <param name="saveDeleted">Told of each save deleted, so the game in progress can note it.</param>
    public LoadGameViewModel(
        ISavedGameLibrary saves,
        SavedGameLoader loader,
        ConfirmationViewModel confirmation,
        Action back,
        Action<SaveName> saveDeleted)
    {
        ArgumentNullException.ThrowIfNull(saves);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentNullException.ThrowIfNull(back);
        ArgumentNullException.ThrowIfNull(saveDeleted);

        _library = saves;
        _loader = loader;
        _confirmation = confirmation;
        _back = back;
        _saveDeleted = saveDeleted;
        _saves = SavedGameRowViewModel.List(saves, out _errorMessage);
    }

    public bool HasSaves => Saves.Count > 0;

    public bool HasError => ErrorMessage is not null;

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private void Load()
    {
        if (SelectedSave is not { } save)
        {
            return;
        }

        ErrorMessage = null;
        _loader.Load(save.Name, message => ErrorMessage = message);
    }

    private bool CanLoad() => SelectedSave is not null;

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private void Delete()
    {
        if (SelectedSave is not { } save)
        {
            return;
        }

        _confirmation.Request(
            "Delete saved game?",
            $"'{save.Name}' will be deleted. This can't be undone.",
            "Delete",
            () => DeleteConfirmed(save.Name));
    }

    private void DeleteConfirmed(SaveName name)
    {
        ErrorMessage = null;
        try
        {
            _library.Delete(name);
        }
        catch (GameSaveStorageException exception)
        {
            ErrorMessage = $"'{name}' could not be deleted. {exception.Message}";
            return;
        }

        _saveDeleted(name);
        SelectedSave = null;
        Saves = SavedGameRowViewModel.List(_library, out var error);
        ErrorMessage = error;
    }

    [RelayCommand]
    private void Back()
    {
        _back();
    }
}