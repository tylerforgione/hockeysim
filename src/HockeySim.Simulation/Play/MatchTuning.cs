using HockeySim.Domain;

namespace HockeySim.Simulation.Play;

/// <summary>
/// The event engine's tuning values, kept together so configurable league settings can later
/// adjust them in one place. They are calibrated so a generated league's season approximates
/// recent NHL league averages; the targets, their sources, and the measured values are in
/// docs/areas/match-engine.md, and Management's calibration tests check them.
/// </summary>
/// <remarks>
/// Rating effects are changes in log-odds per rating point, measured from
/// <see cref="ReferenceRating"/>, so a player at the reference level leaves a base chance unchanged.
/// </remarks>
internal static class MatchTuning
{
    /// <summary>A league-average rating: the centre of generated talent.</summary>
    public const double ReferenceRating = 65;

    public const int RegulationPeriodSeconds = 20 * 60;
    public const int RegularSeasonOvertimeSeconds = 5 * 60;
    public const int PlayoffOvertimePeriodSeconds = 20 * 60;
    public const int ShootoutRounds = 3;

    // Share of ice time each forward line, defence pair, and three-on-three unit is deployed for
    // when rested. Fatigue evens out what they actually play: the five-on-five targets give
    // forward lines about 32/28/22/19% and defence pairs about 37/34/29% of their ice time.
    public static readonly double[] ForwardLineUsage = [0.36, 0.24, 0.22, 0.18];
    public static readonly double[] DefencePairUsage = [0.48, 0.31, 0.21];
    public static readonly double[] ThreeOnThreeUnitUsage = [0.45, 0.35, 0.20];
    public static readonly double[] PenaltyKillUnitUsage = [0.40, 0.35, 0.25];
    public static readonly double[] TwoUnitUsage = [0.60, 0.40];

    // Fatigue. Energy runs from one (rested) to zero. A skater of average stamina tires to the
    // change threshold in under a minute and recovers on the bench in about two minutes.
    public const double EnergyDrainPerSecond = 0.0125;
    public const double EnergyRecoveryPerSecond = 0.0045;
    public const double IntermissionRecovery = 0.6;
    public const double DefenceDrainMultiplier = 0.8;

    /// <summary>Stamina 0 tires this much faster than average, and stamina 100 this much slower.</summary>
    public const double StaminaDrainSpread = 0.6;

    /// <summary>Stamina 0 recovers this much slower than average, and stamina 100 this much faster.</summary>
    public const double StaminaRecoverySpread = 0.4;

    /// <summary>The share of a skater's ratings still available when completely exhausted.</summary>
    public const double ExhaustedPerformance = 0.8;

    // Shifts. A group changes when its mean energy falls below the tired threshold or the shift
    // reaches its longest length; a group returns only once its mean energy is back above the
    // ready threshold, unless no group is ready. At a stoppage a group that has been out a while
    // changes too.
    public const double TiredEnergy = 0.55;
    public const double ReadyEnergy = 0.8;
    public const int LongestShiftSeconds = 90;
    public const int StoppageChangeSeconds = 30;

    // Seconds each step of play takes, by zone (inclusive bounds).
    public const int DefensiveZoneStepMinimum = 3;
    public const int DefensiveZoneStepMaximum = 8;
    public const int NeutralZoneStepMinimum = 2;
    public const int NeutralZoneStepMaximum = 5;
    public const int OffensiveZoneStepMinimum = 3;
    public const int OffensiveZoneStepMaximum = 9;
    public const int ReboundStepMaximum = 2;

    /// <summary>How strongly the gap between attacking and defending skaters shifts each outcome.</summary>
    public const double PlayEdgeSensitivity = 0.0085;

    // Relative weights of what happens in one step, by the possessing team's zone.
    public const double DefensiveZoneExitWeight = 0.55;
    public const double DefensiveZoneTurnoverWeight = 0.12;
    public const double DefensiveZoneHitWeight = 0.10;
    public const double IcingWeight = 0.04;
    public const double DefensiveZoneHoldWeight = 0.21;

    public const double CarryInWeight = 0.35;
    public const double DumpInWeight = 0.30;
    public const double NeutralZoneTurnoverWeight = 0.10;
    public const double NeutralZoneHitWeight = 0.06;
    public const double OffsideWeight = 0.04;
    public const double RegroupWeight = 0.16;

    public const double ShotAttemptWeight = 0.415;
    public const double OffensiveZoneTurnoverWeight = 0.10;
    public const double OffensiveZoneHitWeight = 0.072;
    public const double ClearedWeight = 0.12;
    public const double OffensiveZoneStoppageWeight = 0.03;
    public const double CycleWeight = 0.355;

    /// <summary>A rush straight after a carry-in is this many times as likely to produce a shot.</summary>
    public const double RushShotMultiplier = 2.0;

    /// <summary>The chance the attackers recover their own dump-in.</summary>
    public const double DumpInRecoveryChance = 0.35;

