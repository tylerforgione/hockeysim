using HockeySim.Domain;

namespace HockeySim.Domain.Tests;

internal static class TestBiography
{
    /// <summary>A valid biography for tests whose subject is not the player's details.</summary>
    public static PlayerBiography Create(DateOnly? birthDate = null) =>
        new(
            birthDate ?? new DateOnly(2001, 3, 15),
            new Birthplace("Toronto", "Ontario", Country.Canada),
            Country.Canada,
            Handedness.Left,
            new Height(73),
            new Weight(195));
}