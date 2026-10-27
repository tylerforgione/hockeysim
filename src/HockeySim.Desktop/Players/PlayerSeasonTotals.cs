using System.Globalization;

using HockeySim.Desktop.Schedule;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Players;

/// <summary>
/// One player's current-season totals as the roster tables and player profile show them. A skater
/// has skater totals and a goalie goalie totals; a player who has not appeared has
/// <see cref="None"/>, which shows zero counts and a dash for every value not yet defined. The
/// advanced (on-ice) values are at five-on-five, the usual measure of possession.
/// </summary>
public sealed class PlayerSeasonTotals
{
    private readonly SkaterSeasonStatisticsSnapshot? _skater;
    private readonly GoalieSeasonStatisticsSnapshot? _goalie;

    private PlayerSeasonTotals(SkaterSeasonStatisticsSnapshot? skater, GoalieSeasonStatisticsSnapshot? goalie)
    {
        _skater = skater;
        _goalie = goalie;
    }

    public static PlayerSeasonTotals None { get; } = new(null, null);

    public int GamesPlayed => _skater?.GamesPlayed ?? _goalie?.GamesPlayed ?? 0;

    public int Goals => _skater?.Goals ?? 0;

    public int Assists => _skater?.Assists ?? 0;

    public int Points => _skater?.Points ?? 0;

    public string PlusMinus => MatchDisplay.PlusMinus(PlusMinusValue);

    // The "...Value" properties are the raw figures behind the formatted strings, which tables sort
    // by. A null is a figure that is not defined yet, shown as a dash.
    public int PlusMinusValue => _skater?.PlusMinus ?? 0;

    public int PenaltyMinutes => _skater?.PenaltyMinutes ?? 0;

    public int PowerPlayPoints => _skater?.PowerPlayPoints ?? 0;

    public int ShorthandedPoints => _skater?.ShorthandedPoints ?? 0;

    public int Shots => _skater?.Shots ?? 0;

    /// <summary>Goals as a share of shots on goal, or a dash before a first shot.</summary>
    public string ShootingPercentage => MatchDisplay.Percentage(ShootingPercentageValue);

    public double? ShootingPercentageValue => _skater is { Shots: > 0 } skater ? skater.Goals / (double)skater.Shots : null;

    /// <summary>
    /// The skater's average time on ice in each strength situation, or null before an appearance
    /// (and for a goalie).
    /// </summary>
    public SituationalTimeOnIce? TimeOnIceBySituationPerGame => _skater?.TimeOnIceBySituationPerGame;

    public string TimeOnIcePerGame => MatchDisplay.TimeOnIce(TimeOnIcePerGameValue);

    public TimeSpan? TimeOnIcePerGameValue => _skater?.TimeOnIcePerGame ?? _goalie?.TimeOnIcePerGame;

    public string FaceoffPercentage => MatchDisplay.Percentage(FaceoffPercentageValue);

    public double? FaceoffPercentageValue => _skater?.FaceoffPercentage;

    /// <summary>The summed expected-goal value of the skater's own attempts (ixG).</summary>
    public string IndividualExpectedGoals => MatchDisplay.ExpectedGoals(IndividualExpectedGoalsValue);

    public double IndividualExpectedGoalsValue => _skater?.ExpectedGoals ?? 0;

    /// <summary>Five-on-five shot attempts for while on the ice.</summary>
    public int CorsiFor => FiveOnFive.AttemptsFor;

    public int CorsiAgainst => FiveOnFive.AttemptsAgainst;

    public string CorsiPercentage => MatchDisplay.Percentage(CorsiPercentageValue);

    public double? CorsiPercentageValue => FiveOnFive.CorsiPercentage;

    /// <summary>Five-on-five unblocked attempts for while on the ice.</summary>
    public int FenwickFor => FiveOnFive.UnblockedAttemptsFor;

    public int FenwickAgainst => FiveOnFive.UnblockedAttemptsAgainst;

    public string FenwickPercentage => MatchDisplay.Percentage(FenwickPercentageValue);

    public double? FenwickPercentageValue => FiveOnFive.FenwickPercentage;

    /// <summary>Five-on-five expected goals for while on the ice.</summary>
    public string OnIceExpectedGoalsFor => MatchDisplay.ExpectedGoals(OnIceExpectedGoalsForValue);

    public double OnIceExpectedGoalsForValue => FiveOnFive.ExpectedGoalsFor;

