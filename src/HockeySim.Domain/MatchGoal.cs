namespace HockeySim.Domain;

/// <summary>
/// One goal in a completed match's scoring summary. Shootout goals are not listed; a shootout
/// winner's deciding goal is in the score only.
/// </summary>
public sealed record MatchGoal
{
    /// <param name="period">One to three for regulation, then each overtime period in turn.</param>
    /// <param name="timeInPeriod">Elapsed time in the period, in whole seconds.</param>
    /// <param name="teamId">The scoring team.</param>
    /// <param name="secondaryAssistId">A second assist, present only with a primary assist.</param>
    /// <param name="isEmptyNet">Scored while the conceding team's goalie was pulled.</param>
    public MatchGoal(
        int period,
        TimeSpan timeInPeriod,
        TeamId teamId,
        PlayerId scorerId,
        PlayerId? primaryAssistId,
        PlayerId? secondaryAssistId,
        GoalSituation situation,
        bool isEmptyNet)
    {
        MatchClock.ThrowIfInvalid(period, timeInPeriod);

        if (!Enum.IsDefined(situation))
        {
            throw new ArgumentOutOfRangeException(nameof(situation), "The goal situation is not recognised.");
        }

        if (secondaryAssistId is not null && primaryAssistId is null)
        {
            throw new ArgumentException("A goal can have a secondary assist only with a primary assist.", nameof(secondaryAssistId));
        }

        if (primaryAssistId == scorerId || secondaryAssistId == scorerId
            || (secondaryAssistId is not null && secondaryAssistId == primaryAssistId))
        {
            throw new ArgumentException("A goal's scorer and assists must be different players.", nameof(primaryAssistId));
        }

        if (situation == GoalSituation.PenaltyShot && primaryAssistId is not null)
        {
            throw new ArgumentException("A penalty-shot goal is unassisted.", nameof(primaryAssistId));
        }

        Period = period;
        TimeInPeriod = timeInPeriod;
        TeamId = teamId;
        ScorerId = scorerId;
        PrimaryAssistId = primaryAssistId;
        SecondaryAssistId = secondaryAssistId;
        Situation = situation;
        IsEmptyNet = isEmptyNet;
    }

    public int Period { get; }

    public TimeSpan TimeInPeriod { get; }

    public TeamId TeamId { get; }

    public PlayerId ScorerId { get; }

    public PlayerId? PrimaryAssistId { get; }

    public PlayerId? SecondaryAssistId { get; }

    public GoalSituation Situation { get; }

    public bool IsEmptyNet { get; }

    /// <summary>The players credited with an assist: none, the primary, or both.</summary>
    public IEnumerable<PlayerId> AssistIds =>
        new[] { PrimaryAssistId, SecondaryAssistId }.OfType<PlayerId>();
}