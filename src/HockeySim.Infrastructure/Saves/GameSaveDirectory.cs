using HockeySim.Management.Saves;

namespace HockeySim.Infrastructure.Saves;

/// <summary>
/// Keeps each saved game as a <see cref="GameSaveFile"/> in one folder, named after the save.
/// </summary>
/// <remarks>
/// Save names ignore letter case, so a save is found by comparing names rather than by building a
/// path. On a case-sensitive file system this finds "Dynasty.hockeysim" when the user saves as
/// "dynasty", so the user is asked before it is replaced, just as on Windows or macOS.
/// </remarks>
public sealed class GameSaveDirectory : ISavedGameLibrary
{
    public const string FileExtension = ".hockeysim";

    public GameSaveDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        DirectoryPath = Path.GetFullPath(path);
    }

    public string DirectoryPath { get; }

    /// <summary>
    /// The folder saves are kept in for the current user: HockeySim/Saves in the platform's
    /// application data folder.
    /// </summary>
    public static string DefaultPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HockeySim", "Saves");

    /// <remarks>
    /// Files whose names are not valid save names, such as ones copied in by hand, are not listed.
    /// Each save's header is read for its details, but never its game; a save whose header cannot
    /// be read is listed without details.
    /// </remarks>
    public IReadOnlyList<SavedGameSummary> List() =>
        ReadSaveFiles()
            .Select(file => new SavedGameSummary(
                file.Name,
                new DateTimeOffset(File.GetLastWriteTimeUtc(file.Path)),
                new GameSaveFile(file.Path).ReadDetails()))
            .OrderByDescending(summary => summary.SavedAt)
            .ThenBy(summary => summary.Name.Value, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public bool Contains(SaveName name) => FindFile(name) is not null;

    public IGameSaveStore Open(SaveName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new GameSaveFile(FindFile(name) ?? Path.Combine(DirectoryPath, name.Value + FileExtension));
    }

    public void Delete(SaveName name)
    {
        var path = FindFile(name);
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new GameSaveStorageException($"The save '{name.Value}' could not be deleted. {exception.Message}", exception);
        }
    }

    private string? FindFile(SaveName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return ReadSaveFiles().FirstOrDefault(file => file.Name.Equals(name)).Path;
    }

    private List<(SaveName Name, string Path)> ReadSaveFiles()
    {
        try
        {
            if (!Directory.Exists(DirectoryPath))
            {
                return [];
            }

            var files = new List<(SaveName Name, string Path)>();
            foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*" + FileExtension))
            {
                // The pattern also matches longer extensions on Windows, so check it exactly.
                if (Path.GetExtension(path) == FileExtension
                    && SaveName.TryParse(Path.GetFileNameWithoutExtension(path), out var name, out _))
                {
                    files.Add((name, path));
                }
            }

            return files;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new GameSaveStorageException($"The saves in '{DirectoryPath}' could not be read. {exception.Message}", exception);
        }
    }
}