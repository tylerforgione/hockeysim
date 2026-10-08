namespace HockeySim.Domain;

/// <summary>
/// The side of the ice a lineup slot plays on, such as left wing or right defence. A skater whose
/// <see cref="Handedness"/> differs from the side plays on their off-hand side.
/// </summary>
public enum SkaterSide
{
    Left,
    Right,
}