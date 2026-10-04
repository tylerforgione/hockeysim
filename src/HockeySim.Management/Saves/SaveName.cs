using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace HockeySim.Management.Saves;

/// <summary>
/// The name the user gives a saved game. Two names that differ only in letter case are the same
/// save.
/// </summary>
/// <remarks>
/// Saves are kept as local files named after the save, so a name is limited to characters that
/// are safe in file names on Windows, macOS, and Linux. Ignoring case keeps the saves the user
/// sees the same on case-insensitive file systems (the Windows and macOS defaults) and
/// case-sensitive ones.
/// </remarks>
public sealed class SaveName : IEquatable<SaveName>
{
    public const int MaximumLength = 60;

    private const string AllowedPunctuation = " -_'(),.&!";

    // Device names Windows reserves regardless of extension.
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private SaveName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    /// <summary>
    /// Reads a save name typed by the user, ignoring surrounding spaces.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not allowed; the message says why, for the user.</exception>
    public static SaveName Parse(string text) =>
        TryParse(text, out var name, out var error) ? name : throw new ArgumentException(error, nameof(text));

    /// <summary>
    /// Reads a save name typed by the user, ignoring surrounding spaces.
    /// </summary>
    /// <param name="error">Why the name is not allowed, written for the user, when parsing fails.</param>
    public static bool TryParse(
        string? text,
        [NotNullWhen(true)] out SaveName? name,
        [NotNullWhen(false)] out string? error)
    {
        name = null;

        // macOS may report file names in decomposed form; composing them keeps "é" one character.
        var value = (text ?? string.Empty).Trim().Normalize(NormalizationForm.FormC);
        if (value.Length == 0)
        {
            error = "Enter a name for the save.";
            return false;
        }

        if (value.Length > MaximumLength)
        {
            error = $"Save names cannot be longer than {MaximumLength} characters.";
            return false;
        }

        if (!value.All(character => char.IsLetterOrDigit(character) || AllowedPunctuation.Contains(character)))
        {
            error = "Save names can use letters, numbers, spaces, and - _ ' ( ) , . & !";
            return false;
        }

        // Windows drops a trailing dot from file names, so "Dynasty." would become "Dynasty".
        if (value.EndsWith('.') || value.StartsWith('.'))
        {
            error = "Save names cannot start or end with a full stop.";
            return false;
        }

        if (ReservedNames.Contains(value))
        {
            error = $"'{value}' is reserved by the operating system. Choose another name.";
            return false;
        }

        name = new SaveName(value);
        error = null;
        return true;
    }

    public bool Equals(SaveName? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as SaveName);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;
}