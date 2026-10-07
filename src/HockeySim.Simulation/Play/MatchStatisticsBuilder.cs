using HockeySim.Domain;
using HockeySim.Simulation.Events;

namespace HockeySim.Simulation.Play;

/// <summary>
/// Derives every individual and team statistic from the play-by-play, so they always reconcile
/// with the events and with each other. Only time on ice comes from the skaters' shifts.
/// </summary>
internal sealed class MatchStatisticsBuilder
{
    private readonly Dictionary<PlayerId, SkaterTally> _skaters = [];
    private readonly Dictionary<TeamId, TeamTally> _teams = [];
    private readonly TeamId _homeTeamId;

    public MatchStatisticsBuilder(IEnumerable<MatchEvent> events, TeamId homeTeamId)
    {
        _homeTeamId = homeTeamId;

        foreach (var matchEvent in events)
        {
            switch (matchEvent)
            {
                case GoalEvent goal:
                    AddGoal(goal);
                    break;
                case ShotAttemptEvent attempt:
                    AddShotAttempt(attempt);
                    break;
                case FaceoffEvent faceoff:
                    Skater(faceoff.WinnerId).FaceoffsWon++;
                    Skater(faceoff.LoserId).FaceoffsLost++;
                    break;
                case HitEvent hit:
                    Skater(hit.HitterId).Hits++;
                    break;
                case TakeawayEvent takeaway:
                    Skater(takeaway.PlayerId).Takeaways++;
                    break;
                case GiveawayEvent giveaway:
                    Skater(giveaway.PlayerId).Giveaways++;
                    break;
            }
        }
    }

    /// <param name="score">The final score, including a shootout deciding goal.</param>
    public MatchTeamResult TeamResult(MatchSide side, MatchSide opponent, int score, TimeSpan playingTime)
    {
        var team = Team(side.TeamId);
        var against = Team(opponent.TeamId);
        var skaters = side.Skaters.Select(skater =>
        {
            var tally = Skater(skater.Id);
            return new SkaterMatchStatistics(
                skater.Id,
                tally.Goals,
                tally.Assists,
                tally.PlusMinus,
                TimeSpan.FromSeconds(skater.TimeOnIceSeconds),
                tally.Shots,
                tally.ShotAttempts,
                tally.Hits,
                tally.BlockedShots,
                tally.FaceoffsWon,
                tally.FaceoffsLost,
                tally.Takeaways,
                tally.Giveaways,
                tally.ExpectedGoals);
        });
        var goalie = new GoalieMatchStatistics(
            side.Goalie.Id,
            ShotsAgainst: against.Shots,
            GoalsAgainst: against.Goals,
            ExpectedGoalsAgainst: against.ExpectedGoals,
            TimeOnIce: playingTime);

        return new MatchTeamResult(side.TeamId, score, team.Shots, skaters, goalie);
    }

    private void AddGoal(GoalEvent goal)
    {
        var scorer = Skater(goal.ScorerId);
        scorer.Goals++;
        scorer.Shots++;
        scorer.ShotAttempts++;
        scorer.ExpectedGoals += goal.ExpectedGoals;

        foreach (var assist in new[] { goal.PrimaryAssistId, goal.SecondaryAssistId })
        {
            if (assist is { } assistId)
            {
                Skater(assistId).Assists++;
            }
        }

        var team = Team(goal.TeamId);
        team.Goals++;
        team.Shots++;
        team.ExpectedGoals += goal.ExpectedGoals;

        AddPlusMinus(goal);
    }

    /// <summary>
    /// Credits plus one to the scoring team's skaters on the ice and minus one to the conceding
    /// team's, except for a power-play goal, which counts toward neither.
    /// </summary>
    private void AddPlusMinus(GoalEvent goal)
    {
        var onIce = goal.OnIce;
        var (scoring, conceding) = goal.TeamId == _homeTeamId
            ? (onIce.HomeSkaters, onIce.AwaySkaters)
            : (onIce.AwaySkaters, onIce.HomeSkaters);
        if (scoring.Count > conceding.Count)
        {
            return;
        }

        foreach (var playerId in scoring)
        {
            Skater(playerId).PlusMinus++;
        }

        foreach (var playerId in conceding)
        {
            Skater(playerId).PlusMinus--;
        }
    }

    private void AddShotAttempt(ShotAttemptEvent attempt)
    {
        var shooter = Skater(attempt.ShooterId);
        shooter.ShotAttempts++;
        var team = Team(attempt.TeamId);

        if (attempt.IsOnGoal)
        {
            shooter.Shots++;
            team.Shots++;
        }

        if (attempt.ExpectedGoals is { } expectedGoals)
        {
            shooter.ExpectedGoals += expectedGoals;
            team.ExpectedGoals += expectedGoals;
        }

        if (attempt.BlockerId is { } blockerId)
        {
            Skater(blockerId).BlockedShots++;
        }
    }

    private SkaterTally Skater(PlayerId playerId)
    {
        if (!_skaters.TryGetValue(playerId, out var tally))
        {
            tally = new SkaterTally();
            _skaters[playerId] = tally;
        }

        return tally;
    }

    private TeamTally Team(TeamId teamId)
    {
        if (!_teams.TryGetValue(teamId, out var tally))
        {
            tally = new TeamTally();
            _teams[teamId] = tally;
        }

        return tally;
    }

    private sealed class SkaterTally
    {
        public int Goals { get; set; }

        public int Assists { get; set; }

        public int PlusMinus { get; set; }

        public int Shots { get; set; }

        public int ShotAttempts { get; set; }

        public int Hits { get; set; }

        public int BlockedShots { get; set; }

        public int FaceoffsWon { get; set; }

        public int FaceoffsLost { get; set; }

        public int Takeaways { get; set; }

        public int Giveaways { get; set; }

        public double ExpectedGoals { get; set; }
    }

    private sealed class TeamTally
    {
        public int Goals { get; set; }

        public int Shots { get; set; }

        public double ExpectedGoals { get; set; }
    }
}