using System.Text.Json;
using System.Text.Json.Serialization;

using HockeySim.Domain;
using HockeySim.Management.Inbox;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Infrastructure.Saves;

// Identities and the random state are single-value wrappers. Writing them as their bare value keeps
// the save readable ("3f2c..." rather than { "value": "3f2c..." }) and is how they are keyed.

internal sealed class TeamIdJsonConverter : JsonConverter<TeamId>
{
    public override TeamId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, TeamId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}

internal sealed class PlayerIdJsonConverter : JsonConverter<PlayerId>
{
    public override PlayerId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, PlayerId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}

internal sealed class InboxMessageIdJsonConverter : JsonConverter<InboxMessageId>
{
    public override InboxMessageId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, InboxMessageId value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}

/// <summary>
/// Writes the random state as an exact unsigned 64-bit number; any rounding would change every
/// later outcome.
/// </summary>
internal sealed class RandomStateJsonConverter : JsonConverter<RandomState>
{
    public override RandomState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetUInt64());

    public override void Write(Utf8JsonWriter writer, RandomState value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}