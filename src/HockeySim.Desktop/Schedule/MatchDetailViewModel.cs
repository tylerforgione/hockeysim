using HockeySim.Desktop.Players;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Schedule;

/// <summary>
/// One completed match: the final score, how it was decided, the scoring and penalty summaries,
/// and each side's box score. These are single-match figures only; season totals are presented
/// separately.
/// </summary>
public sealed class MatchDetailViewModel
{
    public MatchDetailViewModel(
        CompletedMatchSnapshot result,
        IReadOnlyDictionary<PlayerId, PlayerSnapshot> players,
        Func<TeamId, string> teamName)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(teamName);

        Date = result.Date;
        HomeTeamId = result.Home.TeamId;
        DateLabel = MatchDisplay.LongDate(result.Date);
        FinalLabel = MatchDisplay.FinalLabel(result.Decision);
        IsShootout = result.Decision == MatchDecision.Shootout;
        Away = new MatchSideViewModel(result.Away, players, teamName(result.Away.TeamId), result.WinnerId == result.Away.TeamId);
        Home = new MatchSideViewModel(result.Home, players, teamName(result.Home.TeamId), result.WinnerId == result.Home.TeamId);

        var initials = new Dictionary<TeamId, string>
        {
            [result.Away.TeamId] = Away.TeamInitials,
            [result.Home.TeamId] = Home.TeamInitials,
        };
        ScoringSummary = ScoringRows(result, players, initials);
        PenaltySummary = result.Penalties
            .Select(penalty => new PenaltySummaryRowViewModel(
                MatchDisplay.PeriodTime(penalty.Period, penalty.TimeInPeriod),
                initials[penalty.TeamId],
                PlayerDisplay.FullName(players[penalty.PlayerId]),
                MatchDisplay.InfractionName(penalty.Infraction),
                MatchDisplay.PenaltyDescription(penalty.Kind, penalty.Minutes)))
            .ToList();
    }

    public DateOnly Date { get; }

    public TeamId HomeTeamId { get; }

    public string DateLabel { get; }

    public string FinalLabel { get; }

    public bool IsShootout { get; }

    /// <summary>
    /// Explains why a shootout score is one more than the goals in the box score.
    /// </summary>
    public string ShootoutNote => "The shootout winner is credited one goal that no player scored; shootout attempts are not in the box score.";

    public MatchSideViewModel Away { get; }

    public MatchSideViewModel Home { get; }

    /// <summary>Every goal scored by a player, in order, with the running score.</summary>
    public IReadOnlyList<GoalSummaryRowViewModel> ScoringSummary { get; }

    public bool HasNoGoals => ScoringSummary.Count == 0;

    /// <summary>Every penalty assessed, in order.</summary>
    public IReadOnlyList<PenaltySummaryRowViewModel> PenaltySummary { get; }

    public bool HasNoPenalties => PenaltySummary.Count == 0;

    private static List<GoalSummaryRowViewModel> ScoringRows(
        CompletedMatchSnapshot result,
        IReadOnlyDictionary<PlayerId, PlayerSnapshot> players,
        Dictionary<TeamId, string> initials)
    {
        var (awayGoals, homeGoals) = (0, 0);
        var rows = new List<GoalSummaryRowViewModel>();
        foreach (var goal in result.Goals)
        {
            if (goal.TeamId == result.Home.TeamId)
            {
                homeGoals++;
            }
            else
            {
                awayGoals++;
            }

            var assists = new[] { goal.PrimaryAssistId, goal.SecondaryAssistId }
                .OfType<PlayerId>()
                .Select(id => PlayerDisplay.FullName(players[id]))
                .ToList();
            rows.Add(new GoalSummaryRowViewModel(
                MatchDisplay.PeriodTime(goal.Period, goal.TimeInPeriod),
                initials[goal.TeamId],
                PlayerDisplay.FullName(players[goal.ScorerId]),
                assists.Count == 0 ? "Unassisted" : string.Join(", ", assists),
                MatchDisplay.GoalSituationLabel(goal.Situation, goal.IsEmptyNet),
                $"{awayGoals}–{homeGoals}"));
        }

        return rows;
    }
}

