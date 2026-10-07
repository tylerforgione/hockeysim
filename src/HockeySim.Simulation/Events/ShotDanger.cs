namespace HockeySim.Simulation.Events;

/// <summary>
/// Where a shot attempt was taken from, by how likely a shot from there is to score: low danger
/// from the point and the perimeter, medium from the faceoff circles, and high from the slot and
/// the crease.
/// </summary>
public enum ShotDanger
{
    Low,
    Medium,
    High,
}