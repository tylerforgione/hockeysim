using System.Globalization;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Schedule;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Players;

/// <summary>
/// One player's profile, a page of the shell outside any section. It brings its own banner and
/// tabs: Overview (biography, season statistics, last five games, ratings, usage), Ratings,
/// Statistics, Game log, and Health, with Career shown disabled. The regular-season or playoff
/// switch drives the banner and every statistic together.
/// </summary>
/// <remarks>
/// A skater's goaltending ratings and a goalie's skater ratings are generated low and play no part
/// in their game, so the profile leaves them out. Durability, wear, and the exact recovery time are
/// hidden and never reach Desktop. Ratings are shown at what the player plays at today: their
/// healthy rating less the points an injury they are playing through takes off.
/// </remarks>
public sealed partial class PlayerProfilePageViewModel : ShellPageViewModel
{
    private static readonly (string Name, Rating[] Ratings)[] SkaterGroups =
    [
        ("OFFENCE", [Rating.Skating, Rating.PuckControl, Rating.Passing, Rating.ShotPower, Rating.ShotAccuracy, Rating.OffensiveAwareness]),
        ("DEFENCE", [Rating.DefensiveAwareness, Rating.Checking, Rating.ShotBlocking, Rating.StickChecking]),
        ("GAME", [Rating.Faceoffs, Rating.Stamina, Rating.Toughness, Rating.Discipline]),
    ];

    private static readonly (string Name, Rating[] Ratings)[] GoalieGroups =
    [
        ("GOALTENDING", [Rating.GoalieReflex, Rating.GoaliePositioning, Rating.GoalieReboundControl]),
        ("GAME", [Rating.Stamina, Rating.Discipline]),
    ];

    private static readonly (SpecialSituation Situation, string Prefix)[] PowerPlayUnits =
    [
        (SpecialSituation.PowerPlay5On4, "PP"),
        (SpecialSituation.PowerPlay5On3, "5v3 U"),
        (SpecialSituation.PowerPlay4On3, "4v3 U"),
    ];

    private static readonly (SpecialSituation Situation, string Prefix)[] PenaltyKillUnits =
    [
        (SpecialSituation.PenaltyKill4On5, "PK"),
        (SpecialSituation.PenaltyKill3On5, "3v5 U"),
        (SpecialSituation.PenaltyKill3On4, "3v4 U"),
    ];

    private const int LastGames = 5;

