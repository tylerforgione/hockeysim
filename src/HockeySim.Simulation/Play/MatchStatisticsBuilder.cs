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
    private readonly TeamId _awayTeamId;

    public MatchStatisticsBuilder(IEnumerable<MatchEvent> events, TeamId homeTeamId, TeamId awayTeamId)
    {
        _homeTeamId = homeTeamId;
        _awayTeamId = awayTeamId;

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
        var skaters = side.AppearingSkaters.Select(skater =>
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
                tally.ShorthandedAssists,
                tally.EmptyNetGoals,
                tally.OnIce.ToTotals());
        });

        // Empty-net goals were scored with the goalie on the bench, so they are not held against them.
        var goalie = new GoalieMatchStatistics(
            side.Goalie.Id,
            ShotsAgainst: against.Shots - against.EmptyNetGoals,
            GoalsAgainst: against.Goals - against.EmptyNetGoals,
            ExpectedGoalsAgainst: against.ExpectedGoals,
            TimeOnIce: TimeSpan.FromSeconds(side.GoalieTimeOnIceSeconds));

        return new MatchTeamResult(
            side.TeamId, score, team.Shots, side.PowerPlayOpportunities, skaters, goalie, team.ShotTotals.ToTotals());
    }

    private void AddGoal(GoalEvent goal)
    {
        var scorer = Skater(goal.ScorerId);
        scorer.Goals++;
        scorer.Shots++;
        scorer.ShotAttempts++;
        scorer.ExpectedGoals += goal.ExpectedGoals ?? 0;
        if (goal.IsEmptyNet)
        {
            scorer.EmptyNetGoals++;
        }

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
        team.ExpectedGoals += goal.ExpectedGoals ?? 0;
        if (goal.IsEmptyNet)
        {
            team.EmptyNetGoals++;
        }

        AddPlusMinus(goal);
        if (goal.Situation != GoalSituation.PenaltyShot)
        {
            AddOnIce(goal, goal.TeamId, new ShotCount(IsBlocked: false, IsOnGoal: true, IsGoal: true, goal.ExpectedGoals ?? 0));
        }
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

        if (!attempt.Context.IsPenaltyShot)
        {
            AddOnIce(
                attempt,
                attempt.TeamId,
                new ShotCount(attempt.BlockerId is not null, attempt.IsOnGoal, IsGoal: false, attempt.ExpectedGoals ?? 0));
        }
    }

    /// <summary>
    /// Counts a shot attempt for the shooting team and every skater it had on the ice, and against
    /// the defending team's, each in its own strength situation. Penalty shots are left out, as
    /// they are from plus/minus, because the skaters on the ice took no part.
    /// </summary>
    private void AddOnIce(MatchEvent shot, TeamId shootingTeamId, ShotCount count)
    {
        var onIce = shot.OnIce;
        var shootingIsHome = shootingTeamId == _homeTeamId;
        var (shooters, defenders) = shootingIsHome
            ? (onIce.HomeSkaters, onIce.AwaySkaters)
            : (onIce.AwaySkaters, onIce.HomeSkaters);
        var (shootingGoalie, defendingGoalie) = shootingIsHome
            ? (onIce.HomeGoalie, onIce.AwayGoalie)
            : (onIce.AwayGoalie, onIce.HomeGoalie);

        var situation = SituationFor(
            Manpower(shooters.Count, shootingGoalie),
            Manpower(defenders.Count, defendingGoalie),
            bothGoaliesInNet: shootingGoalie is not null && defendingGoalie is not null);
        var defendingSituation = situation switch
        {
            StrengthSituation.PowerPlay => StrengthSituation.PenaltyKill,
            StrengthSituation.PenaltyKill => StrengthSituation.PowerPlay,
            _ => situation,
        };

        Team(shootingTeamId).ShotTotals.For(situation, count);
        Team(shootingIsHome ? _awayTeamId : _homeTeamId).ShotTotals.Against(defendingSituation, count);
        foreach (var playerId in shooters)
        {
            Skater(playerId).OnIce.For(situation, count);
        }

        foreach (var playerId in defenders)
        {
            Skater(playerId).OnIce.Against(defendingSituation, count);
        }
    }

    /// <summary>
    /// The skaters the penalties allow a team: those on the ice, less the extra attacker that
    /// replaces a pulled goalie.
    /// </summary>
    private static int Manpower(int skatersOnIce, PlayerId? goalie) => goalie is null ? skatersOnIce - 1 : skatersOnIce;

    private static StrengthSituation SituationFor(int manpower, int opponentManpower, bool bothGoaliesInNet) =>
        manpower > opponentManpower ? StrengthSituation.PowerPlay
        : manpower < opponentManpower ? StrengthSituation.PenaltyKill
        : manpower == 5 && bothGoaliesInNet ? StrengthSituation.FiveOnFive
        : StrengthSituation.Other;

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

        public int EmptyNetGoals { get; set; }

        public SituationalShotTally OnIce { get; } = new();
    }

    private sealed class TeamTally
    {
        public int Goals { get; set; }

        public int Shots { get; set; }

        public double ExpectedGoals { get; set; }

        public int EmptyNetGoals { get; set; }

        public SituationalShotTally ShotTotals { get; } = new();
    }

    /// <summary>One shot attempt, as it counts toward attempts, unblocked attempts, shots, and goals.</summary>
    private readonly record struct ShotCount(bool IsBlocked, bool IsOnGoal, bool IsGoal, double ExpectedGoals);

    private sealed class SituationalShotTally
    {
        private readonly ShotTally[] _for = [new(), new(), new(), new()];
        private readonly ShotTally[] _against = [new(), new(), new(), new()];

        public void For(StrengthSituation situation, ShotCount count) => _for[(int)situation].Add(count);

        public void Against(StrengthSituation situation, ShotCount count) => _against[(int)situation].Add(count);

        public SituationalShotTotals ToTotals() =>
            new(
                Totals(StrengthSituation.FiveOnFive),
                Totals(StrengthSituation.PowerPlay),
                Totals(StrengthSituation.PenaltyKill),
                Totals(StrengthSituation.Other));

        private ShotTotals Totals(StrengthSituation situation)
        {
            var (shotsFor, against) = (_for[(int)situation], _against[(int)situation]);
            return new ShotTotals(
                shotsFor.Attempts,
                against.Attempts,
                shotsFor.UnblockedAttempts,
                against.UnblockedAttempts,
                shotsFor.Shots,
                against.Shots,
                shotsFor.Goals,
                against.Goals,
                shotsFor.ExpectedGoals,
                against.ExpectedGoals);
        }
    }

    private sealed class ShotTally
    {
        public int Attempts { get; private set; }

        public int UnblockedAttempts { get; private set; }

        public int Shots { get; private set; }

        public int Goals { get; private set; }

        public double ExpectedGoals { get; private set; }

        public void Add(ShotCount count)
        {
            Attempts++;
            UnblockedAttempts += count.IsBlocked ? 0 : 1;
            Shots += count.IsOnGoal ? 1 : 0;
            Goals += count.IsGoal ? 1 : 0;
            ExpectedGoals += count.ExpectedGoals;
        }
    }
}