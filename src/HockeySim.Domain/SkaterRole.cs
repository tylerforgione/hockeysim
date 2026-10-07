namespace HockeySim.Domain;

/// <summary>
/// Where a skater plays within a special-situation unit. The centre takes faceoffs and defence
/// players man the points. A role describes the slot, not the player: any skater can fill it.
/// </summary>
public enum SkaterRole
{
    Centre,
    Wing,
    Defence,
}