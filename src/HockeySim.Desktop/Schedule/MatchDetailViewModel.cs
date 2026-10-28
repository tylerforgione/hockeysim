using System.Globalization;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Tables;
using HockeySim.Desktop.Theme;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Schedule;

/// <summary>
/// One completed match: a banner with the final score, then a summary (the game by period, the
/// scoring and penalty summaries, the teams compared, the three stars, and the goalies) or the full
/// player tables. These are single-match figures only; season totals are presented separately. The
/// shot chart and play-by-play wait for the play-by-play to be kept.
/// </summary>
public sealed partial class MatchDetailViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSummaryTab), nameof(IsPlayersTab))]
    private MatchDetailTab _tab = MatchDetailTab.Summary;

    /// <summary>The sort of both teams' skater tables on the Players tab, or none for box-score order.</summary>
    [ObservableProperty]
    private TableSort? _skaterSort;

    /// <param name="skaterSort">The sort to open the Players tab with, to carry one box score's choice to the next.</param>
    /// <param name="openPlayer">Opens a player's profile; player names are plain text without it.</param>
    /// <param name="openTeam">Opens a team's page; team names are plain text without it.</param>
    public MatchDetailViewModel(
        CompletedMatchSnapshot result,
        IReadOnlyDictionary<PlayerId, PlayerSnapshot> players,
        Func<TeamId, TeamSnapshot> team,
        TableSort? skaterSort = null,
        Action<PlayerId>? openPlayer = null,
        Action<TeamId>? openTeam = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(team);

        var homeTeam = team(result.Home.TeamId);
        var awayTeam = team(result.Away.TeamId);
        Date = result.Date;
        HomeTeamId = result.Home.TeamId;
        DateLabel = MatchDisplay.LongDate(result.Date);
        Arena = homeTeam.Arena;
        FinalLabel = MatchDisplay.FinalLabel(result.Decision);
        var (awayChartColour, homeChartColour) = TeamPalette.ChartColours(awayTeam.Colours, homeTeam.Colours);
        Away = new MatchSideViewModel(result.Away, players, awayTeam, awayChartColour, result.Phase, result.WinnerId == result.Away.TeamId, skaterSort, openPlayer, openTeam);
        Home = new MatchSideViewModel(result.Home, players, homeTeam, homeChartColour, result.Phase, result.WinnerId == result.Home.TeamId, skaterSort, openPlayer, openTeam);
        _skaterSort = skaterSort;
        Away.SortRequested += SortSkaters;
        Home.SortRequested += SortSkaters;

        var initials = new Dictionary<TeamId, string>
        {
            [result.Away.TeamId] = Away.TeamInitials,
            [result.Home.TeamId] = Home.TeamInitials,
        };
        PeriodTables = MatchSummaryTables.ByPeriod(result, Away.TeamInitials, Home.TeamInitials);
        ScoringSummary = ScoringRows(result, players, initials, openPlayer);
        PenaltySummary = result.Penalties
            .Select(penalty => new PenaltySummaryRowViewModel(
                MatchDisplay.PeriodTime(penalty.Period, penalty.TimeInPeriod),
                initials[penalty.TeamId],
                PlayerDisplay.FullName(players[penalty.PlayerId]),
                MatchDisplay.InfractionName(penalty.Infraction),
                MatchDisplay.PenaltyDescription(penalty.Kind, penalty.Minutes))
            {
                OpenPlayerCommand = NameLinks.Open(openPlayer, penalty.PlayerId),
            })
            .ToList();
        Comparison = MatchSummaryTables.Comparison(result);
        ThreeStars = result.ThreeStars
            .Select((star, index) => MatchSummaryTables.Star(index + 1, star, result, players[star.PlayerId], initials[star.TeamId], openPlayer))
            .ToList();
        Goalies =
        [
            new GoalieSummaryRowViewModel(Away.TeamInitials, Away.Goalie),
            new GoalieSummaryRowViewModel(Home.TeamInitials, Home.Goalie),
        ];
    }

    public DateOnly Date { get; }

    public TeamId HomeTeamId { get; }

    public string DateLabel { get; }

    /// <summary>The home team's arena, where the match was played.</summary>
    public string Arena { get; }

    public string FinalLabel { get; }

    public MatchSideViewModel Away { get; }

    public MatchSideViewModel Home { get; }

    public bool IsSummaryTab => Tab == MatchDetailTab.Summary;

    public bool IsPlayersTab => Tab == MatchDetailTab.Players;

    /// <summary>Goals, shots, expected goals, and the power play by period, each a small table.</summary>
    public IReadOnlyList<PeriodTableViewModel> PeriodTables { get; }

    /// <summary>Every goal scored by a player, in order, with the running score.</summary>
    public IReadOnlyList<GoalSummaryRowViewModel> ScoringSummary { get; }

    public bool HasNoGoals => ScoringSummary.Count == 0;

    /// <summary>Every penalty assessed, in order.</summary>
    public IReadOnlyList<PenaltySummaryRowViewModel> PenaltySummary { get; }

    public bool HasNoPenalties => PenaltySummary.Count == 0;

    /// <summary>The two teams side by side, away on the left, each figure with its share.</summary>
    public IReadOnlyList<ComparisonRowViewModel> Comparison { get; }

    /// <summary>The match's three stars, first star first.</summary>
    public IReadOnlyList<StarRowViewModel> ThreeStars { get; }

    /// <summary>Both starting goalies, away first.</summary>
    public IReadOnlyList<GoalieSummaryRowViewModel> Goalies { get; }

    [RelayCommand]
    private void SelectTab(MatchDetailTab tab) => Tab = tab;

    /// <summary>One sort applies to both teams' tables, whichever header was clicked.</summary>
    private void SortSkaters(object? sender, TableSort sort)
    {
        var other = sender == Away ? Home : Away;
        other.ApplySort(sort);
        SkaterSort = sort;
    }

    private static List<GoalSummaryRowViewModel> ScoringRows(
        CompletedMatchSnapshot result,
        IReadOnlyDictionary<PlayerId, PlayerSnapshot> players,
        Dictionary<TeamId, string> initials,
        Action<PlayerId>? openPlayer)
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

            var assistIds = new[] { goal.PrimaryAssistId, goal.SecondaryAssistId }.OfType<PlayerId>().ToList();
            var assists = assistIds.Select(id => PlayerDisplay.FullName(players[id])).ToList();
            var assistLinks = assistIds
                .Select((id, index) => new PlayerLinkViewModel(
                    assists[index],
                    NameLinks.Open(openPlayer, id),
                    index < assistIds.Count - 1 ? ", " : string.Empty))
                .ToList();
            rows.Add(new GoalSummaryRowViewModel(
                MatchDisplay.PeriodTime(goal.Period, goal.TimeInPeriod),
                initials[goal.TeamId],
                PlayerDisplay.FullName(players[goal.ScorerId]),
                assists.Count == 0 ? "Unassisted" : string.Join(", ", assists),
                MatchDisplay.GoalSituationLabel(goal.Situation, goal.IsEmptyNet),
                $"{awayGoals}–{homeGoals}")
            {
                OpenScorerCommand = NameLinks.Open(openPlayer, goal.ScorerId),
                AssistLinks = assistLinks,
            });
        }

        return rows;
    }
}