    // The share of turnovers recorded as the defender's takeaway or the carrier's giveaway. The
    // rest are loose pucks and battles credited to nobody, as in NHL scoring.
    public const double TakeawayShare = 0.13;
    public const double GiveawayShare = 0.40;

    // Three-on-three overtime is played with open ice: more rushes and better chances, fewer hits.
    public const double OpenIceShotMultiplier = 2.6;
    public const double OpenIceCarryInMultiplier = 1.5;
    public const double OpenIceHighDangerMultiplier = 2.6;
    public const double OpenIceHitMultiplier = 0.4;

    // Shot danger weights for a set-up attack, and for a rush.
    public const double LowDangerWeight = 0.52;
    public const double MediumDangerWeight = 0.29;
    public const double HighDangerWeight = 0.19;
    public const double RushLowDangerWeight = 0.25;
    public const double RushMediumDangerWeight = 0.45;
    public const double RushHighDangerWeight = 0.30;

    /// <summary>How strongly the attacking edge moves attempts from low danger toward high danger.</summary>
    public const double DangerEdgeSensitivity = 0.01;

    // Who shoots: forwards from the slot, defence from the point.
    public const double ForwardLowDangerShooterWeight = 1.0;
    public const double ForwardMediumDangerShooterWeight = 2.0;
    public const double ForwardHighDangerShooterWeight = 3.0;
    public const double DefenceLowDangerShooterWeight = 1.6;
    public const double DefenceMediumDangerShooterWeight = 0.8;
    public const double DefenceHighDangerShooterWeight = 0.4;

    // Blocked shots, by danger; rush and rebound attempts are blocked less often.
    public const double LowDangerBlockChance = 0.43;
    public const double MediumDangerBlockChance = 0.28;
    public const double HighDangerBlockChance = 0.14;
    public const double RushBlockMultiplier = 0.6;
    public const double ReboundBlockChance = 0.08;
    public const double BlockingSensitivity = 0.012;
    public const double DefenceBlockerWeight = 2.0;
    public const double ForwardBlockerWeight = 1.0;
    public const double BlockedShotRecoveryChance = 0.55;

    // The expected-goal model: the base chance a reference shooter scores on a reference goalie
    // from each danger level, and how a rebound and a rush multiply the odds of scoring.
    public const double LowDangerExpectedGoals = 0.016;
    public const double MediumDangerExpectedGoals = 0.045;
    public const double HighDangerExpectedGoals = 0.118;
    public const double ReboundOddsMultiplier = 2.0;
    public const double RushOddsMultiplier = 1.3;

    // The share of unblocked attempts by a reference-rated shooter that reach the net.
    public const double LowDangerOnNetChance = 0.62;
    public const double MediumDangerOnNetChance = 0.68;
    public const double HighDangerOnNetChance = 0.72;
    public const double ReboundOnNetChance = 0.78;

    /// <summary>A penalty shot is a one-on-one attempt, scored as often as a reference shootout attempt.</summary>
    public const double PenaltyShotExpectedGoals = BaseShootoutGoalChance;

    // Shooter and goalie effects on top of the expected-goal value.
    public const double AccuracySensitivity = 0.012;
    public const double FinishingSensitivity = 0.015;
    public const double GoaltendingSensitivity = 0.02;

    // After a missed shot or a save.
    public const double MissedShotStoppageChance = 0.20;
    public const double MissedShotRecoveryChance = 0.5;
    public const double BaseReboundChance = 0.10;
    public const double ReboundControlSensitivity = 0.025;
    public const double ReboundShotChance = 0.6;
    public const double ReboundScrambleRecoveryChance = 0.5;
    public const double FrozenPuckChance = 0.30;

    public const double FaceoffSensitivity = 0.045;

    // Physical play. Size moves a player's physicality by about a rating point per inch over
    // 6'1" and per four pounds over 200, within limits.
    public const double HitRateSensitivity = 0.03;
    public const double HitTurnoverChance = 0.35;
    public const double HitTurnoverSensitivity = 0.04;
    public const double DefenceHitterWeight = 1.2;
    public const double ForwardHitterWeight = 1.0;
    public const int ReferenceHeightInches = 73;
    public const int ReferenceWeightPounds = 200;
    public const double SizePerInch = 1.0;
    public const double SizePerPound = 0.25;
    public const double MaximumSizeEffect = 15;

    // Assists: most goals are assisted, and most assisted goals have two assists.
    public const double PrimaryAssistChance = 0.9;
    public const double SecondaryAssistChance = 0.75;

    /// <summary>Keeps every teammate eligible for an assist, even with a playmaking strength of zero.</summary>
    public const double MinimumAssistWeight = 1.0;

    // Shootout.
    public const double BaseShootoutGoalChance = 0.32;
    public const double MinimumShootoutChance = 0.05;
    public const double MaximumShootoutChance = 0.95;

    // Special teams. Each skater a team has over the opponent counts as this many rating points of
    // attacking edge, so a power play shoots more, from better ice, and a penalty kill clears.
    public const double ManpowerEdgePerSkater = 84;

