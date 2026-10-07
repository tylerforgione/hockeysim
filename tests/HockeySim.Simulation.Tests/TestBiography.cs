using HockeySim.Domain;

namespace HockeySim.Simulation.Tests;

internal static class TestBiography
{
    /// <summary>A valid biography; the match engine does not yet read any of it.</summary>
    public static PlayerBiography Create() =>
        new(
            new DateOnly(2001, 3, 15),
            new Birthplace("Toronto", "Ontario", Country.Canada),
            Country.Canada,
            Handedness.Left,
            new Height(73),
            new Weight(195));
}