public enum MatchDetailTab
{
    Summary,
    Players,
}

public sealed partial class MatchSideViewModel : ObservableObject
{
    private readonly List<SkaterBoxScoreRowViewModel> _boxScoreOrder;

    public MatchSideViewModel(
        CompletedMatchTeamSnapshot side,
        IReadOnlyDictionary<PlayerId, PlayerSnapshot> players,
        TeamSnapshot team,
        Colour chartColour,
        SeasonPhase phase,
        bool isWinner,
        TableSort? skaterSort = null,
        Action<PlayerId>? openPlayer = null,
        Action<TeamId>? openTeam = null)
    {
        ArgumentNullException.ThrowIfNull(side);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(team);

        TeamId = side.TeamId;
        OpenTeamCommand = NameLinks.Open(openTeam, side.TeamId);
        TeamName = team.Name;
        Colours = team.Colours;
        ChartColour = chartColour;
        Score = side.Score;
        Record = RecordLabel(side.RecordAfter, phase);
        IsWinner = isWinner;
        _boxScoreOrder = side.Skaters
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
                    MatchDisplay.Percentage(line.OnIce.FiveOnFive.ExpectedGoalsPercentage),
                    new SkaterBoxScoreValues(
                        $"{player.LastName}\0{player.FirstName}",
                        player.Position,
                        line.PlusMinus,
                        line.TimeOnIce,
                        line.ExpectedGoals,
                        line.FaceoffsWon + line.FaceoffsLost == 0 ? null : line.FaceoffsWon * 100_000L - line.FaceoffsLost,
                        line.OnIce.FiveOnFive.CorsiPercentage,
                        line.OnIce.FiveOnFive.ExpectedGoalsPercentage))
                {
                    OpenPlayerCommand = NameLinks.Open(openPlayer, line.PlayerId),
                };
            })
            .ToList();
        _sort = skaterSort;
        _skaters = TableSorting.Apply(_boxScoreOrder, BoxScoreTableColumns.Skaters, skaterSort);

        var goalie = players[side.Goalie.PlayerId];
        Goalie = new GoalieBoxScoreRowViewModel(
            PlayerDisplay.FullName(goalie),
            side.Goalie.ShotsAgainst,
            side.Goalie.Saves,
            side.Goalie.GoalsAgainst,
            MatchDisplay.SavePercentage(side.Goalie.Saves, side.Goalie.ShotsAgainst),
            MatchDisplay.ExpectedGoals(side.Goalie.ExpectedGoalsAgainst),
            MatchDisplay.GoalsSavedAboveExpected(side.Goalie.ExpectedGoalsAgainst - side.Goalie.GoalsAgainst),
            MatchDisplay.TimeOnIce(side.Goalie.TimeOnIce))
        {
            OpenPlayerCommand = NameLinks.Open(openPlayer, side.Goalie.PlayerId),
        };
    }

    public TeamId TeamId { get; }

    /// <summary>Opens the team's page; absent where the name links nowhere.</summary>
    public ICommand? OpenTeamCommand { get; }

    public string TeamName { get; }

    public string TeamInitials => PlayerDisplay.TeamInitials(TeamName);

    /// <summary>The team's colours, which paint its half of the banner and its comparison bars.</summary>
    public TeamColours Colours { get; }

    /// <summary>
    /// The team's colour in the comparison bars: one that shows on the dark panels and differs
    /// from the other team's (see <see cref="TeamPalette.ChartColours"/>).
    /// </summary>
    public Colour ChartColour { get; }

    public int Score { get; }

    /// <summary>
    /// The team's record once the match was played: wins, losses, and overtime losses in the
    /// preseason or regular season ("12-5-2"), or its series record in the playoffs.
    /// </summary>
    public string Record { get; }

    public bool IsWinner { get; }

    /// <summary>Every skater who appeared, in box-score order until a sort is chosen.</summary>
    [ObservableProperty]
    private IReadOnlyList<SkaterBoxScoreRowViewModel> _skaters;

    /// <summary>The skater table's sort, or none for box-score order.</summary>
    [ObservableProperty]
    private TableSort? _sort;

    /// <summary>Raised when a header is clicked, with the sort chosen; the match applies it to both teams.</summary>
    public event EventHandler<TableSort>? SortRequested;

    /// <summary>The starting goalie, who played the whole match.</summary>
    public GoalieBoxScoreRowViewModel Goalie { get; }

    /// <summary>Sorts the skaters by a column, or reverses the sort when it is already sorted by it.</summary>
    [RelayCommand]
    private void SortSkaters(string column)
    {
        var sort = TableSorting.Click(BoxScoreTableColumns.Skaters, Sort, column);
        ApplySort(sort);
        SortRequested?.Invoke(this, sort);
    }

    public void ApplySort(TableSort sort)
    {
        Sort = sort;
        Skaters = TableSorting.Apply(_boxScoreOrder, BoxScoreTableColumns.Skaters, sort);
    }

    private static string RecordLabel(MatchRecordSnapshot record, SeasonPhase phase) =>
        phase == SeasonPhase.Playoffs
            ? string.Create(CultureInfo.CurrentCulture, $"Series {record.Wins}–{record.Losses}")
            : string.Create(CultureInfo.CurrentCulture, $"{record.Wins}-{record.Losses}-{record.OvertimeLosses}");
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
    string ExpectedGoalsPercentage,
    SkaterBoxScoreValues Values)
{
    /// <summary>Opens the player's profile; absent where the name links nowhere.</summary>
    public ICommand? OpenPlayerCommand { get; init; }
}

