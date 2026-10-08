using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// One team's side of a completed match: its score, shots, and the box score of every player who
/// appeared.
/// </summary>
public sealed class CompletedMatchTeam
{
    private readonly ReadOnlyCollection<SkaterBoxScore> _skaters;

    /// <param name="score">
    /// The decisive final score. A shootout winner's score includes the one deciding goal that no
    /// player scored.
    /// </param>
    /// <param name="shots">
    /// Shots on goal in regulation and overtime, equal to the skaters' shots; shootout attempts are
    /// excluded.
    /// </param>
    /// <param name="powerPlayOpportunities">
    /// Opponent penalties that gave the team a manpower advantage, each counted once.
    /// </param>
    /// <param name="shotTotals">
    /// Both teams' shot attempts, shots, goals, and expected goals by strength situation from this
    /// team's side, excluding penalty shots.
    /// </param>
    public CompletedMatchTeam(
        TeamId teamId,
        int score,
        int shots,
        int powerPlayOpportunities,
        IEnumerable<SkaterBoxScore> skaters,
        GoalieBoxScore goalie,
        SituationalShotTotals shotTotals)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(score);
        ArgumentOutOfRangeException.ThrowIfNegative(shots);
        ArgumentOutOfRangeException.ThrowIfNegative(powerPlayOpportunities);
        ArgumentNullException.ThrowIfNull(skaters);
        ArgumentNullException.ThrowIfNull(goalie);
        ArgumentNullException.ThrowIfNull(shotTotals);

        var skaterList = skaters.ToList();
        if (skaterList.Any(skater => skater is null))
        {
            throw new ArgumentException("A completed match cannot contain a missing skater box score.", nameof(skaters));
        }

        var appearingPlayerIds = skaterList.Select(skater => skater.PlayerId).Append(goalie.PlayerId).ToList();
        if (appearingPlayerIds.Distinct().Count() != appearingPlayerIds.Count)
        {
            throw new ArgumentException("A player can appear only once for a team in a match.", nameof(skaters));
        }

        if (skaterList.Sum(skater => skater.Shots) != shots)
        {
            throw new ArgumentException("A team's shots must equal its skaters' shots on goal.", nameof(skaters));
        }

        var playerGoals = skaterList.Sum(skater => skater.Goals);

        // Each goal credits at most a primary and a secondary assist.
        if (skaterList.Sum(skater => skater.Assists) > 2 * playerGoals
            || skaterList.Sum(skater => skater.PowerPlayAssists) > 2 * skaterList.Sum(skater => skater.PowerPlayGoals)
            || skaterList.Sum(skater => skater.ShorthandedAssists) > 2 * skaterList.Sum(skater => skater.ShorthandedGoals))
        {
            throw new ArgumentException("A team cannot record more than two assists per goal.", nameof(skaters));
        }

        // A power-play goal needs a power play.
        if (powerPlayOpportunities == 0 && skaterList.Any(skater => skater.PowerPlayGoals > 0))
        {
            throw new ArgumentException("A team cannot score on the power play without a power-play opportunity.", nameof(skaters));
        }

        // The team's own totals leave out only penalty shots, and a skater is on the ice for part of the play.
        var all = shotTotals.All;
        if (all.AttemptsFor > skaterList.Sum(skater => skater.ShotAttempts)
            || all.ShotsFor > shots
            || all.GoalsFor > playerGoals
            || all.ExpectedGoalsFor > skaterList.Sum(skater => skater.ExpectedGoals) + ExpectedGoalTotals.Tolerance)
        {
            throw new ArgumentException("A team's shot totals cannot exceed its skaters' attempts, shots, goals, and expected goals.", nameof(shotTotals));
        }

        if (skaterList.Any(skater => !skater.OnIce.IsWithin(shotTotals)))
        {
            throw new ArgumentException("A skater's on-ice shot totals cannot exceed the team's in any situation.", nameof(skaters));
        }

        TeamId = teamId;
        Score = score;
        Shots = shots;
        PowerPlayOpportunities = powerPlayOpportunities;
        _skaters = skaterList.AsReadOnly();
        Goalie = goalie;
        ShotTotals = shotTotals;
    }

    public TeamId TeamId { get; }

    public int Score { get; }

    public int Shots { get; }

    public int PowerPlayOpportunities { get; }

    public int PowerPlayGoals => _skaters.Sum(skater => skater.PowerPlayGoals);

    public int ShorthandedGoals => _skaters.Sum(skater => skater.ShorthandedGoals);

    /// <summary>Goals scored while the opponent's goalie was pulled for an extra attacker.</summary>
    public int EmptyNetGoals => _skaters.Sum(skater => skater.EmptyNetGoals);

    public int PenaltyMinutes => _skaters.Sum(skater => skater.PenaltyMinutes);

    public IReadOnlyList<SkaterBoxScore> Skaters => _skaters;

    public GoalieBoxScore Goalie { get; }

    /// <summary>
    /// Both teams' shot totals by strength situation from this team's side, excluding penalty shots.
    /// </summary>
    public SituationalShotTotals ShotTotals { get; }

    public int FaceoffsWon => _skaters.Sum(skater => skater.FaceoffsWon);

    public int FaceoffsLost => _skaters.Sum(skater => skater.FaceoffsLost);

    /// <summary>Goals scored by players, excluding a shootout deciding goal.</summary>
    public int PlayerGoals => _skaters.Sum(skater => skater.Goals);

    /// <summary>Every shot attempt: on goal, missed, or blocked.</summary>
    public int ShotAttempts => _skaters.Sum(skater => skater.ShotAttempts);

    /// <summary>The summed expected-goal value of the team's unblocked attempts.</summary>
    public double ExpectedGoals => _skaters.Sum(skater => skater.ExpectedGoals);
}