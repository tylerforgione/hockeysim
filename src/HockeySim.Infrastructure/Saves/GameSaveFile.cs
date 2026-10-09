using System.IO.Compression;
using System.Text.Json;

using HockeySim.Management.Saves;

namespace HockeySim.Infrastructure.Saves;

/// <summary>
/// Stores one game in a local file: a Brotli-compressed JSON document whose header names the
/// format and its version ahead of the game itself. See
/// docs/adr/0004-local-save-format.md for why.
/// </summary>
/// <remarks>
/// A save is written to a temporary file beside the target and moved over it only once complete,
/// so an interrupted or failed save never leaves a damaged or partly written game behind.
/// </remarks>
public sealed class GameSaveFile : IGameSaveStore
{
    /// <summary>
    /// The only format version this build reads or writes. Increase it whenever
    /// <see cref="GameSave"/> or its serialization changes shape; pre-release builds reject saves
    /// from any other version rather than migrating them.
    /// </summary>
    public const int FormatVersion = 9;

    private const string FormatName = "HockeySim save";
    private const string FormatProperty = "format";
    private const string FormatVersionProperty = "formatVersion";
    private const string GameProperty = "game";

    public GameSaveFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FilePath = Path.GetFullPath(path);
    }

    public string FilePath { get; }

    public void Save(GameSave save)
    {
        ArgumentNullException.ThrowIfNull(save);

        try
        {
            WriteAtomically(save);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new GameSaveStorageException($"The game could not be saved to '{FilePath}'. {exception.Message}", exception);
        }
    }

    public GameSave Load()
    {
        if (!File.Exists(FilePath))
        {
            throw new GameSaveStorageException($"No save exists at '{FilePath}'.", new FileNotFoundException(null, FilePath));
        }

        try
        {
            using var file = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var compressed = new BrotliStream(file, CompressionMode.Decompress);
            using var document = JsonDocument.Parse(compressed);
            return ReadDocument(document.RootElement);
        }
        // The Brotli decoder reports data it cannot decode as an invalid operation.
        catch (Exception exception) when (exception is JsonException or InvalidDataException or InvalidOperationException)
        {
            throw new InvalidGameSaveException(
                $"'{FilePath}' is not a readable HockeySim save. It may be damaged or incomplete.",
                exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new GameSaveStorageException($"'{FilePath}' could not be read. {exception.Message}", exception);
        }
    }

    private void WriteAtomically(GameSave save)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(FilePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var compressed = new BrotliStream(file, CompressionLevel.Optimal, leaveOpen: true))
                using (var writer = new Utf8JsonWriter(compressed))
                {
                    WriteDocument(writer, save);
                }

                // Reach the disk before the rename, so a crash cannot leave the rename without the data.
                file.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }
    }

    private static void WriteDocument(Utf8JsonWriter writer, GameSave save)
    {
        writer.WriteStartObject();
        writer.WriteString(FormatProperty, FormatName);
        writer.WriteNumber(FormatVersionProperty, FormatVersion);
        writer.WritePropertyName(GameProperty);
        JsonSerializer.Serialize(writer, save, GameSaveJsonContext.Default.GameSave);
        writer.WriteEndObject();
    }

    /// <summary>
    /// Checks the header before reading the game, so a save from another format version is
    /// reported as unsupported rather than as damaged.
    /// </summary>
    private GameSave ReadDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(FormatProperty, out var format)
            || format.ValueKind != JsonValueKind.String
            || format.GetString() != FormatName)
        {
            throw new InvalidGameSaveException($"'{FilePath}' is not a HockeySim save.");
        }

        if (!root.TryGetProperty(FormatVersionProperty, out var versionElement)
            || versionElement.ValueKind != JsonValueKind.Number
            || !versionElement.TryGetInt32(out var version))
        {
            throw new InvalidGameSaveException($"'{FilePath}' does not state a valid save format version.");
        }

        if (version != FormatVersion)
        {
            throw new UnsupportedGameSaveVersionException(version, FormatVersion);
        }

        if (!root.TryGetProperty(GameProperty, out var game))
        {
            throw new InvalidGameSaveException($"'{FilePath}' does not contain a game.");
        }

        return game.Deserialize(GameSaveJsonContext.Default.GameSave)
            ?? throw new InvalidGameSaveException($"'{FilePath}' does not contain a game.");
    }
}