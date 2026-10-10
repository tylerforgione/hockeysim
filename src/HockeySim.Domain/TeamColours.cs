namespace HockeySim.Domain;

/// <summary>
/// A team's identity colours. The primary colour leads (banners, headers); the secondary colour
/// accents it, so the two must differ.
/// </summary>
public sealed record TeamColours
{
    public TeamColours(Colour primary, Colour secondary)
    {
        if (primary == secondary)
        {
            throw new ArgumentException("A team's primary and secondary colours must differ.", nameof(secondary));
        }

        Primary = primary;
        Secondary = secondary;
    }

    public Colour Primary { get; }

    public Colour Secondary { get; }
}