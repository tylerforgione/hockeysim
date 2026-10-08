using HockeySim.Domain;

namespace HockeySim.Simulation.Play;

/// <summary>
/// Combines individual ratings into the composite strengths the match engine compares.
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

    private static readonly Rating[] SavingRatings = [Rating.GoalieReflex, Rating.GoaliePositioning];

    private static readonly Rating[] HittingRatings = [Rating.Checking, Rating.Toughness];

    private static readonly Rating[] PuckProtectionRatings = [Rating.PuckControl, Rating.Skating];

    /// <summary>Ability to create shots while on the ice.</summary>
    public static double Offence(Player player) => Average(player, OffenceRatings);

    /// <summary>Ability to suppress the opponent's shots while on the ice.</summary>
    public static double Defence(Player player) => Average(player, DefenceRatings);

    /// <summary>Ability to beat a goalie with a shot that reaches the net.</summary>
    public static double Finishing(Player player) => Average(player, FinishingRatings);

    /// <summary>Ability to set up a teammate's goal.</summary>
    public static double Playmaking(Player player) => Average(player, PlaymakingRatings);

    /// <summary>Ability to beat a goalie one-on-one in a shootout.</summary>
    public static double Shootout(Player player) => Average(player, ShootoutRatings);

    /// <summary>A goalie's ability to stop a shot on goal.</summary>
    public static double Saving(Player goalie) => Average(goalie, SavingRatings);

    /// <summary>
    /// A goalie's overall strength in net, including rebound control; used in the shootout.
    /// </summary>
    public static double Goaltending(Player goalie) =>
        (Saving(goalie) * SavingRatings.Length + goalie.GetRating(Rating.GoalieReboundControl).Value)
        / (SavingRatings.Length + 1);

    /// <summary>How hard and how often a skater hits, including the effect of their size.</summary>
    public static double Physicality(Player player) => Average(player, HittingRatings) + SizeEffect(player);

    /// <summary>How well a puck carrier keeps the puck through a hit, including the effect of their size.</summary>
    public static double PuckProtection(Player player) => Average(player, PuckProtectionRatings) + SizeEffect(player);

    /// <summary>
    /// The effect of size on physical play in rating points: positive for players taller and
    /// heavier than the reference, negative for smaller ones, and bounded either way.
    /// </summary>
    public static double SizeEffect(Player player)
    {
        var biography = player.Biography;
        var effect = ((biography.Height.Inches - MatchTuning.ReferenceHeightInches) * MatchTuning.SizePerInch)
            + ((biography.Weight.Pounds - MatchTuning.ReferenceWeightPounds) * MatchTuning.SizePerPound);
        return Math.Clamp(effect, -MatchTuning.MaximumSizeEffect, MatchTuning.MaximumSizeEffect);
    }

    private static double Average(Player player, IEnumerable<Rating> ratings) =>
        ratings.Average(rating => player.GetRating(rating).Value);
}