    public string OnIceExpectedGoalsAgainst => MatchDisplay.ExpectedGoals(OnIceExpectedGoalsAgainstValue);

    public double OnIceExpectedGoalsAgainstValue => FiveOnFive.ExpectedGoalsAgainst;

    public string OnIceExpectedGoalsPercentage => MatchDisplay.Percentage(OnIceExpectedGoalsPercentageValue);

    public double? OnIceExpectedGoalsPercentageValue => FiveOnFive.ExpectedGoalsPercentage;

    public int ShotsAgainst => _goalie?.ShotsAgainst ?? 0;

    public int Saves => _goalie?.Saves ?? 0;

    public int GoalsAgainst => _goalie?.GoalsAgainst ?? 0;

    /// <summary>".912" style, or a dash before the goalie has faced a shot.</summary>
    public string SavePercentage => MatchDisplay.SavePercentage(SavePercentageValue);

    public double? SavePercentageValue => _goalie?.SavePercentage;

    public string GoalsAgainstAverage => MatchDisplay.GoalsAgainstAverage(GoalsAgainstAverageValue);

    public double? GoalsAgainstAverageValue => _goalie?.GoalsAgainstAverage;

    public int Shutouts => _goalie?.Shutouts ?? 0;

    /// <summary>The expected-goal value of the shots a goalie faced.</summary>
    public string ExpectedGoalsAgainst => MatchDisplay.ExpectedGoals(ExpectedGoalsAgainstValue);

    public double ExpectedGoalsAgainstValue => _goalie?.ExpectedGoalsAgainst ?? 0;

    /// <summary>
    /// The save percentage an average goalie would have had on the shots faced (one minus expected
    /// goals against over shots against), formatted like <see cref="SavePercentage"/>.
    /// </summary>
    public string ExpectedSavePercentage => MatchDisplay.SavePercentage(ExpectedSavePercentageValue);

    /// <summary>Null before the goalie has faced a shot, shown as a dash.</summary>
    public double? ExpectedSavePercentageValue =>
        _goalie is { ShotsAgainst: > 0 } goalie ? 1 - goalie.ExpectedGoalsAgainst / goalie.ShotsAgainst : null;

    public string GoalsSavedAboveExpected => MatchDisplay.GoalsSavedAboveExpected(GoalsSavedAboveExpectedValue);

    public double GoalsSavedAboveExpectedValue => _goalie?.GoalsSavedAboveExpected ?? 0;

    /// <summary>A goalie's total time in net.</summary>
    public string TimeInNet => MatchDisplay.TimeOnIce(TimeInNetValue);

    public TimeSpan TimeInNetValue => _goalie?.TimeOnIce ?? TimeSpan.Zero;

    private ShotTotals FiveOnFive => _skater?.OnIce.FiveOnFive ?? ShotTotals.None;

    /// <summary>
    /// Indexes the regular season's totals by player, or the playoffs' (none before they start).
    /// Skaters and goalies are disjoint: a goalie only appears in goal, so no player has both
    /// kinds of totals.
    /// </summary>
    public static Dictionary<PlayerId, PlayerSeasonTotals> Index(SeasonSnapshot season, StatisticsSet statistics)
    {
        ArgumentNullException.ThrowIfNull(season);

        var (skaters, goalies) = statistics switch
        {
            StatisticsSet.RegularSeason => (season.SkaterStatistics, season.GoalieStatistics),
            StatisticsSet.Playoffs => (
                season.Playoffs?.SkaterStatistics ?? [],
                season.Playoffs?.GoalieStatistics ?? (IReadOnlyList<GoalieSeasonStatisticsSnapshot>)[]),
            _ => throw new ArgumentOutOfRangeException(nameof(statistics), statistics, "Unknown statistics set."),
        };

        return skaters
            .Select(skater => (skater.PlayerId, Totals: new PlayerSeasonTotals(skater, null)))
            .Concat(goalies.Select(goalie => (goalie.PlayerId, Totals: new PlayerSeasonTotals(null, goalie))))
            .ToDictionary(pair => pair.PlayerId, pair => pair.Totals);
    }

    /// <summary>The summary line for the profile's overview, grouped: fewer figures than <see cref="ProfileGroups"/>.</summary>
    public IReadOnlyList<SeasonStatGroupViewModel> OverviewGroups(bool isGoalie) =>
        isGoalie ? GoalieOverviewGroups() : SkaterOverviewGroups();

