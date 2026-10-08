using HockeySim.Domain;

namespace HockeySim.Simulation.Tests;

internal static class TestBiography
{
    /// <summary>
    /// A valid biography. The match engine reads height and weight, which affect physical play,
    /// and handedness, which matters on the wings and defence; the default size is close to the
    /// engine's reference size.
    /// </summary>
    public static PlayerBiography Create(
        int heightInches = 73,
        int weightPounds = 200,
        Handedness handedness = Handedness.Left) =>
        new(
            new DateOnly(2001, 3, 15),
            new Birthplace("Toronto", "Ontario", Country.Canada),
            Country.Canada,
            handedness,
            new Height(heightInches),
            new Weight(weightPounds));
}