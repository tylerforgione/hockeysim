using System.Globalization;

using HockeySim.Management.Saves;

namespace HockeySim.Desktop.Saves;

/// <summary>
/// One saved game in a list of saves to choose from.
/// </summary>
public sealed class SavedGameRowViewModel
{
    public SavedGameRowViewModel(SavedGameSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        Name = summary.Name;
        SavedAtLabel = summary.SavedAt.ToLocalTime().ToString("MMM d, yyyy · h:mm tt", CultureInfo.CurrentCulture);
    }

    public SaveName Name { get; }

    public string DisplayName => Name.Value;

    public string SavedAtLabel { get; }

    /// <summary>
    /// Lists the library's saves, or reports why they cannot be listed.
    /// </summary>
    public static IReadOnlyList<SavedGameRowViewModel> List(ISavedGameLibrary saves, out string? error)
    {
        try
        {
            error = null;
            return saves.List().Select(summary => new SavedGameRowViewModel(summary)).ToList();
        }
        catch (GameSaveStorageException exception)
        {
            error = $"Your saved games could not be listed. {exception.Message}";
            return [];
        }
    }
}