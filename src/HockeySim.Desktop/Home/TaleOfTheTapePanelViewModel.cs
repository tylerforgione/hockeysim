using System.Globalization;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Playoffs;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.Standings;
using HockeySim.Desktop.TeamStatistics;
using HockeySim.Desktop.Theme;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Home;

/// <summary>
/// The managed team set against its next opponent: records, standings, a comparison of the
/// teams' figures with the better side of each marked, and each side's top scorer and starting
/// goalie. The figures are the regular season's, or the playoffs' while the managed team is in
/// them. The managed team is always on the left.
/// </summary>
public sealed partial class TaleOfTheTapePanelViewModel : ObservableObject
{
    private readonly GameSession _session;
    private readonly Action<ShellPage> _navigate;
    private readonly Action<PlayerId> _openPlayer;
    private readonly Action<TeamId> _openTeam;

    private TeamId? _opponentId;
    private ShellPage _contextPage = ShellPage.TeamSchedule;

    [ObservableProperty]
    private bool _hasMatch;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    [ObservableProperty]
    private string _caption = string.Empty;

    [ObservableProperty]
    private TapeTeamViewModel? _ours;

    [ObservableProperty]
    private TapeTeamViewModel? _theirs;

    [ObservableProperty]
    private IReadOnlyList<TapeRowViewModel> _rows = [];

    [ObservableProperty]
    private IReadOnlyList<TapePlayerViewModel> _ourPlayers = [];

    [ObservableProperty]
    private IReadOnlyList<TapePlayerViewModel> _theirPlayers = [];

    [ObservableProperty]
    private string _opponentLinkText = string.Empty;

    /// <summary>The season series or the playoff series status, whichever the figures are from.</summary>
    [ObservableProperty]
    private string _contextLinkText = string.Empty;

    public TaleOfTheTapePanelViewModel(
        GameSession session,
        Action<ShellPage> navigate,
        Action<PlayerId> openPlayer,
        Action<TeamId> openTeam)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(navigate);
        ArgumentNullException.ThrowIfNull(openPlayer);
        ArgumentNullException.ThrowIfNull(openTeam);

