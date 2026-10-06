using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Management.NewGame;

/// <summary>
/// Generates a new player's ratings with believable differences between positions.
/// </summary>
/// <remarks>
/// Each player draws one talent level. Skill ratings the position relies on follow that talent,
/// shifted by a position profile (centres pass and take faceoffs, defence block shots and defend)
/// with a small per-rating variation, so a strong player is strong across the board without every
/// rating being equal. Traits (discipline, stamina, durability, toughness) vary independently of
/// talent. Ratings outside the position's game (a goalie's skating, a skater's reflexes) are drawn
/// low around a fixed value. The values are provisional until the event engine is calibrated.
/// </remarks>
internal static class PlayerRatingGenerator
{
    // Three draws of 0-10 make a talent of 50-80 that clusters around 65, the mean of the
    // earlier uniform 40-90 ratings, so stars and fringe players are both uncommon.
    private const int TalentBase = 50;
    private const int TalentDrawCount = 3;
    private const int TalentDrawMaximum = 10;

    private const int SkillSpread = 8;
    private const int TraitSpread = 15;
    private const int OffPositionSpread = 10;

    private static readonly Rating[] SkaterSkills =
    [
        Rating.Skating,
        Rating.ShotPower,
        Rating.ShotAccuracy,
        Rating.PuckControl,
        Rating.Passing,
        Rating.OffensiveAwareness,
        Rating.DefensiveAwareness,
        Rating.Checking,
        Rating.ShotBlocking,
        Rating.StickChecking,
        Rating.Faceoffs,
    ];

    private static readonly Rating[] GoalieSkills =
    [
        Rating.GoalieReflex,
        Rating.GoaliePositioning,
        Rating.GoalieReboundControl,
    ];

    private static readonly IReadOnlyDictionary<Position, IReadOnlyDictionary<Rating, RatingProfile>> Profiles =
        new Dictionary<Position, IReadOnlyDictionary<Rating, RatingProfile>>
        {
            [Position.Centre] = CreateSkaterProfile(
                skillOffsets: new()
                {
                    [Rating.Skating] = 0,
                    [Rating.ShotPower] = -3,
                    [Rating.ShotAccuracy] = 0,
                    [Rating.PuckControl] = 2,
                    [Rating.Passing] = 4,
                    [Rating.OffensiveAwareness] = 3,
                    [Rating.DefensiveAwareness] = 0,
                    [Rating.Checking] = -5,
                    [Rating.ShotBlocking] = -6,
                    [Rating.StickChecking] = 0,
                    [Rating.Faceoffs] = 5,
                },
                toughness: 55),
            [Position.Wing] = CreateSkaterProfile(
                skillOffsets: new()
                {
                    [Rating.Skating] = 2,
                    [Rating.ShotPower] = 2,
                    [Rating.ShotAccuracy] = 3,
                    [Rating.PuckControl] = 1,
                    [Rating.Passing] = 0,
                    [Rating.OffensiveAwareness] = 3,
                    [Rating.DefensiveAwareness] = -4,
                    [Rating.Checking] = -2,
                    [Rating.ShotBlocking] = -6,
                    [Rating.StickChecking] = -2,
                    [Rating.Faceoffs] = -15,
                },
                toughness: 60),
            [Position.Defence] = CreateSkaterProfile(
                skillOffsets: new()
                {
                    [Rating.Skating] = -2,
                    [Rating.ShotPower] = 1,
                    [Rating.ShotAccuracy] = -6,
                    [Rating.PuckControl] = -4,
                    [Rating.Passing] = 0,
                    [Rating.OffensiveAwareness] = -6,
                    [Rating.DefensiveAwareness] = 5,
                    [Rating.Checking] = 3,
                    [Rating.ShotBlocking] = 6,
                    [Rating.StickChecking] = 3,
                    [Rating.Faceoffs] = -25,
                },
                toughness: 62),
            [Position.Goalie] = CreateGoalieProfile(),
        };

    public static Dictionary<Rating, RatingScore> Create(Position position, ControlledRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var talent = TalentBase + Enumerable.Range(0, TalentDrawCount)
            .Sum(_ => random.NextInt(0, TalentDrawMaximum + 1));

        // Enum order keeps the number of random draws, and so every later generated value,
        // reproducible for a given random state.
        return Enum.GetValues<Rating>().ToDictionary(
            rating => rating,
            rating => Profiles[position][rating].Generate(talent, random));
    }

    private static IReadOnlyDictionary<Rating, RatingProfile> CreateSkaterProfile(
        Dictionary<Rating, int> skillOffsets,
        int toughness)
    {
        var profile = SkaterSkills.ToDictionary(
            rating => rating,
            rating => RatingProfile.Skill(skillOffsets[rating]));

        foreach (var rating in GoalieSkills)
        {
            profile[rating] = RatingProfile.OffPosition(20);
        }

        AddTraits(profile, stamina: 65, toughness);
        return profile;
    }

    private static IReadOnlyDictionary<Rating, RatingProfile> CreateGoalieProfile()
    {
        var profile = new Dictionary<Rating, RatingProfile>
        {
            [Rating.GoalieReflex] = RatingProfile.Skill(2),
            [Rating.GoaliePositioning] = RatingProfile.Skill(1),
            [Rating.GoalieReboundControl] = RatingProfile.Skill(0),
        };

        foreach (var rating in SkaterSkills)
        {
            profile[rating] = RatingProfile.OffPosition(rating == Rating.Faceoffs ? 15 : 30);
        }

        AddTraits(profile, stamina: 65, toughness: 45);
        return profile;
    }

    private static void AddTraits(Dictionary<Rating, RatingProfile> profile, int stamina, int toughness)
    {
        profile[Rating.Discipline] = RatingProfile.Trait(65);
        profile[Rating.Stamina] = RatingProfile.Trait(stamina);
        profile[Rating.Durability] = RatingProfile.Trait(65);
        profile[Rating.Toughness] = RatingProfile.Trait(toughness);
    }

    /// <summary>
    /// How one rating is drawn for a position: <see cref="Centre"/> plus the player's talent when
    /// the rating follows it, varied by up to <see cref="Spread"/> either way.
    /// </summary>
    private sealed record RatingProfile(bool FollowsTalent, int Centre, int Spread)
    {
        public static RatingProfile Skill(int talentOffset) => new(true, talentOffset, SkillSpread);

        public static RatingProfile Trait(int centre) => new(false, centre, TraitSpread);

        public static RatingProfile OffPosition(int centre) => new(false, centre, OffPositionSpread);

        public RatingScore Generate(int talent, ControlledRandom random)
        {
            var value = (FollowsTalent ? talent : 0) + Centre + random.NextInt(-Spread, Spread + 1);
            return new RatingScore(Math.Clamp(value, 0, 100));
        }
    }
}