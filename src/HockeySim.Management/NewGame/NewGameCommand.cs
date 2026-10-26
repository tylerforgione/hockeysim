using System.Buffers.Binary;
using System.Security.Cryptography;

using HockeySim.Simulation.Randomness;

namespace HockeySim.Management.NewGame;

/// <summary>
/// Supplies every external choice needed to create a reproducible fictional league.
/// </summary>
public sealed record NewGameCommand(
    int SeasonYear,
    RandomState RandomState,
    string ManagedTeamName)
{
    /// <summary>
    /// Creates a command whose random state is drawn from an unpredictable seed.
    /// </summary>
    public static NewGameCommand WithFreshSeed(int seasonYear, string managedTeamName)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(bytes);
        return new NewGameCommand(
            seasonYear,
            new RandomState(BinaryPrimitives.ReadUInt64LittleEndian(bytes)),
            managedTeamName);
    }
}