using System.Text.Json.Serialization;

using HockeySim.Management.Saves;

namespace HockeySim.Infrastructure.Saves;

/// <summary>
/// The body of a save file, after the format header has been checked.
/// </summary>
/// <remarks>
/// Nullable annotations and constructor parameters are enforced, so a missing or null value the
/// save model requires is rejected while reading instead of producing a partial game. Enums are
/// written by name, so reordering an enum cannot silently change a saved value.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UseStringEnumConverter = true,
    Converters =
    [
        typeof(TeamIdJsonConverter),
        typeof(PlayerIdJsonConverter),
        typeof(InboxMessageIdJsonConverter),
        typeof(RandomStateJsonConverter),
    ])]
[JsonSerializable(typeof(GameSave))]
internal sealed partial class GameSaveJsonContext : JsonSerializerContext;