    private readonly GameSession _session;
    private readonly PlayerId _playerId;
    private readonly Action<DateOnly, TeamId> _openMatch;
    private readonly PlayerBannerViewModel _banner;
    private readonly IReadOnlyList<PlayerProfileTabViewModel> _tabs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsOverview), nameof(ShowsRatings), nameof(ShowsStatistics), nameof(ShowsGameLog), nameof(ShowsHealth))]
    private PlayerProfileTab _tab;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRegularSeason), nameof(ShowsPlayoffs))]
    private StatisticsSet _statistics;

    /// <summary>
    /// The game log row chosen; choosing one opens its box score, after which nothing is chosen
    /// again.
    /// </summary>
    [ObservableProperty]
    private PlayerGameRowViewModel? _selectedGame;

    /// <param name="statistics">
    /// The totals to show; the regular season's until the playoffs start, whatever is asked.
    /// </param>
    /// <param name="openTeam">Opens the player's team from the banner's team link.</param>
    /// <param name="openMatch">Opens a game log row's box score in the given team's schedule.</param>
    public PlayerProfilePageViewModel(
        GameSession session,
        PlayerId playerId,
        PlayerProfileTab tab,
        StatisticsSet statistics,
        Action<TeamId> openTeam,
        Action<DateOnly, TeamId> openMatch)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(openTeam);
        ArgumentNullException.ThrowIfNull(openMatch);

        _session = session;
        _playerId = playerId;
        _openMatch = openMatch;
        _banner = new PlayerBannerViewModel(openTeam);
        _tab = tab;
        _statistics = statistics.OrRegularSeason(HasPlayoffStatistics);
        _tabs =
        [
            TabFor("Overview", PlayerProfileTab.Overview),
            TabFor("Ratings", PlayerProfileTab.Ratings),
            TabFor("Statistics", PlayerProfileTab.Statistics),
            TabFor("Game log", PlayerProfileTab.GameLog),
            TabFor("Health", PlayerProfileTab.Health),

            // Waits for career history; the tab is listed so the profile's shape is known.
            new("Career", new RelayCommand(() => { }, () => false)),
        ];
        UpdateActiveTab();
        Rebuild();
    }

    public override string Subtitle => TeamName;

    public override object? Banner => _banner;

    public override IReadOnlyList<IPageTab>? Tabs => _tabs;

    public PlayerId Id => _playerId;

    public string FullName { get; private set; } = string.Empty;

    /// <summary>The jersey number with its hash, such as #19.</summary>
    public string Number { get; private set; } = string.Empty;

    public string Position { get; private set; } = string.Empty;

    public int Age { get; private set; }

    public string TeamName { get; private set; } = string.Empty;

    public TeamId TeamId { get; private set; }

    public bool IsGoalie { get; private set; }

    /// <summary>The player's overall rating for their position.</summary>
    public int Overall { get; private set; }

    /// <summary>The line the Overview shows beside the player's other ratings; the Ratings tab leads with it.</summary>
    public RatingLineViewModel OverallRating => new("Overall", Overall, Overall);

    /// <summary>Feet and inches, such as 6' 1".</summary>
    public string Height { get; private set; } = string.Empty;

    /// <summary>Pounds, such as 195 lb.</summary>
    public string Weight { get; private set; } = string.Empty;

    /// <summary>Shoots for a skater, Catches for a goalie.</summary>
    public string HandednessLabel { get; private set; } = string.Empty;

    public string Handedness { get; private set; } = string.Empty;

    public string BirthDate { get; private set; } = string.Empty;

    public string Birthplace { get; private set; } = string.Empty;

    public string Nationality { get; private set; } = string.Empty;

    public string LineupRole { get; private set; } = string.Empty;

    /// <summary>Born, birthplace, nationality, height, weight, handedness, position, and number.</summary>
    public IReadOnlyList<ProfileFactViewModel> Biography { get; private set; } = [];

    /// <summary>Time on ice by situation and the lineup role for a skater; starts, time in net, and the role for a goalie.</summary>
    public IReadOnlyList<ProfileFactViewModel> Usage { get; private set; } = [];

    /// <summary>The season and "statistics", such as "2026–27 statistics".</summary>
    public string StatisticsTitle { get; private set; } = string.Empty;

    /// <summary>The overview's figures: scoring, shooting and usage, on ice at 5v5, and physical (a goalie's are two groups).</summary>
    public IReadOnlyList<SeasonStatGroupViewModel> OverviewStatisticGroups { get; private set; } = [];

    /// <summary>
    /// The full statistic line for the chosen part of the season, grouped. Counts are zero before a
    /// first appearance, and a percentage, average, or rate is a dash until it is defined.
    /// </summary>
    public IReadOnlyList<SeasonStatGroupViewModel> StatisticGroups { get; private set; } = [];

    /// <summary>The player's ratings, in groups, with the overall rating apart.</summary>
    public IReadOnlyList<RatingGroupViewModel> RatingGroups { get; private set; } = [];

    /// <summary>The ratings of <see cref="RatingGroups"/> in one list.</summary>
    public IReadOnlyList<RatingLineViewModel> OverviewRatings { get; private set; } = [];

    /// <summary>
    /// When the injuries heal, such as "Injured until Sun 23 Nov", or "Injured until Sun 23 Nov – Wed 26 Nov" when
    /// the staff cannot say which day; null for a healthy player. It follows the injury that heals last.
    /// </summary>
    public string? InjuredUntil { get; private set; }

    /// <summary>Healthy, playing through injury, or out injured.</summary>
    public string Health { get; private set; } = string.Empty;

    public bool IsOut { get; private set; }

    public bool IsPlayingHurt => HasInjuries && !IsOut;

    /// <summary>The injuries that have not healed, in the order suffered.</summary>
    public IReadOnlyList<PlayerInjuryViewModel> Injuries { get; private set; } = [];

    public bool HasInjuries => Injuries.Count > 0;

    public bool IsHealthy => !HasInjuries;

    /// <summary>The column labels of <see cref="GameLog"/>'s cells: a skater's or a goalie's.</summary>
    public IReadOnlyList<string> GameLogColumns { get; private set; } = [];

    /// <summary>Every completed match of the season the player appeared in, newest first.</summary>
    public IReadOnlyList<PlayerGameRowViewModel> GameLog { get; private set; } = [];

    /// <summary>The newest five rows of <see cref="GameLog"/>.</summary>
    public IReadOnlyList<PlayerGameRowViewModel> LastFiveGames { get; private set; } = [];

    public bool ShowsOverview => Tab == PlayerProfileTab.Overview;

    public bool ShowsRatings => Tab == PlayerProfileTab.Ratings;

    public bool ShowsStatistics => Tab == PlayerProfileTab.Statistics;

    public bool ShowsGameLog => Tab == PlayerProfileTab.GameLog;

    public bool ShowsHealth => Tab == PlayerProfileTab.Health;

    /// <summary>Whether the playoffs have started, so there are playoff statistics to switch to.</summary>
    public bool HasPlayoffStatistics => _session.Snapshot.Season.Playoffs is not null;

    public bool ShowsRegularSeason => Statistics == StatisticsSet.RegularSeason;

    public bool ShowsPlayoffs => Statistics == StatisticsSet.Playoffs;

    public override void Refresh()
    {
        Rebuild();
    }

    [RelayCommand]
    private void ShowStatisticsSet(StatisticsSet statistics)
    {
        Statistics = statistics.OrRegularSeason(HasPlayoffStatistics);
    }

    partial void OnTabChanged(PlayerProfileTab value)
    {
        UpdateActiveTab();
    }

    partial void OnStatisticsChanged(StatisticsSet value)
    {
        Rebuild();
    }

    partial void OnSelectedGameChanged(PlayerGameRowViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedGame = null;
        _openMatch(value.Date, value.TeamId);
    }

    private PlayerProfileTabViewModel TabFor(string label, PlayerProfileTab tab) =>
        new(label, new RelayCommand(() => Tab = tab));

    private void UpdateActiveTab()
    {
        for (var index = 0; index < Enum.GetValues<PlayerProfileTab>().Length; index++)
        {
            _tabs[index].IsActive = index == (int)Tab;
        }
    }

    /// <summary>Builds everything the page shows from the session's latest snapshot and the chosen statistic set.</summary>
    private void Rebuild()
    {
        var snapshot = _session.Snapshot;
        var team = _session.TeamOf(_playerId);
        var player = team.Roster.Single(candidate => candidate.Id == _playerId);
        var totals = _session.GetSeasonTotals(_playerId, Statistics);
        var isGoalie = player.Position == Domain.Position.Goalie;

        _banner.Show(player, team, totals);

        FullName = PlayerDisplay.FullName(player);
        Number = $"#{player.Number}";
        Position = PlayerDisplay.PositionName(player.Position);
        Age = player.Age;
        TeamName = team.Name;
        TeamId = team.Id;
        IsGoalie = isGoalie;
        Overall = player.Overall;
        Height = PlayerDisplay.FormatHeight(player.Biography.Height);
        Weight = PlayerDisplay.FormatWeight(player.Biography.Weight);
        HandednessLabel = PlayerDisplay.HandednessLabel(player.Position).ToUpperInvariant();
        Handedness = PlayerDisplay.HandednessName(player.Biography.Handedness);
        BirthDate = PlayerDisplay.FormatBirthDate(player.Biography.BirthDate);
        Birthplace = PlayerDisplay.FormatBirthplace(player.Biography.Birthplace);
        Nationality = PlayerDisplay.CountryName(player.Biography.Nationality);
        LineupRole = PlayerDisplay.LineupRole(player.Id, team.Lineup);
        Biography =
        [
            new("Born", BirthDate),
            new("Birthplace", Birthplace),
            new("Nationality", Nationality),
            new("Height", Height),
            new("Weight", Weight),
            new(PlayerDisplay.HandednessLabel(player.Position), Handedness),
            new("Position", Position),
            new("Number", player.Number.ToString(CultureInfo.CurrentCulture)),
        ];
        Usage = isGoalie ? GoalieUsage(totals) : SkaterUsage(team, totals);

        StatisticsTitle = $"{PlayerDisplay.FormatSeason(snapshot.League.SeasonYear)} statistics";
        OverviewStatisticGroups = totals.OverviewGroups(isGoalie);
        StatisticGroups = totals.ProfileGroups(isGoalie);

        RatingGroups = (isGoalie ? GoalieGroups : SkaterGroups)
            .Select(group => new RatingGroupViewModel(group.Name, group.Ratings.Select(rating => RatingLine(player, rating)).ToList()))
            .ToList();
        OverviewRatings = RatingGroups.SelectMany(group => group.Ratings).ToList();

        Health = player.Injuries.Count == 0 ? "Healthy" : player.CanPlay ? "Playing through injury" : "Out injured";
        IsOut = !player.CanPlay;
        Injuries = player.Injuries.Select(injury => new PlayerInjuryViewModel(injury)).ToList();
        InjuredUntil = InjuryDisplay.InjuredUntil(player.Injuries);

        GameLogColumns = isGoalie ? PlayerGameLog.GoalieColumns : PlayerGameLog.SkaterColumns;
        GameLog = PlayerGameLog.For(_session, player, team);
        LastFiveGames = GameLog.Take(LastGames).ToList();

        // Everything may have changed; the properties above have no change notification of their own.
        OnPropertyChanged(string.Empty);
    }

    private static RatingLineViewModel RatingLine(PlayerSnapshot player, Rating rating)
    {
        var healthy = player.Ratings[rating];
        var reduction = player.RatingReductions.GetValueOrDefault(rating);
        return new RatingLineViewModel(PlayerDisplay.RatingName(rating), Math.Max(0, healthy - reduction), healthy);
    }

    private List<ProfileFactViewModel> SkaterUsage(TeamSnapshot team, PlayerSeasonTotals totals)
    {
        var perGame = totals.TimeOnIceBySituationPerGame;
        return
        [
            new("Even strength / GP", MatchDisplay.TimeOnIce(perGame is null ? null : perGame.FiveOnFive + perGame.Other)),
            new("Power play / GP", WithUnits(MatchDisplay.TimeOnIce(perGame?.PowerPlay), UnitTags(team.Lineup, PowerPlayUnits))),
            new("Penalty kill / GP", WithUnits(MatchDisplay.TimeOnIce(perGame?.PenaltyKill), UnitTags(team.Lineup, PenaltyKillUnits))),
            new("Line", LineupRole),
        ];
    }

    private List<ProfileFactViewModel> GoalieUsage(PlayerSeasonTotals totals) =>
    [
        new("Starts", totals.GamesPlayed.ToString(CultureInfo.CurrentCulture)),
        new("TOI/GP", totals.TimeOnIcePerGame),
        new("Lineup", LineupRole),
    ];

    private static string WithUnits(string time, IReadOnlyList<string> units) =>
        units.Count == 0 ? time : $"{time} · {string.Join(" · ", units)}";

    /// <summary>
    /// The player's unit in each of the situations, in order, such as PP1, 5v3 U1, and 4v3 U2. The
    /// main situation (five-on-four or four-on-five) keeps the usual PP or PK tag.
    /// </summary>
    private List<string> UnitTags(LineupSnapshot lineup, IReadOnlyList<(SpecialSituation Situation, string Prefix)> situations)
    {
        var tags = new List<string>();
        foreach (var (situation, prefix) in situations)
        {
            var units = lineup.UnitsFor(situation);
            for (var index = 0; index < units.Count; index++)
            {
                if (units[index].PlayerIds.Contains(_playerId))
                {
                    tags.Add(string.Create(CultureInfo.CurrentCulture, $"{prefix}{index + 1}"));
                    break;
                }
            }
        }

        return tags;
    }
}

/// <summary>A label and its value, such as one line of the biography.</summary>
public sealed record ProfileFactViewModel(string Label, string Value);

/// <summary>A player's profile as a history entry: the player, and the tab and statistics set shown.</summary>
public sealed record PlayerProfileSelection(PlayerId Player, PlayerProfileTab Tab, StatisticsSet Statistics);