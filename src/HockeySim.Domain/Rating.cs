namespace HockeySim.Domain;

/// <summary>
/// A player ability on the 0-100 scale. Every player has a value for every rating, including
/// ratings their position rarely uses: a goalie's skater ratings are generated low and are not
/// shown to the user.
/// </summary>
public enum Rating
{
    Skating,
    ShotPower,
    ShotAccuracy,
    PuckControl,
    Passing,
    OffensiveAwareness,
    DefensiveAwareness,
    Checking,
    ShotBlocking,
    StickChecking,
    GoalieReflex,
    GoaliePositioning,
    GoalieReboundControl,

    /// <summary>Winning faceoffs; centres take most of them.</summary>
    Faceoffs,

    /// <summary>How rarely the player takes penalties; higher is more disciplined.</summary>
    Discipline,

    /// <summary>How slowly the player tires during a match; higher tires more slowly.</summary>
    Stamina,

    /// <summary>
    /// Resistance to injury. Hidden from the user: Management snapshots omit it and it does not
    /// contribute to the <see cref="OverallRating"/>, so it cannot be inferred from what is shown.
    /// </summary>
    Durability,

    /// <summary>Physical play and willingness and ability to fight.</summary>
    Toughness,
}