    /// <summary>The full statistic line for the player profile, grouped.</summary>
    public IReadOnlyList<SeasonStatGroupViewModel> ProfileGroups(bool isGoalie) =>
        isGoalie ? GoalieGroups() : SkaterGroups();

    private List<SeasonStatGroupViewModel> SkaterOverviewGroups()
    {
        var skater = _skater;
        var fiveOnFive = FiveOnFive;
        return
        [
            new("SCORING",
            [
                Count("GP", GamesPlayed, "Games played"),
                Count("G", Goals, "Goals"),
                Count("A", Assists, "Assists"),
                Count("P", Points, "Points"),
                new("+/-", PlusMinus, "Plus/minus"),
                Count("PIM", PenaltyMinutes, "Penalty minutes"),
            ]),
            new("SHOOTING AND USAGE",
            [
                Count("SOG", Shots, "Shots on goal"),
                new("SH%", ShootingPercentage, "Shooting percentage: goals per shot on goal"),
                new("ixG", IndividualExpectedGoals, "Individual expected goals"),
                new("TOI/GP", TimeOnIcePerGame, "Average time on ice per game"),
                Count("PPP", PowerPlayPoints, "Power-play points"),
                Count("SHP", ShorthandedPoints, "Shorthanded points"),
            ]),
            new("ON ICE AT 5V5",
            [
                Count("CF", fiveOnFive.AttemptsFor, "Corsi for: team shot attempts while on the ice"),
                Count("CA", fiveOnFive.AttemptsAgainst, "Corsi against: opponent shot attempts while on the ice"),
                new("CF%", CorsiPercentage, "Corsi percentage"),
                new("xGF", OnIceExpectedGoalsFor, "Team expected goals while on the ice"),
                new("xGA", OnIceExpectedGoalsAgainst, "Opponent expected goals while on the ice"),
                new("xGF%", OnIceExpectedGoalsPercentage, "Share of expected goals"),
            ]),
            new("PHYSICAL",
            [
                Count("HIT", skater?.Hits ?? 0, "Hits"),
                Count("BLK", skater?.BlockedShots ?? 0, "Opponent shot attempts blocked"),
                Count("TK", skater?.Takeaways ?? 0, "Takeaways"),
                Count("GV", skater?.Giveaways ?? 0, "Giveaways"),
                new("FO%", FaceoffPercentage, "Faceoff percentage"),
            ]),
        ];
    }

    private List<SeasonStatGroupViewModel> GoalieOverviewGroups() =>
    [
        new("GOALTENDING",
        [
            Count("GP", GamesPlayed, "Games played (starts)"),
            Count("SA", ShotsAgainst, "Shots against"),
            Count("SV", Saves, "Saves"),
            Count("GA", GoalsAgainst, "Goals against"),
            new("SV%", SavePercentage, "Save percentage"),
            new("GAA", GoalsAgainstAverage, "Goals-against average"),
        ]),
        new("EXPECTED AND USAGE",
        [
            new("xGA", ExpectedGoalsAgainst, "Expected goals against"),
            new("GSAx", GoalsSavedAboveExpected, "Goals saved above expected"),
            new("xSV%", ExpectedSavePercentage, "Expected save percentage"),
            Count("SO", Shutouts, "Shutouts"),
            new("TOI/GP", TimeOnIcePerGame, "Average time in net per start"),
        ]),
    ];

