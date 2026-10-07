using HockeySim.Domain;

namespace HockeySim.Simulation.Play;

/// <summary>A skater on the ice and the role they play there.</summary>
internal readonly record struct OnIceSkater(SkaterState Skater, SkaterRole Role)
{
    public bool IsDefence => Role == SkaterRole.Defence;
}