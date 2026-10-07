using HockeySim.Domain;

namespace HockeySim.Simulation.Tests;

internal static class TestBiography
{
    /// <summary>
    /// A valid biography. The match engine reads only height and weight, which affect physical
    /// play; the defaults are close to the engine's reference size.
    /// </summary>
    public static PlayerBiography Create(int heightInches = 73, int weightPounds = 200) =>
        new(
            new DateOnly(2001, 3, 15),
            new Birthplace("Toronto", "Ontario", Country.Canada),
            Country.Canada,
            Handedness.Left,
            new Height(heightInches),
            new Weight(weightPounds));
}