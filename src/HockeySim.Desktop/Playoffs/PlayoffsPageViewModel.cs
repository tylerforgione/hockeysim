using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Schedule;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Playoffs;

/// <summary>
/// The playoff bracket: every round's series with its score, the champion once crowned, and the
/// selected series' games, each opening its box score. Management forms the series and schedules
/// their games; this page only presents them. A round not yet formed is shown waiting.
/// </summary>
public sealed partial class PlayoffsPageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;
    private readonly Action<DateOnly, TeamId> _openMatch;

    [ObservableProperty]
    private IReadOnlyList<PlayoffRoundViewModel> _rounds = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedSeries))]
    private PlayoffSeriesViewModel? _selectedSeries;

    /// <param name="openMatch">Opens a game's box score, from the schedule of a team that plays in it.</param>
    public PlayoffsPageViewModel(GameSession session, Action<DateOnly, TeamId> openMatch)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(openMatch);

        _session = session;
        _openMatch = openMatch;
        Refresh();
    }

    public override string Subtitle
    {
        get
        {
            var season = $"{PlayerDisplay.FormatSeason(_session.Snapshot.League.SeasonYear)} playoffs";
            return Playoffs switch
            {
                null => $"{season} · begin after the regular season",
                { ChampionId: { } champion } => $"{season} · {_session.GetTeam(champion).Name} are champions",
                var playoffs => $"{season} · {PlayoffDisplay.RoundName(playoffs.CurrentRound).ToLower(CultureInfo.CurrentCulture)} in progress",
            };
        }
    }

    public bool HasPlayoffs => Playoffs is not null;

    /// <summary>Explains the empty bracket before the regular season ends.</summary>
    public string NotStartedMessage
    {
        get
        {
            var finalDay = _session.Snapshot.Schedule.Matches[^1].Date;
            return $"The playoffs begin after the regular season ends on {MatchDisplay.LongDate(finalDay)}. "
                + "The top three of each division and two wild cards from each conference qualify.";
        }
    }

    public bool HasChampion => Playoffs?.ChampionId is not null;

    /// <summary>Announces the champion, or is empty until the final is decided.</summary>
    public string ChampionAnnouncement => Playoffs?.ChampionId is { } champion
        ? $"{_session.GetTeam(champion).Name} · {PlayerDisplay.FormatSeason(_session.Snapshot.League.SeasonYear)} champions"
        : string.Empty;

    public bool IsManagedTeamChampion => Playoffs?.ChampionId == _session.Snapshot.ManagedTeamId;

    public bool HasSelectedSeries => SelectedSeries is not null;

    private PlayoffsSnapshot? Playoffs => _session.Snapshot.Season.Playoffs;

    /// <summary>
    /// Rebuilds the bracket, keeping the selected series. Until one is chosen, the managed team's
    /// latest series is shown, or the first series when the team did not qualify.
    /// </summary>
    public override void Refresh()
    {
        var selected = SelectedSeries is { } current ? (current.Round, current.HigherRanked.TeamId) : default((PlayoffRound, TeamId)?);
        var series = (Playoffs?.Series ?? [])
            .Select(snapshot => new PlayoffSeriesViewModel(snapshot, _session, _openMatch, Select))
            .ToList();

        Rounds = Enum.GetValues<PlayoffRound>()
            .Select(round => new PlayoffRoundViewModel(
                PlayoffDisplay.RoundName(round).ToUpper(CultureInfo.CurrentCulture),
                series.Where(candidate => candidate.Round == round).ToList()))
            .ToList();

        var managedTeamId = _session.Snapshot.ManagedTeamId;
        Select(series.FirstOrDefault(candidate => (candidate.Round, candidate.HigherRanked.TeamId) == selected)
            ?? series.LastOrDefault(candidate => candidate.Involves(managedTeamId))
            ?? series.FirstOrDefault());

        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(HasPlayoffs));
        OnPropertyChanged(nameof(HasChampion));
        OnPropertyChanged(nameof(ChampionAnnouncement));
        OnPropertyChanged(nameof(IsManagedTeamChampion));
    }

    private void Select(PlayoffSeriesViewModel? series)
    {
        foreach (var candidate in Rounds.SelectMany(round => round.Series))
        {
            candidate.IsSelected = candidate == series;
        }

        SelectedSeries = series;
    }
}

/// <param name="Series">The round's series in bracket order; empty until the round starts.</param>
public sealed record PlayoffRoundViewModel(string Title, IReadOnlyList<PlayoffSeriesViewModel> Series)
{
    public bool HasStarted => Series.Count > 0;
}

/// <summary>One series in the bracket, with its games for the detail pane.</summary>
public sealed partial class PlayoffSeriesViewModel : ObservableObject
{
    private readonly Action<PlayoffSeriesViewModel> _select;

    [ObservableProperty]
    private bool _isSelected;

    public PlayoffSeriesViewModel(
        PlayoffSeriesSnapshot series,
        GameSession session,
        Action<DateOnly, TeamId> openMatch,
        Action<PlayoffSeriesViewModel> select)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(openMatch);
        ArgumentNullException.ThrowIfNull(select);

        _select = select;
        var managedTeamId = session.Snapshot.ManagedTeamId;
        string Name(TeamId teamId) => session.GetTeam(teamId).Name;

