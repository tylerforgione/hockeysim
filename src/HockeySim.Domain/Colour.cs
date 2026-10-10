using System.Globalization;

namespace HockeySim.Domain;

/// <summary>
/// An opaque sRGB colour, written as <c>#RRGGBB</c>.
/// </summary>
public readonly record struct Colour(byte Red, byte Green, byte Blue)
{
    /// <summary>Reads a colour written as <c>#RRGGBB</c>.</summary>
    /// <exception cref="ArgumentException">The text is not a <c>#RRGGBB</c> colour.</exception>
    public static Colour FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);

        if (hex.Length != 7
            || hex[0] != '#'
            || !int.TryParse(hex.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            throw new ArgumentException($"'{hex}' is not a colour written as #RRGGBB.", nameof(hex));
        }

        return new Colour((byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    public override string ToString() => $"#{Red:X2}{Green:X2}{Blue:X2}";
}