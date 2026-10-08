namespace HockeySim.Domain;

/// <summary>
/// Injuries are capped so every team can still dress a lineup: an injury that would leave a team
/// with fewer able skaters or goalies than a lineup needs does not happen. With a 23-man roster of
/// two goalies, goalies therefore suffer only injuries they can play through.
/// </summary>
public static class InjuryCap
{
    /// <summary>The fewest rostered skaters able to play that a team keeps.</summary>
    public const int MinimumAbleSkaters = 18;

    /// <summary>The fewest rostered goalies able to play that a team keeps.</summary>
    public const int MinimumAbleGoalies = 2;

    /// <summary>
    /// Whether a team with this many able players at the position can lose one more to an injury
    /// that cannot be played through.
    /// </summary>
    public static bool AllowsLosing(Position position, int ablePlayersAtPosition) =>
        ablePlayersAtPosition > (position == Position.Goalie ? MinimumAbleGoalies : MinimumAbleSkaters);
}