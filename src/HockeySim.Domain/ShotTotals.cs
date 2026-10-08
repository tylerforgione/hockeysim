namespace HockeySim.Domain;

/// <summary>
/// Shot attempts, unblocked attempts, shots on goal, goals, and expected goals for and against
/// over a span of play: while a skater was on the ice, or for a whole team. Each is a subset of the
/// one before it, apart from expected goals. Their shares are the usual possession measures: Corsi
/// counts every attempt and Fenwick only unblocked attempts. Penalty shots are not counted, as they
/// are not in plus/minus, because the skaters on the ice took no part in them.
/// </summary>
public sealed record ShotTotals
{
    public ShotTotals(
        int attemptsFor,
        int attemptsAgainst,
        int unblockedAttemptsFor,
        int unblockedAttemptsAgainst,
        int shotsFor,
        int shotsAgainst,
        int goalsFor,
        int goalsAgainst,
        double expectedGoalsFor,
        double expectedGoalsAgainst)
    {
        ThrowIfInconsistent(attemptsFor, unblockedAttemptsFor, shotsFor, goalsFor);
        ThrowIfInconsistent(attemptsAgainst, unblockedAttemptsAgainst, shotsAgainst, goalsAgainst);
        ExpectedGoalTotals.ThrowIfInvalid(expectedGoalsFor, nameof(expectedGoalsFor));
        ExpectedGoalTotals.ThrowIfInvalid(expectedGoalsAgainst, nameof(expectedGoalsAgainst));

        AttemptsFor = attemptsFor;
        AttemptsAgainst = attemptsAgainst;
        UnblockedAttemptsFor = unblockedAttemptsFor;
        UnblockedAttemptsAgainst = unblockedAttemptsAgainst;
        ShotsFor = shotsFor;
        ShotsAgainst = shotsAgainst;
        GoalsFor = goalsFor;
        GoalsAgainst = goalsAgainst;
        ExpectedGoalsFor = expectedGoalsFor;
        ExpectedGoalsAgainst = expectedGoalsAgainst;
    }

    public static ShotTotals None { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Every shot attempt: on goal, missed, or blocked (Corsi for).</summary>
    public int AttemptsFor { get; }

    /// <summary>Every opponent shot attempt (Corsi against).</summary>
    public int AttemptsAgainst { get; }

    /// <summary>Attempts that were not blocked (Fenwick for).</summary>
    public int UnblockedAttemptsFor { get; }

    /// <summary>Opponent attempts that were not blocked (Fenwick against).</summary>
    public int UnblockedAttemptsAgainst { get; }

    public int ShotsFor { get; }

    public int ShotsAgainst { get; }

    public int GoalsFor { get; }

    public int GoalsAgainst { get; }

    public double ExpectedGoalsFor { get; }

    public double ExpectedGoalsAgainst { get; }

    /// <summary>The share of all shot attempts that were for, or <see langword="null"/> before any.</summary>
    public double? CorsiPercentage => Share(AttemptsFor, AttemptsAgainst);

    /// <summary>The share of unblocked attempts that were for, or <see langword="null"/> before any.</summary>
    public double? FenwickPercentage => Share(UnblockedAttemptsFor, UnblockedAttemptsAgainst);

    /// <summary>The share of shots on goal that were for, or <see langword="null"/> before any.</summary>
    public double? ShotsPercentage => Share(ShotsFor, ShotsAgainst);

    /// <summary>The share of goals that were for, or <see langword="null"/> before any.</summary>
    public double? GoalsPercentage => Share(GoalsFor, GoalsAgainst);

    /// <summary>
    /// The share of expected goals that were for, or <see langword="null"/> while both are zero.
    /// </summary>
    public double? ExpectedGoalsPercentage =>
        ExpectedGoalsFor + ExpectedGoalsAgainst == 0
            ? null
            : ExpectedGoalsFor / (ExpectedGoalsFor + ExpectedGoalsAgainst);

    /// <summary>The same play seen from the opponent's side: for and against swapped.</summary>
    public ShotTotals Reverse() =>
        new(
            AttemptsAgainst,
            AttemptsFor,
            UnblockedAttemptsAgainst,
            UnblockedAttemptsFor,
            ShotsAgainst,
            ShotsFor,
            GoalsAgainst,
            GoalsFor,
            ExpectedGoalsAgainst,
            ExpectedGoalsFor);

    public ShotTotals Add(ShotTotals other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new(
            AttemptsFor + other.AttemptsFor,
            AttemptsAgainst + other.AttemptsAgainst,
            UnblockedAttemptsFor + other.UnblockedAttemptsFor,
            UnblockedAttemptsAgainst + other.UnblockedAttemptsAgainst,
            ShotsFor + other.ShotsFor,
            ShotsAgainst + other.ShotsAgainst,
            GoalsFor + other.GoalsFor,
            GoalsAgainst + other.GoalsAgainst,
            ExpectedGoalsFor + other.ExpectedGoalsFor,
            ExpectedGoalsAgainst + other.ExpectedGoalsAgainst);
    }

    /// <summary>
    /// Whether these totals count the same play as <paramref name="other"/>, allowing for
    /// expected goals summed in a different order.
    /// </summary>
    internal bool Matches(ShotTotals other) =>
        AttemptsFor == other.AttemptsFor
        && AttemptsAgainst == other.AttemptsAgainst
        && UnblockedAttemptsFor == other.UnblockedAttemptsFor
        && UnblockedAttemptsAgainst == other.UnblockedAttemptsAgainst
        && ShotsFor == other.ShotsFor
        && ShotsAgainst == other.ShotsAgainst
        && GoalsFor == other.GoalsFor
        && GoalsAgainst == other.GoalsAgainst
        && ExpectedGoalTotals.AreEqual(ExpectedGoalsFor, other.ExpectedGoalsFor)
        && ExpectedGoalTotals.AreEqual(ExpectedGoalsAgainst, other.ExpectedGoalsAgainst);

    /// <summary>
    /// Whether every count is at most <paramref name="bound"/>'s, as a part of some play is
    /// within the whole of it.
    /// </summary>
    internal bool IsWithin(ShotTotals bound) =>
        AttemptsFor <= bound.AttemptsFor
        && AttemptsAgainst <= bound.AttemptsAgainst
        && UnblockedAttemptsFor <= bound.UnblockedAttemptsFor
        && UnblockedAttemptsAgainst <= bound.UnblockedAttemptsAgainst
        && ShotsFor <= bound.ShotsFor
        && ShotsAgainst <= bound.ShotsAgainst
        && GoalsFor <= bound.GoalsFor
        && GoalsAgainst <= bound.GoalsAgainst
        && ExpectedGoalsFor <= bound.ExpectedGoalsFor + ExpectedGoalTotals.Tolerance
        && ExpectedGoalsAgainst <= bound.ExpectedGoalsAgainst + ExpectedGoalTotals.Tolerance;

    private static double? Share(int forCount, int againstCount) =>
        forCount + againstCount == 0 ? null : forCount / (double)(forCount + againstCount);

    private static void ThrowIfInconsistent(int attempts, int unblockedAttempts, int shots, int goals)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(goals);
        ArgumentOutOfRangeException.ThrowIfLessThan(shots, goals);
        ArgumentOutOfRangeException.ThrowIfLessThan(unblockedAttempts, shots);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, unblockedAttempts);
    }
}