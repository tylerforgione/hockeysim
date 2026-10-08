using Xunit;

using static HockeySim.Domain.Tests.TestResults;

namespace HockeySim.Domain.Tests;

public sealed class MatchSummaryTests
{
    private static readonly League League = TestLeague.Create();
    private static readonly Team HomeTeam = League.Teams[0];
    private static readonly Team AwayTeam = League.Teams[1];
    private static readonly ScheduledMatch Scheduled = new(new DateOnly(2026, 10, 1), HomeTeam.Id, AwayTeam.Id);

    // Home: A scores twice (once on the power play, assisted by B); C takes a major.
    // Away: D scores at even strength; E takes the minor that gave home its power play.
    private static readonly PlayerId A = HomeTeam.Lineup.ForwardLines[0].Centre.Id;
    private static readonly PlayerId B = HomeTeam.Lineup.ForwardLines[0].LeftWing.Id;
    private static readonly PlayerId C = HomeTeam.Lineup.DefencePairs[0].LeftDefence.Id;
    private static readonly PlayerId D = AwayTeam.Lineup.ForwardLines[0].Centre.Id;
    private static readonly PlayerId E = AwayTeam.Lineup.DefencePairs[0].LeftDefence.Id;

    private static readonly MatchGoal FirstGoal = Goal(1, 300, HomeTeam.Id, A, B, GoalSituation.PowerPlay);
    private static readonly MatchGoal SecondGoal = Goal(2, 600, AwayTeam.Id, D, null, GoalSituation.EvenStrength);
    private static readonly MatchGoal ThirdGoal = Goal(3, 900, HomeTeam.Id, A, null, GoalSituation.EvenStrength);
    private static readonly MatchPenalty FirstPenalty = Penalty(1, 240, AwayTeam.Id, E, PenaltyKind.Minor);
    private static readonly MatchPenalty SecondPenalty = Penalty(2, 60, HomeTeam.Id, A, PenaltyKind.Minor);
    private static readonly MatchPenalty ThirdPenalty = Penalty(2, 300, HomeTeam.Id, C, PenaltyKind.Major);

    [Fact]
    public void AMatchKeepsItsScoringAndPenaltySummariesInOrder()
    {
        var match = Build();

        Assert.Equal([FirstGoal, SecondGoal, ThirdGoal], match.Goals);
        Assert.Equal([FirstPenalty, SecondPenalty, ThirdPenalty], match.Penalties);
        Assert.Equal([B], FirstGoal.AssistIds);
        Assert.Equal([2, 2, 5], match.Penalties.Select(penalty => penalty.Minutes));
    }

    public static TheoryData<string> MismatchedSummaries() =>
    [
        "missing goal",
        "extra goal",
        "wrong scorer",
        "wrong situation",
        "unmarked empty net",
        "missing assist",
        "missing penalty",
        "wrong penalty length",
        "penalty to an opponent",
        "goals out of order",
        "penalties out of order",
    ];

    [Theory]
    [MemberData(nameof(MismatchedSummaries))]
    public void ASummaryThatDisagreesWithTheBoxScoresIsRejected(string mismatch)
    {
        IEnumerable<MatchGoal> goals = mismatch switch
        {
            "missing goal" => [FirstGoal, SecondGoal],
            "extra goal" => [FirstGoal, SecondGoal, ThirdGoal, Goal(3, 1000, HomeTeam.Id, A, null, GoalSituation.EvenStrength)],
            "wrong scorer" => [FirstGoal, SecondGoal, Goal(3, 900, HomeTeam.Id, B, null, GoalSituation.EvenStrength)],
            "wrong situation" => [Goal(1, 300, HomeTeam.Id, A, B, GoalSituation.EvenStrength), SecondGoal, ThirdGoal],
            "missing assist" => [Goal(1, 300, HomeTeam.Id, A, null, GoalSituation.PowerPlay), SecondGoal, ThirdGoal],
            "goals out of order" => [FirstGoal, ThirdGoal, SecondGoal],
            _ => [FirstGoal, SecondGoal, ThirdGoal],
        };
        IEnumerable<MatchPenalty> penalties = mismatch switch
        {
            "missing penalty" => [FirstPenalty, SecondPenalty],
            "wrong penalty length" => [FirstPenalty, SecondPenalty, Penalty(2, 300, HomeTeam.Id, C, PenaltyKind.DoubleMinor)],
            "penalty to an opponent" => [FirstPenalty, SecondPenalty, Penalty(2, 300, AwayTeam.Id, C, PenaltyKind.Major)],
            "penalties out of order" => [SecondPenalty, FirstPenalty, ThirdPenalty],
            _ => [FirstPenalty, SecondPenalty, ThirdPenalty],
        };

        // The home scorer's second goal is marked empty-net in the box score but not the summary.
        var homeEmptyNetGoals = mismatch == "unmarked empty net" ? 1 : 0;

        Assert.Throws<ArgumentException>(() => Build(goals, penalties, homeEmptyNetGoals));
    }

