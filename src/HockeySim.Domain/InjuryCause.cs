namespace HockeySim.Domain;

/// <summary>What in a match injured a player.</summary>
public enum InjuryCause
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