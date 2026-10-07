using HockeySim.Desktop.Players;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Schedule;

/// <summary>
/// One completed match: the final score, how it was decided, and each side's box score. These are
/// single-match figures only; season totals are presented separately.
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
                    line.Giveaways);
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
            MatchDisplay.TimeOnIce(side.Goalie.TimeOnIce));
    }

    public string TeamName { get; }

    public string TeamInitials => PlayerDisplay.TeamInitials(TeamName);

    public int Score { get; }

    public int Shots { get; }

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
    int Giveaways);

public sealed record GoalieBoxScoreRowViewModel(
    string Name,
    int ShotsAgainst,
    int Saves,
    int GoalsAgainst,
    string SavePercentage,
    string ExpectedGoalsAgainst,
    string TimeOnIce);