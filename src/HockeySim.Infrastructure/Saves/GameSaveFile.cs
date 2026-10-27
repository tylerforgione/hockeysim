using System.IO.Compression;
using System.Text.Json;

using HockeySim.Management.Saves;

namespace HockeySim.Infrastructure.Saves;

/// <summary>
/// Stores one game in a local file: a Brotli-compressed JSON document whose header names the
/// format, its version, and a short description of the game ahead of the game itself. See
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
    public const int FormatVersion = 16;

    private const string FormatName = "HockeySim save";
    private const string FormatProperty = "format";
    private const string FormatVersionProperty = "formatVersion";
    private const string DetailsProperty = "details";
    private const string GameProperty = "game";

    // The details follow the version at the start of the document and are a few hundred bytes, so
    // this leaves ample room while keeping a listing from decompressing any of the game.
    private const int HeaderBytes = 16 * 1024;

    public GameSaveFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FilePath = Path.GetFullPath(path);
    }

    public string FilePath { get; }

    public void Save(GameSave save, SavedGameDetails details)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(details);

        try
        {
            WriteAtomically(save, details);
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

    private void WriteAtomically(GameSave save, SavedGameDetails details)
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
                    WriteDocument(writer, save, details);
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

    /// <summary>
    /// Reads what the save says about the managed team without reading the game: only the start
    /// of the document is decompressed. Unlike <see cref="Load"/> this never throws for a file
    /// that cannot be used, so one bad save cannot stop others being listed.
    /// </summary>
    /// <returns>
    /// The details, or null when the file is missing or unreadable, is not a HockeySim save, uses
    /// another format version, or has damaged or misplaced details. A file cut off after its
    /// header still has details; only <see cref="Load"/> finds the game missing.
    /// </returns>
    public SavedGameDetails? ReadDetails()
    {
        try
        {
            using var file = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var compressed = new BrotliStream(file, CompressionMode.Decompress);
            var buffer = new byte[HeaderBytes];
            var length = compressed.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            return ParseDetails(buffer.AsSpan(0, length), isFinalBlock: length < buffer.Length);
        }
        // The Brotli decoder reports data it cannot decode as an invalid operation.
        catch (Exception exception) when (exception is JsonException or InvalidDataException or InvalidOperationException
            or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static SavedGameDetails? ParseDetails(ReadOnlySpan<byte> header, bool isFinalBlock)
    {
        var reader = new Utf8JsonReader(header, isFinalBlock, default);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return null;
        }

        // The format name and version must come first, in that order, as Load checks them.
        if (!NextProperty(ref reader, FormatProperty)
            || !reader.Read() || reader.TokenType != JsonTokenType.String || !reader.ValueTextEquals(FormatName)
            || !NextProperty(ref reader, FormatVersionProperty)
            || !reader.Read() || reader.TokenType != JsonTokenType.Number
            || !reader.TryGetInt32(out var version) || version != FormatVersion
            || !NextProperty(ref reader, DetailsProperty)
            || !reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return null;
        }

        // Deserializing needs the whole object, so check it fits in what was read.
        var probe = reader;
        if (!probe.TrySkip())
        {
            return null;
        }

        return JsonSerializer.Deserialize(ref reader, GameSaveJsonContext.Default.SavedGameDetails);
    }

    private static bool NextProperty(ref Utf8JsonReader reader, string name) =>
        reader.Read() && reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals(name);

    private static void WriteDocument(Utf8JsonWriter writer, GameSave save, SavedGameDetails details)
    {
        writer.WriteStartObject();
        writer.WriteString(FormatProperty, FormatName);
        writer.WriteNumber(FormatVersionProperty, FormatVersion);
        writer.WritePropertyName(DetailsProperty);
        JsonSerializer.Serialize(writer, details, GameSaveJsonContext.Default.SavedGameDetails);
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