    [Fact]
    public void EachTeamsShotTotalsMustBeTheOthersReversed()
    {
        var home = In(StrengthSituation.PowerPlay, Shots(attemptsFor: 3, shotsFor: 2, goalsFor: 1, expectedGoalsFor: 0.4));

        Assert.Equal(home, Build(homeShotTotals: home, awayShotTotals: home.Reverse()).Home.ShotTotals);
        Assert.Throws<ArgumentException>(() => Build(homeShotTotals: home, awayShotTotals: SituationalShotTotals.None));

        // The away side must see the home power play as its penalty kill, not its own power play.
        Assert.Throws<ArgumentException>(() => Build(
            homeShotTotals: home,
            awayShotTotals: In(StrengthSituation.PowerPlay, home.PowerPlay.Reverse())));
    }

    [Fact]
    public void ATeamsShotTotalsCannotExceedItsSkatersAttemptsShotsGoalsOrExpectedGoals()
    {
        Assert.Throws<ArgumentException>(() => Side(HomeTeam, [Skater(A, 1, shots: 2, shotAttempts: 3, expectedGoals: 0.5)],
            In(StrengthSituation.FiveOnFive, Shots(attemptsFor: 4))));
        Assert.Throws<ArgumentException>(() => Side(HomeTeam, [Skater(A, 1, shots: 2, shotAttempts: 3, expectedGoals: 0.5)],
            In(StrengthSituation.FiveOnFive, Shots(attemptsFor: 3, shotsFor: 3))));
        Assert.Throws<ArgumentException>(() => Side(HomeTeam, [Skater(A, 1, shots: 2, shotAttempts: 3, expectedGoals: 0.5)],
            In(StrengthSituation.FiveOnFive, Shots(attemptsFor: 3, shotsFor: 2, goalsFor: 2))));
        Assert.Throws<ArgumentException>(() => Side(HomeTeam, [Skater(A, 1, shots: 2, shotAttempts: 3, expectedGoals: 0.5)],
            In(StrengthSituation.FiveOnFive, Shots(attemptsFor: 3, shotsFor: 2, goalsFor: 1, expectedGoalsFor: 0.6))));
    }

    [Fact]
    public void ASkatersOnIceTotalsCannotExceedTheTeamsInAnySituation()
    {
        var team = In(StrengthSituation.FiveOnFive, Shots(attemptsFor: 3, attemptsAgainst: 5));
        var onIce = In(StrengthSituation.FiveOnFive, Shots(attemptsFor: 3, attemptsAgainst: 5));

        Assert.NotNull(Side(HomeTeam, [Skater(A, shots: 0, shotAttempts: 3, onIce: onIce)], team));
        Assert.Throws<ArgumentException>(() => Side(HomeTeam, [Skater(A, shots: 0, shotAttempts: 3, onIce: onIce)],
            In(StrengthSituation.FiveOnFive, Shots(attemptsFor: 3, attemptsAgainst: 4))));
        Assert.Throws<ArgumentException>(() => Side(HomeTeam, [Skater(A, shots: 0, shotAttempts: 3, onIce: onIce)],
            In(StrengthSituation.Other, Shots(attemptsFor: 3, attemptsAgainst: 5))));
    }

    [Fact]
    public void AGoalNeedsAPrimaryAssistBeforeASecondaryOneAndDistinctPlayers()
    {
        Assert.Throws<ArgumentException>(() => Goal(1, 0, HomeTeam.Id, A, null, GoalSituation.EvenStrength, secondary: B));
        Assert.Throws<ArgumentException>(() => Goal(1, 0, HomeTeam.Id, A, A, GoalSituation.EvenStrength));
        Assert.Throws<ArgumentException>(() => Goal(1, 0, HomeTeam.Id, A, B, GoalSituation.EvenStrength, secondary: B));
        Assert.Equal([B, C], Goal(1, 0, HomeTeam.Id, A, B, GoalSituation.EvenStrength, secondary: C).AssistIds);
    }

