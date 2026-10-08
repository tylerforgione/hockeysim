namespace HockeySim.Simulation.Play;

/// <summary>What in the play can injure a player.</summary>
internal enum InjuryCause
{
    /// <summary>Being hit while carrying the puck.</summary>
    Hit,

    /// <summary>Throwing a hit: the hitter's side of the collision.</summary>
    Collision,

    /// <summary>Blocking a shot.</summary>
    BlockedShot,

    Fight,

    /// <summary>A non-contact strain from skating or making a save, likelier when tired.</summary>
    Strain,
}