public sealed class MatchSideViewModel
{
    public MatchSideViewModel(
        CompletedMatchTeamSnapshot side,
        IReadOnlyDictionary<PlayerId, PlayerSnapshot> players,
        string teamName,
        bool isWinner)
    {
        TeamName = teamName;
        Score = side.Score;
        Shots = side.Shots;
        PowerPlay = MatchDisplay.PowerPlay(side.PowerPlayGoals, side.PowerPlayOpportunities);
        PenaltyMinutes = side.PenaltyMinutes;
        IsWinner = isWinner;
        Skaters = side.Skaters
            .Select(line =>
            {
                var player = players[line.PlayerId];
                return new SkaterBoxScoreRowViewModel(
                    PlayerDisplay.FullName(player),
                    PlayerDisplay.PositionAbbreviation(player.Position),
                    line.Goals,
                    line.Assists,
                    line.Points,
                    MatchDisplay.PlusMinus(line.PlusMinus),
                    MatchDisplay.TimeOnIce(line.TimeOnIce),
                    line.Shots,
                    line.ShotAttempts,
                    MatchDisplay.ExpectedGoals(line.ExpectedGoals),
                    line.Hits,
                    line.BlockedShots,
                    MatchDisplay.Faceoffs(line.FaceoffsWon, line.FaceoffsLost),
                    line.Takeaways,
                    line.Giveaways,
                    line.PenaltyMinutes,
                    line.PowerPlayGoals,
                    line.ShorthandedGoals,
                    MatchDisplay.Percentage(line.OnIce.FiveOnFive.CorsiPercentage),
                    MatchDisplay.Percentage(line.OnIce.FiveOnFive.ExpectedGoalsPercentage));
            })
            .ToList();

        var goalie = players[side.Goalie.PlayerId];
        Goalie = new GoalieBoxScoreRowViewModel(
            PlayerDisplay.FullName(goalie),
            side.Goalie.ShotsAgainst,
            side.Goalie.Saves,
            side.Goalie.GoalsAgainst,
            MatchDisplay.SavePercentage(side.Goalie.Saves, side.Goalie.ShotsAgainst),
            MatchDisplay.ExpectedGoals(side.Goalie.ExpectedGoalsAgainst),
            MatchDisplay.GoalsSavedAboveExpected(side.Goalie.ExpectedGoalsAgainst - side.Goalie.GoalsAgainst),
            MatchDisplay.TimeOnIce(side.Goalie.TimeOnIce));
    }

    public string TeamName { get; }

    public string TeamInitials => PlayerDisplay.TeamInitials(TeamName);

    public int Score { get; }

    public int Shots { get; }

    /// <summary>Power-play goals of opportunities, such as "1/3".</summary>
    public string PowerPlay { get; }

    public int PenaltyMinutes { get; }

    public bool IsWinner { get; }

    /// <summary>Every skater who appeared, in box-score order.</summary>
    public IReadOnlyList<SkaterBoxScoreRowViewModel> Skaters { get; }

    /// <summary>The starting goalie, who played the whole match.</summary>
    public GoalieBoxScoreRowViewModel Goalie { get; }
}

/// <param name="Shots">Shots on goal.</param>
/// <param name="ShotAttempts">Shots on goal, missed, and blocked.</param>
/// <param name="BlockedShots">Opponent attempts the skater blocked.</param>
/// <param name="Faceoffs">Faceoffs won and lost, or a dash when none were taken.</param>
/// <param name="PowerPlayGoals">Goals on the power play, counted among the goals.</param>
/// <param name="ShorthandedGoals">Goals while shorthanded, counted among the goals.</param>
/// <param name="CorsiPercentage">The 5-on-5 share of shot attempts while on the ice, or a dash.</param>
/// <param name="ExpectedGoalsPercentage">The 5-on-5 share of expected goals while on the ice, or a dash.</param>
public sealed record SkaterBoxScoreRowViewModel(
    string Name,
    string Position,
    int Goals,
    int Assists,
    int Points,
    string PlusMinus,
    string TimeOnIce,
    int Shots,
    int ShotAttempts,
    string ExpectedGoals,
    int Hits,
    int BlockedShots,
    string Faceoffs,
    int Takeaways,
    int Giveaways,
    int PenaltyMinutes,
    int PowerPlayGoals,
    int ShorthandedGoals,
    string CorsiPercentage,
    string ExpectedGoalsPercentage);

public sealed record GoalieBoxScoreRowViewModel(
    string Name,
    int ShotsAgainst,
    int Saves,
    int GoalsAgainst,
    string SavePercentage,
    string ExpectedGoalsAgainst,
    string GoalsSavedAboveExpected,
    string TimeOnIce);

/// <param name="Time">The period and clock, such as "2nd 14:05".</param>
/// <param name="Team">The scoring team's initials.</param>
/// <param name="Assists">The assisting players, or "Unassisted".</param>
/// <param name="Situation">PP, SH, PS, and EN markers, or empty at even strength.</param>
/// <param name="Score">The score after the goal, away first as on the scoreboard.</param>
public sealed record GoalSummaryRowViewModel(
    string Time,
    string Team,
    string Scorer,
    string Assists,
    string Situation,
    string Score);

/// <param name="Time">The period and clock when play stopped for the penalty.</param>
/// <param name="Team">The penalized team's initials.</param>
/// <param name="Penalty">The minutes and kind, such as "2 min" or "Penalty shot".</param>
public sealed record PenaltySummaryRowViewModel(
    string Time,
    string Team,
    string Player,
    string Infraction,
    string Penalty);