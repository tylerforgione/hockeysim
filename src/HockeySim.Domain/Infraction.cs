namespace HockeySim.Domain;

/// <summary>The rule a penalized skater broke.</summary>
public enum Infraction
{
    Hooking,
    Tripping,
    Holding,
    Interference,
    Slashing,
    HighSticking,
    CrossChecking,
    Roughing,
    Boarding,
    Charging,
    Elbowing,

    /// <summary>Shooting the puck over the glass from the defending zone.</summary>
    DelayOfGame,

    Fighting,
}