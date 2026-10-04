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
    /// <param name="shots">Shots on goal in regulation and overtime; shootout attempts are excluded.</param>
    public CompletedMatchTeam(
        TeamId teamId,
        int score,
        int shots,
        IEnumerable<SkaterBoxScore> skaters,
        GoalieBoxScore goalie)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(score);
        ArgumentOutOfRangeException.ThrowIfNegative(shots);
        ArgumentNullException.ThrowIfNull(skaters);
        ArgumentNullException.ThrowIfNull(goalie);

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

        var playerGoals = skaterList.Sum(skater => skater.Goals);
        if (playerGoals > shots)
        {
            throw new ArgumentException("A team cannot score more player goals than it has shots.", nameof(skaters));
        }

        // Each goal credits at most a primary and a secondary assist.
        if (skaterList.Sum(skater => skater.Assists) > 2 * playerGoals)
        {
            throw new ArgumentException("A team cannot record more than two assists per goal.", nameof(skaters));
        }

        TeamId = teamId;
        Score = score;
        Shots = shots;
        _skaters = skaterList.AsReadOnly();
        Goalie = goalie;
    }

    public TeamId TeamId { get; }

    public int Score { get; }

    public int Shots { get; }

    public IReadOnlyList<SkaterBoxScore> Skaters => _skaters;

    public GoalieBoxScore Goalie { get; }

    /// <summary>Goals scored by players, excluding a shootout deciding goal.</summary>
    public int PlayerGoals => _skaters.Sum(skater => skater.Goals);
}