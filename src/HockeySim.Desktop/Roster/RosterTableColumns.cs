using HockeySim.Desktop.Tables;

namespace HockeySim.Desktop.Roster;

/// <summary>
/// The sortable columns of each roster table, keyed as the headers in <c>TeamRosterView</c> key
/// them. Sort keys are the raw figures, not the displayed text; a dash is a null key.
/// </summary>
public static class RosterTableColumns
{
    public static IReadOnlyList<TableColumn<PlayerRowViewModel>> Skaters(RosterColumns columns)
    {
        List<TableColumn<PlayerRowViewModel>> identity =
        [
            new("number", row => row.Number, AscendingFirst: true),
            Name,
            new("position", row => row.Player.Position, AscendingFirst: true),
            Age,
            Nationality,
            new("shoots", row => row.Handedness, AscendingFirst: true),
            Lineup,
        ];
        return [.. identity, .. columns switch
        {
            RosterColumns.Ratings => SkaterRatings,
            RosterColumns.Basic => SkaterBasic,
            RosterColumns.Advanced => SkaterAdvanced,
            _ => throw new ArgumentOutOfRangeException(nameof(columns), columns, "Unknown columns."),
        }];
    }

    public static IReadOnlyList<TableColumn<PlayerRowViewModel>> Goalies(RosterColumns columns)
    {
        List<TableColumn<PlayerRowViewModel>> identity =
        [
            new("number", row => row.Number, AscendingFirst: true),
            Name,
            Age,
            Nationality,
            new("catches", row => row.Handedness, AscendingFirst: true),
            Lineup,
        ];
        return [.. identity, .. columns switch
        {
            RosterColumns.Ratings => GoalieRatings,
            RosterColumns.Basic => GoalieBasic,
            RosterColumns.Advanced => GoalieAdvanced,
            _ => throw new ArgumentOutOfRangeException(nameof(columns), columns, "Unknown columns."),
        }];
    }

    // Last name, then first name, compared ordinally: the separator sorts before any letter.
    private static TableColumn<PlayerRowViewModel> Name { get; } =
        new("name", row => $"{row.Player.LastName}\0{row.Player.FirstName}", AscendingFirst: true);

    private static TableColumn<PlayerRowViewModel> Age { get; } = new("age", row => row.Age);

    private static TableColumn<PlayerRowViewModel> Nationality { get; } =
        new("nationality", row => row.Nationality, AscendingFirst: true);

    private static TableColumn<PlayerRowViewModel> Lineup { get; } =
        new("lineup", row => row.LineupOrder, AscendingFirst: true);

    private static List<TableColumn<PlayerRowViewModel>> SkaterRatings =>
    [
        new("overall", row => row.Overall),
        new("skating", row => row.Skating),
        new("shot-power", row => row.ShotPower),
        new("shot-accuracy", row => row.ShotAccuracy),
        new("puck-control", row => row.PuckControl),
        new("passing", row => row.Passing),
        new("offensive-awareness", row => row.OffensiveAwareness),
        new("defensive-awareness", row => row.DefensiveAwareness),
        new("checking", row => row.Checking),
        new("shot-blocking", row => row.ShotBlocking),
        new("stick-checking", row => row.StickChecking),
    ];

    private static List<TableColumn<PlayerRowViewModel>> SkaterBasic =>
    [
        new("games-played", row => row.Season.GamesPlayed),
        new("goals", row => row.Season.Goals),
        new("assists", row => row.Season.Assists),
        new("points", row => row.Season.Points),
        new("plus-minus", row => row.Season.PlusMinusValue),
        new("penalty-minutes", row => row.Season.PenaltyMinutes),
        new("power-play-points", row => row.Season.PowerPlayPoints),
        new("shorthanded-points", row => row.Season.ShorthandedPoints),
        new("shots", row => row.Season.Shots),
        new("time-on-ice-per-game", row => row.Season.TimeOnIcePerGameValue),
        new("faceoff-percentage", row => row.Season.FaceoffPercentageValue),
    ];

    private static List<TableColumn<PlayerRowViewModel>> SkaterAdvanced =>
    [
        new("games-played", row => row.Season.GamesPlayed),
        new("corsi-for", row => row.Season.CorsiFor),
        new("corsi-against", row => row.Season.CorsiAgainst),
        new("corsi-percentage", row => row.Season.CorsiPercentageValue),
        new("fenwick-for", row => row.Season.FenwickFor),
        new("fenwick-against", row => row.Season.FenwickAgainst),
        new("fenwick-percentage", row => row.Season.FenwickPercentageValue),
        new("expected-goals-for", row => row.Season.OnIceExpectedGoalsForValue),
        new("expected-goals-against", row => row.Season.OnIceExpectedGoalsAgainstValue),
        new("expected-goals-percentage", row => row.Season.OnIceExpectedGoalsPercentageValue),
        new("individual-expected-goals", row => row.Season.IndividualExpectedGoalsValue),
    ];

    private static List<TableColumn<PlayerRowViewModel>> GoalieRatings =>
    [
        new("overall", row => row.Overall),
        new("reflex", row => row.GoalieReflex),
        new("positioning", row => row.GoaliePositioning),
        new("rebound-control", row => row.GoalieReboundControl),
    ];

    private static List<TableColumn<PlayerRowViewModel>> GoalieBasic =>
    [
        new("games-played", row => row.Season.GamesPlayed),
        new("shots-against", row => row.Season.ShotsAgainst),
        new("saves", row => row.Season.Saves),
        new("goals-against", row => row.Season.GoalsAgainst),
        new("save-percentage", row => row.Season.SavePercentageValue),
        new("goals-against-average", row => row.Season.GoalsAgainstAverageValue),
        new("shutouts", row => row.Season.Shutouts),
    ];

    private static List<TableColumn<PlayerRowViewModel>> GoalieAdvanced =>
    [
        new("games-played", row => row.Season.GamesPlayed),
        new("time-in-net", row => row.Season.TimeInNetValue),
        new("expected-goals-against", row => row.Season.ExpectedGoalsAgainstValue),
        new("goals-against", row => row.Season.GoalsAgainst),
        new("goals-saved-above-expected", row => row.Season.GoalsSavedAboveExpectedValue),
        new("time-on-ice-per-game", row => row.Season.TimeOnIcePerGameValue),
    ];
}