using HockeySim.Domain;
using HockeySim.Simulation.Events;

namespace HockeySim.Simulation.Play;

/// <summary>
/// Derives every individual and team statistic from the play-by-play, so they always reconcile
/// with the events and with each other. Only time on ice, which comes from the shifts, and
/// power-play opportunities, which come from the penalties being served, are kept by the match.
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
                case PenaltyEvent penalty:
                    Skater(penalty.PlayerId).PenaltyMinutes += penalty.Minutes;
                    break;
            }
        }
    }

    /// <param name="score">The final score, including a shootout deciding goal.</param>
    public MatchTeamResult TeamResult(MatchSide side, MatchSide opponent, int score)
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
                tally.ExpectedGoals,
                tally.PenaltyMinutes,
                tally.PowerPlayGoals,
                tally.PowerPlayAssists,
                tally.ShorthandedGoals,
                tally.ShorthandedAssists);
        });
        var goalie = new GoalieMatchStatistics(
            side.Goalie.Id,
            ShotsAgainst: against.Shots,
            GoalsAgainst: against.Goals,
            ExpectedGoalsAgainst: against.ExpectedGoals,
            TimeOnIce: TimeSpan.FromSeconds(side.GoalieTimeOnIceSeconds));

        return new MatchTeamResult(side.TeamId, score, team.Shots, side.PowerPlayOpportunities, skaters, goalie);
    }

    private void AddGoal(GoalEvent goal)
    {
        var scorer = Skater(goal.ScorerId);
        scorer.Goals++;
        scorer.Shots++;
        scorer.ShotAttempts++;
        scorer.ExpectedGoals += goal.ExpectedGoals;
        if (goal.Situation == GoalSituation.PowerPlay)
        {
            scorer.PowerPlayGoals++;
        }
        else if (goal.Situation == GoalSituation.Shorthanded)
        {
            scorer.ShorthandedGoals++;
        }

        foreach (var assist in new[] { goal.PrimaryAssistId, goal.SecondaryAssistId })
        {
            if (assist is { } assistId)
            {
                var assister = Skater(assistId);
                assister.Assists++;
                if (goal.Situation == GoalSituation.PowerPlay)
                {
                    assister.PowerPlayAssists++;
                }
                else if (goal.Situation == GoalSituation.Shorthanded)
                {
                    assister.ShorthandedAssists++;
                }
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
    /// team's, except for a power-play or penalty-shot goal, which counts toward neither. An extra
    /// attacker during a delayed penalty is on the ice like any other skater.
    /// </summary>
    private void AddPlusMinus(GoalEvent goal)
    {
        if (goal.Situation is GoalSituation.PowerPlay or GoalSituation.PenaltyShot)
        {
            return;
        }

        var onIce = goal.OnIce;
        var (scoring, conceding) = goal.TeamId == _homeTeamId
            ? (onIce.HomeSkaters, onIce.AwaySkaters)
            : (onIce.AwaySkaters, onIce.HomeSkaters);

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

        public int PenaltyMinutes { get; set; }

        public int PowerPlayGoals { get; set; }

        public int PowerPlayAssists { get; set; }

        public int ShorthandedGoals { get; set; }

        public int ShorthandedAssists { get; set; }
    }

    private sealed class TeamTally
    {
        public int Goals { get; set; }

        public int Shots { get; set; }

        public double ExpectedGoals { get; set; }
    }
}