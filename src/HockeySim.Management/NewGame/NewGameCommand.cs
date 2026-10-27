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
    string ManagedTeamName,
    string ManagerName = NewGameCommand.DefaultManagerName)
{
    /// <summary>The manager name used when the caller does not choose one.</summary>
    public const string DefaultManagerName = "General Manager";

    /// <summary>The longest manager name, after trimming, that a new game accepts.</summary>
    public const int MaximumManagerNameLength = 40;

    /// <summary>
    /// Trims a manager name and checks it is neither blank nor longer than the maximum. Shared by
    /// starting a game and restoring a save so both hold the same rule.
    /// </summary>
    /// <exception cref="ArgumentException">The name is missing, blank, or too long.</exception>
    internal static string NormalizeManagerName(string? name, string parameterName)
    {
        var managerName = name?.Trim();
        if (string.IsNullOrEmpty(managerName))
        {
            throw new ArgumentException("The manager name cannot be blank.", parameterName);
        }

        if (managerName.Length > MaximumManagerNameLength)
        {
            throw new ArgumentException(
                $"The manager name '{managerName}' is longer than {MaximumManagerNameLength} characters.",
                parameterName);
        }

        return managerName;
    }

    /// <summary>
    /// Draws an unpredictable random state, for a league the user did not choose a seed for.
    /// </summary>
    public static RandomState FreshRandomState()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(bytes);
        return new RandomState(BinaryPrimitives.ReadUInt64LittleEndian(bytes));
    }

    /// <summary>
    /// Creates a command whose random state is drawn from an unpredictable seed.
    /// </summary>
    public static NewGameCommand WithFreshSeed(
        int seasonYear,
        string managedTeamName,
        string managerName = DefaultManagerName) =>
        new(seasonYear, FreshRandomState(), managedTeamName, managerName);
}