    // Pulling the goalie: a team trailing by one goal pulls with two minutes of the third period
    // left, and by two goals with three and a half, close to recent NHL averages.
    public const int PullGoalieOneGoalDownSeconds = 2 * 60;
    public const int PullGoalieTwoGoalsDownSeconds = (3 * 60) + 30;

    // Facing an empty net, the chance per step that a team with the puck short of the attacking
    // zone shoots for it from distance, and the chance a reference shooter's attempt reaches it.
    public const double LongEmptyNetShotChance = 0.3;
    public const double LongEmptyNetShotOnNetChance = 0.55;

    // Penalties in the run of play: the chance per step that the defending or the attacking team
    // commits one, before discipline and the play adjust it. Defenders who are being beaten foul
    // more, and every skater fouls more when tired, since fatigue lowers their effective discipline.
    public const double DefendingPenaltyChance = 0.0045;
    public const double AttackingPenaltyChance = 0.0018;
    public const double PenaltyEdgeSensitivity = 0.02;
    public const double DisciplineSensitivity = 0.03;

    /// <summary>Keeps every skater on the ice a possible offender, however disciplined.</summary>
    public const double MinimumIndisciplineWeight = 10;

    /// <summary>A foul on a rush that is awarded a penalty shot rather than a minor.</summary>
    public const double PenaltyShotShare = 0.12;

    // Severity: some high-sticking draws blood (a double minor), and a dangerous foul can be a
    // major, which always carries a game misconduct when it is not for fighting.
    public const double DoubleMinorHighStickingChance = 0.2;
    public const double DangerousHitMajorChance = 0.06;
    public const double StickFoulMajorChance = 0.01;

    // Incidents after a hit: an illegal hit, a scrum where both players take roughing minors, or
    // a fight where both take fighting majors. Fights are likelier between tough players and in
    // lopsided matches, and do not happen in overtime.
    public const double IllegalHitChance = 0.022;
    public const double ScrumChance = 0.008;
    public const double FightChance = 0.0038;
    public const double FightToughnessSensitivity = 0.05;
    public const int BlowoutGoalDifference = 3;
    public const double BlowoutFightMultiplier = 2.5;
    public const double ScrumMisconductChance = 0.12;

    /// <summary>Penalties are not called on a team with fewer available skaters than this.</summary>
    public const int MinimumAvailableSkaters = 8;

    // The infractions behind each kind of foul, with their relative frequency.
    public static readonly (Infraction Infraction, double Weight)[] RushFouls =
    [
        (Infraction.Hooking, 0.30),
        (Infraction.Tripping, 0.30),
        (Infraction.Holding, 0.20),
        (Infraction.Interference, 0.20),
    ];

    public static readonly (Infraction Infraction, double Weight)[] DefendingZoneFouls =
    [
        (Infraction.Hooking, 0.18),
        (Infraction.Holding, 0.15),
        (Infraction.Tripping, 0.15),
        (Infraction.Interference, 0.10),
        (Infraction.CrossChecking, 0.12),
        (Infraction.Slashing, 0.12),
        (Infraction.HighSticking, 0.10),
        (Infraction.Roughing, 0.08),
    ];

    public static readonly (Infraction Infraction, double Weight)[] ForecheckingFouls =
    [
        (Infraction.Interference, 0.30),
        (Infraction.Holding, 0.20),
        (Infraction.Hooking, 0.20),
        (Infraction.Slashing, 0.15),
        (Infraction.HighSticking, 0.15),
    ];

    public static readonly (Infraction Infraction, double Weight)[] OwnZoneFoulsWithThePuck =
    [
        (Infraction.DelayOfGame, 0.35),
        (Infraction.Interference, 0.20),
        (Infraction.Holding, 0.15),
        (Infraction.Slashing, 0.15),
        (Infraction.HighSticking, 0.15),
    ];

    public static readonly (Infraction Infraction, double Weight)[] AttackingFouls =
    [
        (Infraction.Interference, 0.30),
        (Infraction.Slashing, 0.25),
        (Infraction.HighSticking, 0.20),
        (Infraction.Tripping, 0.15),
        (Infraction.Hooking, 0.10),
    ];

    public static readonly (Infraction Infraction, double Weight)[] IllegalHits =
    [
        (Infraction.Boarding, 0.30),
        (Infraction.Charging, 0.20),
        (Infraction.Elbowing, 0.15),
        (Infraction.Roughing, 0.20),
        (Infraction.Interference, 0.15),
    ];

    // Keep every event possible but never certain, however lopsided the ratings.
    public const double MinimumChance = 0.005;
    public const double MaximumChance = 0.995;

    /// <summary>Each unit's share of a special situation's ice time when rested.</summary>
    public static double[] UnitUsage(SpecialSituation situation) => situation switch
    {
        SpecialSituation.ThreeOnThree => ThreeOnThreeUnitUsage,
        SpecialSituation.PenaltyKill4On5 => PenaltyKillUnitUsage,
        _ => TwoUnitUsage,
    };
}