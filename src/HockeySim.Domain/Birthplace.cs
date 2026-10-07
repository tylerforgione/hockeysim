namespace HockeySim.Domain;

/// <summary>
/// Where a player was born. Countries that identify places by state or province (Canada and the
/// United States) require a region; other countries have none.
/// </summary>
public sealed record Birthplace
{
    public Birthplace(string city, string? region, Country country)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(city);

        if (!Enum.IsDefined(country))
        {
            throw new ArgumentOutOfRangeException(nameof(country), "The birth country must be defined.");
        }

        if (UsesRegions(country))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(region);
        }
        else if (region is not null)
        {
            throw new ArgumentException("Only countries with states or provinces have a birth region.", nameof(region));
        }

        City = city;
        Region = region;
        Country = country;
    }

    public string City { get; }

    /// <summary>The state or province, present only where the country uses them.</summary>
    public string? Region { get; }

    public Country Country { get; }

    /// <summary>Whether places in the country are identified by state or province.</summary>
    public static bool UsesRegions(Country country) => country is Country.Canada or Country.UnitedStates;
}