        _session = session;
        _navigate = navigate;
        _openPlayer = openPlayer;
        _openTeam = openTeam;
        Refresh();
    }

    public void Refresh()
    {
        var snapshot = _session.Snapshot;
        var season = snapshot.Season;
        var team = _session.ManagedTeam;
        var next = MatchDisplay.NextMatch(snapshot, team.Id);
        if (next is null)
        {
            HasMatch = false;
            EmptyText = season.IsComplete ? "Season complete" : "No match scheduled";
            Caption = string.Empty;
            _opponentId = null;
            return;
        }

        var opponent = _session.GetTeam(next.HomeTeamId == team.Id ? next.AwayTeamId : next.HomeTeamId);
        var statistics = HomeStatistics.For(snapshot, team.Id);
        var series = statistics == StatisticsSet.Playoffs
            ? PlayoffDisplay.FindGame(season.Playoffs, next.Date, team.Id)?.Series
            : null;

        Caption = CreateCaption(snapshot, next, team);
        Ours = CreateTeam(team, series, statistics);
        Theirs = CreateTeam(opponent, series, statistics);
        Rows = CreateRows(team, opponent, statistics);
        OurPlayers = CreatePlayers(team, statistics);
        TheirPlayers = CreatePlayers(opponent, statistics);

        _opponentId = opponent.Id;
        OpponentLinkText = $"{opponent.Name} ›";
        if (series is null)
        {
            _contextPage = ShellPage.TeamSchedule;
            ContextLinkText = $"Season series {SeasonSeries(team.Id, opponent.Id)} ›";
        }
        else
        {
            _contextPage = ShellPage.PlayoffPicture;
            ContextLinkText = $"{PlayoffDisplay.SeriesStatus(series, Name)} ›";
        }

        HasMatch = true;
    }

    [RelayCommand]
    private void OpenLines()
    {
        _navigate(ShellPage.Lines);
    }

    [RelayCommand]
    private void OpenOpponent()
    {
        if (_opponentId is { } opponentId)
        {
            _openTeam(opponentId);
        }
    }

    [RelayCommand]
    private void OpenContext()
    {
        _navigate(_contextPage);
    }

    private string Name(TeamId teamId) => _session.GetTeam(teamId).Name;

    private string CreateCaption(GameSnapshot snapshot, ScheduledMatchSnapshot next, TeamSnapshot team)
    {
        var season = snapshot.Season;
        var when = next.Date == season.CurrentDate ? "Today" : MatchDisplay.ShortDate(next.Date);
        var venue = next.HomeTeamId == team.Id ? "Home" : "Away";
        return snapshot.Schedule.PreseasonMatches.Contains(next) ? $"{when} · {venue} · Preseason"
            : PlayoffDisplay.FindGame(season.Playoffs, next.Date, team.Id) is { } game
                ? $"{when} · {venue} · {PlayoffDisplay.RoundName(game.Series.Round)}, game {game.GameNumber} · {PlayoffDisplay.SeriesStatus(game.Series, Name)}"
            : $"{when} · {venue}";
    }

    private TapeTeamViewModel CreateTeam(TeamSnapshot team, PlayoffSeriesSnapshot? series, StatisticsSet statistics)
    {
        var season = _session.Snapshot.Season;
        var record = HomeStatistics.Records(season, statistics).SingleOrDefault(entry => entry.TeamId == team.Id);
        string recordText;
        string standing;
        if (statistics == StatisticsSet.Playoffs)
        {
            recordText = string.Create(CultureInfo.CurrentCulture, $"{record?.Wins ?? 0}-{record?.Losses ?? 0}");
            var seed = series is null ? null : series.HigherRanked.TeamId == team.Id ? series.HigherRanked : series.LowerRanked;
            standing = seed is null ? MatchDisplay.Undefined : PlayoffDisplay.SeedLabel(seed);
        }
        else
        {
            recordText = record is null ? "0-0-0" : MatchDisplay.Record(record);
            standing = record is { GamesPlayed: > 0 } ? DivisionStanding(team) : MatchDisplay.Undefined;
        }

        return new TapeTeamViewModel(team.Id, team.Name, PlayerDisplay.TeamInitials(team.Name), team.Colours.Primary, recordText, standing);
    }

    /// <summary>The team's rank in its division and the division's name, such as "2nd · Northern".</summary>
    private string DivisionStanding(TeamSnapshot team)
    {
        var (conference, division) = _session.FindDivision(team);
        var entry = _session.Snapshot.Season.Standings.Conferences
            .Single(standings => standings.Name == conference.Name).Divisions
            .Single(standings => standings.Name == division.Name).Teams
            .Single(standings => standings.Record.TeamId == team.Id);
        return $"{MatchDisplay.Ordinal(entry.Rank)} · {division.Name}";
    }

    private List<TapeRowViewModel> CreateRows(TeamSnapshot team, TeamSnapshot opponent, StatisticsSet statistics)
    {
        var season = _session.Snapshot.Season;
        var isPlayoffs = statistics == StatisticsSet.Playoffs;
        var (ourColour, theirColour) = TeamPalette.ChartColours(team.Colours, opponent.Colours);
        var ours = Figures(team.Id);
        var theirs = Figures(opponent.Id);
        var rows = new List<TapeRowViewModel>();
        if (!isPlayoffs)
        {
            rows.Add(Compare("Points %", ours.Record?.PointsPercentage, theirs.Record?.PointsPercentage, Fraction));
        }

        rows.Add(Compare("Goals for / game", ours.GoalsForPerGame, theirs.GoalsForPerGame, PerGame));
        rows.Add(Compare("Goals against / game", ours.GoalsAgainstPerGame, theirs.GoalsAgainstPerGame, PerGame, lowerIsBetter: true));
        rows.Add(Compare("Power play", ours.Team.PowerPlayPercentage, theirs.Team.PowerPlayPercentage, Percent));
        rows.Add(Compare("Penalty kill", ours.Team.PenaltyKillPercentage, theirs.Team.PenaltyKillPercentage, Percent));
        rows.Add(Compare("5v5 xGF%", ours.Team.ShotTotals.FiveOnFive.ExpectedGoalsPercentage, theirs.Team.ShotTotals.FiveOnFive.ExpectedGoalsPercentage, Percent));
        rows.Add(Compare("5v5 CF%", ours.Team.ShotTotals.FiveOnFive.CorsiPercentage, theirs.Team.ShotTotals.FiveOnFive.CorsiPercentage, Percent));
        rows.Add(Compare("Team save %", ours.SavePercentage, theirs.SavePercentage, Fraction));
        if (!isPlayoffs)
        {
            var results = TeamResults.ByTeam(_session.Snapshot.League.Teams.Select(entry => entry.Id), season.Results);
            var ourForm = TeamForm.LastTenPoints(results[team.Id], team.Id);
            var theirForm = TeamForm.LastTenPoints(results[opponent.Id], opponent.Id);
            rows.Add(Compare(
                "Last 10",
                ourForm,
                theirForm,
                _ => string.Empty,
                ourText: TeamForm.LastTen(results[team.Id], team.Id),
                theirText: TeamForm.LastTen(results[opponent.Id], opponent.Id)));
        }

        return rows;

        TeamFigures Figures(TeamId teamId)
        {
            var record = HomeStatistics.Records(season, statistics).SingleOrDefault(entry => entry.TeamId == teamId);
            var teamStatistics = HomeStatistics.TeamStatistics(season, statistics).SingleOrDefault(entry => entry.TeamId == teamId)
                ?? TeamStatisticsDisplay.None(teamId);
            var goalies = HomeStatistics.Goalies(season, statistics, teamId).ToList();
            var shotsAgainst = goalies.Sum(goalie => goalie.ShotsAgainst);
            return new TeamFigures(
                record,
                teamStatistics,
                record is { GamesPlayed: > 0 } ? record.GoalsFor / (double)record.GamesPlayed : null,
                record is { GamesPlayed: > 0 } ? record.GoalsAgainst / (double)record.GamesPlayed : null,
                shotsAgainst == 0 ? null : goalies.Sum(goalie => goalie.Saves) / (double)shotsAgainst);
        }

        TapeRowViewModel Compare(
            string label,
            double? ourValue,
            double? theirValue,
            Func<double, string> format,
            bool lowerIsBetter = false,
            string? ourText = null,
            string? theirText = null)
        {
            var share = 0.5;
            var (oursBetter, theirsBetter) = (false, false);
            if (ourValue is { } mine && theirValue is { } theirsValue && mine != theirsValue)
            {
                oursBetter = lowerIsBetter ? mine < theirsValue : mine > theirsValue;
                theirsBetter = !oursBetter;
                share = (lowerIsBetter ? theirsValue : mine) / (mine + theirsValue);
            }

            return new TapeRowViewModel(
                label,
                ourText ?? (ourValue is { } ourFigure ? format(ourFigure) : MatchDisplay.Undefined),
                theirText ?? (theirValue is { } theirFigure ? format(theirFigure) : MatchDisplay.Undefined),
                share,
                oursBetter,
                theirsBetter,
                ourColour,
                theirColour);
        }

        static string Fraction(double value) => value.ToString(".000", CultureInfo.CurrentCulture);

        static string PerGame(double value) => value.ToString("0.00", CultureInfo.CurrentCulture);

        static string Percent(double value) => MatchDisplay.Percentage(value);
    }

    /// <summary>
    /// The team's top scorer in the statistics set (points, then goals, then last name) and its
    /// starting goalie today.
    /// </summary>
    private List<TapePlayerViewModel> CreatePlayers(TeamSnapshot team, StatisticsSet statistics)
    {
        var season = _session.Snapshot.Season;
        var roster = team.Roster.ToDictionary(player => player.Id);

        var scorer = HomeStatistics.Skaters(season, statistics, team.Id)
            .Where(skater => skater.Points > 0)
            .OrderByDescending(skater => skater.Points)
            .ThenByDescending(skater => skater.Goals)
            .ThenBy(skater => roster[skater.PlayerId].LastName, StringComparer.CurrentCulture)
            .FirstOrDefault();
        var scorerRow = scorer is null
            ? new TapePlayerViewModel("Top scorer", MatchDisplay.Undefined, string.Empty, null)
            : new TapePlayerViewModel(
                "Top scorer",
                PlayerDisplay.ShortName(roster[scorer.PlayerId]),
                string.Create(CultureInfo.CurrentCulture, $"{scorer.Goals} G · {scorer.Assists} A · {scorer.Points} P"),
                OpenPlayerCommand(scorer.PlayerId));

        var goalieId = team.Lineup.StartingGoalieId;
        var goalie = HomeStatistics.Goalies(season, statistics, team.Id).SingleOrDefault(entry => entry.PlayerId == goalieId);
        var goalieRow = new TapePlayerViewModel(
            "Goalie",
            PlayerDisplay.ShortName(roster[goalieId]),
            goalie is null
                ? $"{MatchDisplay.Undefined} · {MatchDisplay.Undefined}"
                : $"{MatchDisplay.SavePercentage(goalie.SavePercentage)} · {(goalie.GoalsAgainstAverage is { } average ? $"{MatchDisplay.GoalsAgainstAverage(average)} GAA" : MatchDisplay.Undefined)}",
            OpenPlayerCommand(goalieId));

        return [scorerRow, goalieRow];
    }

    private RelayCommand OpenPlayerCommand(PlayerId playerId) => new(() => _openPlayer(playerId));

    /// <summary>The managed team's record in its completed regular-season meetings with the opponent.</summary>
    private string SeasonSeries(TeamId teamId, TeamId opponentId)
    {
        var meetings = _session.Snapshot.Season.Results
            .Where(result => TeamResults.Involves(result, teamId) && TeamResults.Involves(result, opponentId))
            .ToList();
        var wins = meetings.Count(result => result.WinnerId == teamId);
        var regulationLosses = meetings.Count(result => result.WinnerId != teamId && result.Decision == MatchDecision.Regulation);
        return string.Create(CultureInfo.CurrentCulture, $"{wins}-{regulationLosses}-{meetings.Count - wins - regulationLosses}");
    }

    private sealed record TeamFigures(
        TeamRecordSnapshot? Record,
        TeamSeasonStatisticsSnapshot Team,
        double? GoalsForPerGame,
        double? GoalsAgainstPerGame,
        double? SavePercentage);
}

/// <param name="Standing">Division rank and name, or the playoff seed.</param>
public sealed record TapeTeamViewModel(TeamId TeamId, string Name, string Initials, Colour Colour, string Record, string Standing);

/// <summary>
/// One figure compared between the teams.
/// </summary>
/// <param name="OursShare">
/// The managed team's share of the bar, from zero to one: half when the figures are equal or either
/// is undefined; for a figure where less is better, the opponent's figure over the two.
/// </param>
/// <param name="OursBetter">Whether the managed team's figure is the better, so it is emphasised.</param>
/// <param name="OursColour">The managed team's colour on the bar, seen against the other's.</param>
public sealed record TapeRowViewModel(
    string Label,
    string OursValue,
    string TheirsValue,
    double OursShare,
    bool OursBetter,
    bool TheirsBetter,
    Colour OursColour,
    Colour TheirsColour);

/// <param name="Label">What the player is, such as "Top scorer".</param>
/// <param name="Line">The player's figures; empty when there is no player to show.</param>
/// <param name="OpenCommand">Opens the player; absent when there is none.</param>
public sealed record TapePlayerViewModel(string Label, string Name, string Line, ICommand? OpenCommand);