    private List<SeasonStatGroupViewModel> SkaterGroups()
    {
        var skater = _skater;
        var fiveOnFive = FiveOnFive;
        return
        [
            new("SCORING",
            [
                Count("GP", GamesPlayed, "Games played"),
                Count("G", Goals, "Goals"),
                Count("A", Assists, "Assists"),
                Count("P", Points, "Points"),
                new("+/-", PlusMinus, "Plus/minus"),
                Count("PIM", PenaltyMinutes, "Penalty minutes"),
                Count("ENG", skater?.EmptyNetGoals ?? 0, "Empty-net goals"),
            ]),
            new("SPECIAL TEAMS",
            [
                Count("PPG", skater?.PowerPlayGoals ?? 0, "Power-play goals"),
                Count("PPA", skater?.PowerPlayAssists ?? 0, "Power-play assists"),
                Count("PPP", PowerPlayPoints, "Power-play points"),
                Count("SHG", skater?.ShorthandedGoals ?? 0, "Shorthanded goals"),
                Count("SHA", skater?.ShorthandedAssists ?? 0, "Shorthanded assists"),
                Count("SHP", ShorthandedPoints, "Shorthanded points"),
            ]),
            new("SHOOTING AND PLAY",
            [
                Count("S", Shots, "Shots on goal"),
                Count("SAT", skater?.ShotAttempts ?? 0, "Shot attempts: on goal, missed, or blocked"),
                new("ixG", IndividualExpectedGoals, "Individual expected goals"),
                Count("HIT", skater?.Hits ?? 0, "Hits"),
                Count("BLK", skater?.BlockedShots ?? 0, "Opponent shot attempts blocked"),
                Count("TK", skater?.Takeaways ?? 0, "Takeaways"),
                Count("GV", skater?.Giveaways ?? 0, "Giveaways"),
            ]),
            new("FACEOFFS AND ICE TIME",
            [
                Count("FOW", skater?.FaceoffsWon ?? 0, "Faceoffs won"),
                Count("FOL", skater?.FaceoffsLost ?? 0, "Faceoffs lost"),
                new("FO%", FaceoffPercentage, "Faceoff percentage"),
                new("TOI", MatchDisplay.TimeOnIce(skater?.TimeOnIce ?? TimeSpan.Zero), "Total time on ice"),
                new("TOI/GP", TimeOnIcePerGame, "Average time on ice per game"),
            ]),
            new("5-ON-5 ON ICE",
            [
                Count("CF", fiveOnFive.AttemptsFor, "Corsi for: team shot attempts while on the ice"),
                Count("CA", fiveOnFive.AttemptsAgainst, "Corsi against: opponent shot attempts while on the ice"),
                new("CF%", CorsiPercentage, "Corsi percentage"),
                Count("FF", fiveOnFive.UnblockedAttemptsFor, "Fenwick for: unblocked team attempts while on the ice"),
                Count("FA", fiveOnFive.UnblockedAttemptsAgainst, "Fenwick against: unblocked opponent attempts while on the ice"),
                new("FF%", FenwickPercentage, "Fenwick percentage"),
                Count("SF", fiveOnFive.ShotsFor, "Team shots on goal while on the ice"),
                Count("SA", fiveOnFive.ShotsAgainst, "Opponent shots on goal while on the ice"),
                new("SF%", MatchDisplay.Percentage(fiveOnFive.ShotsPercentage), "Share of shots on goal"),
                Count("GF", fiveOnFive.GoalsFor, "Team goals while on the ice"),
                Count("GA", fiveOnFive.GoalsAgainst, "Opponent goals while on the ice"),
                new("GF%", MatchDisplay.Percentage(fiveOnFive.GoalsPercentage), "Share of goals"),
                new("xGF", OnIceExpectedGoalsFor, "Team expected goals while on the ice"),
                new("xGA", OnIceExpectedGoalsAgainst, "Opponent expected goals while on the ice"),
                new("xGF%", OnIceExpectedGoalsPercentage, "Share of expected goals"),
            ]),
        ];
    }

    private List<SeasonStatGroupViewModel> GoalieGroups() =>
    [
        new("GOALTENDING",
        [
            Count("GP", GamesPlayed, "Games played (starts)"),
            Count("SA", ShotsAgainst, "Shots against"),
            Count("SV", Saves, "Saves"),
            Count("GA", GoalsAgainst, "Goals against"),
            new("SV%", SavePercentage, "Save percentage"),
            new("GAA", GoalsAgainstAverage, "Goals-against average"),
            Count("SO", Shutouts, "Shutouts"),
        ]),
        new("EXPECTED GOALS AND ICE TIME",
        [
            new("xGA", ExpectedGoalsAgainst, "Expected goals against"),
            new("GSAx", GoalsSavedAboveExpected, "Goals saved above expected"),
            new("TOI", TimeInNet, "Total time in net"),
            new("TOI/GP", TimeOnIcePerGame, "Average time in net per start"),
        ]),
    ];

    private static SeasonStatViewModel Count(string label, int value, string description) =>
        new(label, value.ToString(CultureInfo.CurrentCulture), description);
}

public sealed record SeasonStatViewModel(string Label, string Value, string Description);

public sealed record SeasonStatGroupViewModel(string Name, IReadOnlyList<SeasonStatViewModel> Statistics);