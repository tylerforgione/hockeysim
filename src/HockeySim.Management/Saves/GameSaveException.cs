namespace HockeySim.Management.Saves;

/// <summary>
/// A save could not be loaded. The active game, if any, is unchanged.
/// </summary>
public abstract class GameSaveException : Exception
{
    protected GameSaveException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The save is damaged, is not a save, or describes a game that breaks the game's rules.
/// </summary>
public sealed class InvalidGameSaveException(string message, Exception? innerException = null)
    : GameSaveException(message, innerException);

/// <summary>
/// The save was written in a format version this build does not read. Pre-release builds do not
/// read saves from other format versions.
/// </summary>
public sealed class UnsupportedGameSaveVersionException(int version, int supportedVersion)
    : GameSaveException(
        $"Save format version {version} is not supported; this version of HockeySim reads version {supportedVersion}.")
{
    public int Version { get; } = version;

    public int SupportedVersion { get; } = supportedVersion;
}