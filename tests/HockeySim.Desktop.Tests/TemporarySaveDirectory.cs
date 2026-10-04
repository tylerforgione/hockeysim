using HockeySim.Infrastructure.Saves;

namespace HockeySim.Desktop.Tests;

/// <summary>
/// A real save folder in its own temporary directory, deleted when the test ends.
/// </summary>
internal sealed class TemporarySaveDirectory : IDisposable
{
    public TemporarySaveDirectory()
    {
        Library = new GameSaveDirectory(Path.Combine(Path.GetTempPath(), $"hockeysim-desktop-tests-{Guid.NewGuid():N}"));
    }

    public GameSaveDirectory Library { get; }

    public string PathFor(string saveName) => Path.Combine(Library.DirectoryPath, saveName + GameSaveDirectory.FileExtension);

    public void Dispose()
    {
        if (Directory.Exists(Library.DirectoryPath))
        {
            Directory.Delete(Library.DirectoryPath, recursive: true);
        }
    }
}