using System.Buffers.Binary;

namespace HockeySim.Simulation.Randomness;

/// <summary>
/// The single source of outcome-affecting randomness. A SplitMix64 stream whose whole state is
/// one value, so it can be saved and resumed exactly. Changing the algorithm changes every
/// seeded outcome, including generated worlds.
/// </summary>
public sealed class ControlledRandom
{
    private const ulong Increment = 0x9E3779B97F4A7C15;

    private ulong _state;

    public ControlledRandom(RandomState state)
    {
        _state = state.Value;
    }

    public RandomState State => new(_state);

    public bool Chance(double probability) => NextDouble() < probability;

    public int NextInt(int minimumInclusive, int maximumExclusive)
    {
        if (minimumInclusive >= maximumExclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumExclusive),
                "The maximum must be greater than the minimum.");
        }

        var range = (ulong)(maximumExclusive - minimumInclusive);
        return minimumInclusive + (int)(NextUInt64() % range);
    }

    /// <summary>
    /// Picks an index with probability proportional to its weight.
    /// </summary>
    public int NextWeightedIndex(IReadOnlyList<double> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        if (weights.Count == 0 || weights.Any(weight => weight < 0) || weights.Sum() <= 0)
        {
            throw new ArgumentException("Weights must be non-negative with a positive total.", nameof(weights));
        }

        var target = NextDouble() * weights.Sum();

        for (var index = 0; index < weights.Count; index++)
        {
            target -= weights[index];
            if (target < 0)
            {
                return index;
            }
        }

        // Floating-point rounding can leave a tiny remainder; it belongs to the last entry.
        return weights.Count - 1;
    }

    public Guid NextGuid()
    {
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, NextUInt64());
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[8..], NextUInt64());

        // Mark generated identities as RFC 4122 variant, version 4 UUIDs while
        // retaining deterministic bytes from the persisted random state.
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    private ulong NextUInt64()
    {
        _state += Increment;
        var value = _state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EB;
        return value ^ (value >> 31);
    }
}