/// <summary>
/// The raw figures behind a box-score row's text, which its table sorts by.
/// </summary>
/// <param name="Name">Last name, then first, ordinally comparable.</param>
/// <param name="Faceoffs">Faceoffs won, then fewer lost, as one number; null when none were taken.</param>
/// <param name="CorsiPercentage">Null while undefined (shown as a dash).</param>
/// <param name="ExpectedGoalsPercentage">Null while undefined (shown as a dash).</param>
public sealed record SkaterBoxScoreValues(
    string Name,
    Position Position,
    int PlusMinus,
    TimeSpan TimeOnIce,
    double ExpectedGoals,
    long? Faceoffs,
    double? CorsiPercentage,
    double? ExpectedGoalsPercentage);

public sealed record GoalieBoxScoreRowViewModel(
    string Name,
    int ShotsAgainst,
    int Saves,
    int GoalsAgainst,
    string SavePercentage,
    string ExpectedGoalsAgainst,
    string GoalsSavedAboveExpected,
    string TimeOnIce)
{
    /// <summary>Opens the goalie's profile; absent where the name links nowhere.</summary>
    public ICommand? OpenPlayerCommand { get; init; }
}

/// <summary>A starting goalie on the summary, with the team's initials.</summary>
public sealed record GoalieSummaryRowViewModel(string Team, GoalieBoxScoreRowViewModel Goalie);

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
    string Score)
{
    /// <summary>Opens the scorer's profile; absent where the name links nowhere.</summary>
    public ICommand? OpenScorerCommand { get; init; }

    /// <summary>The assisting players one by one, so each can open its profile; empty when unassisted.</summary>
    public IReadOnlyList<PlayerLinkViewModel> AssistLinks { get; init; } = [];

    public bool IsUnassisted => AssistLinks.Count == 0;
}


/// <param name="Time">The period and clock when play stopped for the penalty.</param>
/// <param name="Team">The penalized team's initials.</param>
/// <param name="Penalty">The minutes and kind, such as "2 min" or "Penalty shot".</param>
public sealed record PenaltySummaryRowViewModel(
    string Time,
    string Team,
    string Player,
    string Infraction,
    string Penalty)
{
    /// <summary>Opens the player's profile; absent where the name links nowhere.</summary>
    public ICommand? OpenPlayerCommand { get; init; }
}