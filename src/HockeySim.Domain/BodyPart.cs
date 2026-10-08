namespace HockeySim.Domain;

/// <summary>
/// A part of the body that can be injured. Each player has hidden wear on every part; see
/// <see cref="PlayerHealth"/>.
/// </summary>
public enum BodyPart
{
    Head,
    Face,
    Shoulder,
    Hand,
    Ribs,
    Back,
    Groin,
    Knee,
    Ankle,
    Foot,
}