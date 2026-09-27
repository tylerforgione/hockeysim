using System.Buffers.Binary;

namespace HockeySim.Management.NewGame;

internal sealed class ControlledRandom
{
    private const ulong Increment = 0x9E3779B97F4A7C15;

    private ulong _state;

    public ControlledRandom(RandomState state)
    {
        _state = state.Value;
    }

    public RandomState State => new(_state);

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

    private ulong NextUInt64()
    {
        _state += Increment;
        var value = _state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EB;
        return value ^ (value >> 31);
    }
}