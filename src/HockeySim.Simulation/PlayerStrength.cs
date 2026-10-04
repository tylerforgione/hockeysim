using HockeySim.Domain;

namespace HockeySim.Simulation;

/// <summary>
/// Combines individual ratings into the composite strengths the match model compares.
/// Each composite is an unweighted mean on the same 0-100 scale as a single rating.
/// </summary>
internal static class PlayerStrength
{
    private static readonly Rating[] OffenceRatings =
    [
        Rating.Skating,
        Rating.ShotPower,
        Rating.ShotAccuracy,
        Rating.PuckControl,
        Rating.Passing,
        Rating.OffensiveAwareness,
    ];

    private static readonly Rating[] DefenceRatings =
    [
        Rating.Skating,
        Rating.DefensiveAwareness,
        Rating.Checking,
        Rating.ShotBlocking,
        Rating.StickChecking,
    ];

    private static readonly Rating[] FinishingRatings = [Rating.ShotPower, Rating.ShotAccuracy];

    private static readonly Rating[] PlaymakingRatings = [Rating.Passing, Rating.PuckControl, Rating.OffensiveAwareness];

    private static readonly Rating[] ShootoutRatings = [Rating.ShotAccuracy, Rating.PuckControl];

    private static readonly Rating[] GoaltendingRatings =
    [
        Rating.GoalieReflex,
        Rating.GoaliePositioning,
        Rating.GoalieReboundControl,
    ];

    /// <summary>Ability to create shots while on the ice.</summary>
    public static double Offence(Player player) => Average(player, OffenceRatings);

    /// <summary>Ability to suppress the opponent's shots while on the ice.</summary>
    public static double Defence(Player player) => Average(player, DefenceRatings);

    /// <summary>Ability to beat a goalie with a shot.</summary>
    public static double Finishing(Player player) => Average(player, FinishingRatings);

    /// <summary>Ability to set up a teammate's goal.</summary>
    public static double Playmaking(Player player) => Average(player, PlaymakingRatings);

    /// <summary>Ability to beat a goalie one-on-one in a shootout.</summary>
    public static double Shootout(Player player) => Average(player, ShootoutRatings);

    public static double Goaltending(Player goalie) => Average(goalie, GoaltendingRatings);

    private static double Average(Player player, IEnumerable<Rating> ratings) =>
        ratings.Average(rating => player.GetRating(rating).Value);
}