        Round = series.Round;
        RoundName = PlayoffDisplay.RoundName(series.Round);
        HigherRanked = new PlayoffSeriesTeamViewModel(series.HigherRanked, Name(series.HigherRanked.TeamId), series.HigherRankedWins, series.WinnerId, managedTeamId);
        LowerRanked = new PlayoffSeriesTeamViewModel(series.LowerRanked, Name(series.LowerRanked.TeamId), series.LowerRankedWins, series.WinnerId, managedTeamId);
        Status = PlayoffDisplay.SeriesStatus(series, Name);
        Title = $"{HigherRanked.Name} vs {LowerRanked.Name}";

        // The team listed first in a game's schedule is the one whose schedule opens; the
        // managed team's when it plays, so its own results stay in view.
        var scheduleTeamId = series.LowerRanked.TeamId == managedTeamId ? managedTeamId : series.HigherRanked.TeamId;
        Games = series.Games
            .Select((game, index) => new PlayoffGameRowViewModel(
                $"Game {index + 1}",
                MatchDisplay.ShortDate(game.Date),
                $"{Name(game.Away.TeamId)} @ {Name(game.Home.TeamId)}",
                ScoreLine(game, Name),
                true,
                () => openMatch(game.Date, scheduleTeamId)))
            .Concat(series.NextGame is { } next
                ?
                [
                    new PlayoffGameRowViewModel(
                        $"Game {series.Games.Count + 1}",
                        MatchDisplay.ShortDate(next.Date),
                        $"{Name(next.AwayTeamId)} @ {Name(next.HomeTeamId)}",
                        "Scheduled",
                        false,
                        () => openMatch(next.Date, scheduleTeamId)),
                ]
                : [])
            .ToList();
    }

    public PlayoffRound Round { get; }

    public string RoundName { get; }

    /// <summary>The team with home ice, listed first.</summary>
    public PlayoffSeriesTeamViewModel HigherRanked { get; }

    public PlayoffSeriesTeamViewModel LowerRanked { get; }

    /// <summary>Where the series stands, such as "Ottawa Owls lead 3–1".</summary>
    public string Status { get; }

    public string Title { get; }

    /// <summary>The games played, in order, then the next one scheduled.</summary>
    public IReadOnlyList<PlayoffGameRowViewModel> Games { get; }

    public bool Involves(TeamId teamId) => HigherRanked.TeamId == teamId || LowerRanked.TeamId == teamId;

    [RelayCommand]
    private void Select()
    {
        _select(this);
    }

    /// <summary>The final score, winner first, with the winner named: "Owls 3–2 OT".</summary>
    private static string ScoreLine(CompletedMatchSnapshot game, Func<TeamId, string> name)
    {
        var (winner, loser) = game.WinnerId == game.Home.TeamId ? (game.Home, game.Away) : (game.Away, game.Home);
        var suffix = MatchDisplay.DecisionSuffix(game.Decision);
        var score = string.Create(CultureInfo.CurrentCulture, $"{name(winner.TeamId)} {winner.Score}–{loser.Score}");
        return suffix.Length == 0 ? score : $"{score} {suffix}";
    }
}

/// <summary>One side of a series: how it qualified, its wins, and whether it advanced.</summary>
public sealed class PlayoffSeriesTeamViewModel
{
    public PlayoffSeriesTeamViewModel(PlayoffSeedSnapshot seed, string name, int wins, TeamId? winnerId, TeamId managedTeamId)
    {
        ArgumentNullException.ThrowIfNull(seed);

        TeamId = seed.TeamId;
        Seed = PlayoffDisplay.SeedLabel(seed);
        SeedDescription = PlayoffDisplay.SeedDescription(seed);
        Name = name;
        Wins = wins;
        IsWinner = winnerId == seed.TeamId;
        IsEliminated = winnerId is not null && !IsWinner;
        IsManaged = seed.TeamId == managedTeamId;
    }

    public TeamId TeamId { get; }

    /// <summary>"N1", "WC2", and so on.</summary>
    public string Seed { get; }

    public string SeedDescription { get; }

    public string Name { get; }

    public int Wins { get; }

    public bool IsWinner { get; }

    public bool IsEliminated { get; }

    public bool IsManaged { get; }

    /// <summary>Bold for the team that advanced and for the managed team, so both stand out.</summary>
    public bool IsEmphasized => IsWinner || IsManaged;
}

/// <summary>A game in the selected series; a played game opens its box score.</summary>
public sealed partial class PlayoffGameRowViewModel
{
    private readonly Action _open;

    /// <param name="result">The winner and score, or "Scheduled" before the game is played.</param>
    public PlayoffGameRowViewModel(string game, string date, string matchup, string result, bool isPlayed, Action open)
    {
        Game = game;
        Date = date;
        Matchup = matchup;
        Result = result;
        IsPlayed = isPlayed;
        _open = open;
    }

    public string Game { get; }

    public string Date { get; }

    public string Matchup { get; }

    public string Result { get; }

    public bool IsPlayed { get; }

    /// <summary>Shows the game on the schedule page: its box score, or when it is scheduled.</summary>
    [RelayCommand]
    private void Open()
    {
        _open();
    }
}