    [Fact]
    public void APenaltyShotGoalIsUnassisted()
    {
        Assert.Throws<ArgumentException>(() => Goal(1, 0, HomeTeam.Id, A, B, GoalSituation.PenaltyShot));
        Assert.Empty(Goal(1, 0, HomeTeam.Id, A, null, GoalSituation.PenaltyShot).AssistIds);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 1201)]
    public void SummaryTimesMustBeInAPeriodAndWithinTwentyMinutes(int period, int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Goal(period, seconds, HomeTeam.Id, A, null, GoalSituation.EvenStrength));
        Assert.Throws<ArgumentOutOfRangeException>(() => Penalty(period, seconds, HomeTeam.Id, A, PenaltyKind.Minor));
    }

    [Fact]
    public void SummaryTimesAreWholeSeconds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatchPenalty(
            1, TimeSpan.FromMilliseconds(1500), HomeTeam.Id, A, Infraction.Hooking, PenaltyKind.Minor));
    }

    [Theory]
    [InlineData(PenaltyKind.Minor, 2)]
    [InlineData(PenaltyKind.DoubleMinor, 4)]
    [InlineData(PenaltyKind.Major, 5)]
    [InlineData(PenaltyKind.Misconduct, 10)]
    [InlineData(PenaltyKind.GameMisconduct, 10)]
    [InlineData(PenaltyKind.PenaltyShot, 0)]
    public void APenaltyChargesTheMinutesOfItsKind(PenaltyKind kind, int minutes)
    {
        Assert.Equal(minutes, Penalty(1, 0, HomeTeam.Id, A, kind).Minutes);
    }

    [Fact]
    public void UndefinedSummaryValuesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Goal(1, 0, HomeTeam.Id, A, null, (GoalSituation)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatchPenalty(
            1, TimeSpan.Zero, HomeTeam.Id, A, (Infraction)99, PenaltyKind.Minor));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatchPenalty(
            1, TimeSpan.Zero, HomeTeam.Id, A, Infraction.Hooking, (PenaltyKind)99));
    }

    private static CompletedMatch Build(
        IEnumerable<MatchGoal>? goals = null,
        IEnumerable<MatchPenalty>? penalties = null,
        int homeEmptyNetGoals = 0,
        SituationalShotTotals? homeShotTotals = null,
        SituationalShotTotals? awayShotTotals = null)
    {
        var home = Side(
            HomeTeam,
            [
                Skater(A, 2, shots: 10, shotAttempts: 14, expectedGoals: 1.5, powerPlayGoals: 1, emptyNetGoals: homeEmptyNetGoals, penaltyMinutes: 2),
                Skater(B, 0, 1, powerPlayAssists: 1),
                Skater(C, penaltyMinutes: 5),
            ],
            homeShotTotals ?? SituationalShotTotals.None,
            powerPlayOpportunities: 1,
            goalie: Goalie(HomeTeam.Lineup.StartingGoalie.Id, 8, 1, 1.0));
        var away = Side(
            AwayTeam,
            [Skater(D, 1, shots: 8, shotAttempts: 12, expectedGoals: 1.0), Skater(E, penaltyMinutes: 2)],
            awayShotTotals ?? SituationalShotTotals.None,
            powerPlayOpportunities: 2,
            goalie: Goalie(AwayTeam.Lineup.StartingGoalie.Id, 10 - homeEmptyNetGoals, 2 - homeEmptyNetGoals, 1.5));

        return new CompletedMatch(
            Scheduled,
            home,
            away,
            MatchDecision.Regulation,
            goals ?? [FirstGoal, SecondGoal, ThirdGoal],
            penalties ?? [FirstPenalty, SecondPenalty, ThirdPenalty]);
    }

    private static CompletedMatchTeam Side(
        Team team,
        IReadOnlyList<SkaterBoxScore> skaters,
        SituationalShotTotals shotTotals,
        int powerPlayOpportunities = 0,
        GoalieBoxScore? goalie = null) =>
        new(
            team.Id,
            skaters.Sum(skater => skater.Goals),
            skaters.Sum(skater => skater.Shots),
            powerPlayOpportunities,
            skaters,
            goalie ?? Goalie(team.Lineup.StartingGoalie.Id, 0, 0),
            shotTotals);

    private static MatchGoal Goal(
        int period,
        int seconds,
        TeamId teamId,
        PlayerId scorer,
        PlayerId? primary,
        GoalSituation situation,
        PlayerId? secondary = null) =>
        new(period, TimeSpan.FromSeconds(seconds), teamId, scorer, primary, secondary, situation, isEmptyNet: false);

    private static MatchPenalty Penalty(int period, int seconds, TeamId teamId, PlayerId playerId, PenaltyKind kind) =>
        new(period, TimeSpan.FromSeconds(seconds), teamId, playerId, Infraction.Hooking, kind);
}