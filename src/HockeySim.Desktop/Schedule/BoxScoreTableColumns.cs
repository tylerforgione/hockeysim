using HockeySim.Desktop.Tables;

namespace HockeySim.Desktop.Schedule;

/// <summary>
/// The sortable columns of a box score's skater tables. A skater who took no faceoffs shows a dash
/// and sorts last on that column.
/// </summary>
public static class BoxScoreTableColumns
{
    public static IReadOnlyList<TableColumn<SkaterBoxScoreRowViewModel>> Skaters { get; } =
    [
        new("name", row => row.Values.Name, AscendingFirst: true),
        new("position", row => row.Values.Position, AscendingFirst: true),
        new("goals", row => row.Goals),
        new("assists", row => row.Assists),
        new("points", row => row.Points),
        new("plus-minus", row => row.Values.PlusMinus),
        new("time-on-ice", row => row.Values.TimeOnIce),
        new("shots", row => row.Shots),
        new("shot-attempts", row => row.ShotAttempts),
        new("expected-goals", row => row.Values.ExpectedGoals),
        new("hits", row => row.Hits),
        new("blocked-shots", row => row.BlockedShots),
        new("faceoffs", row => row.Values.Faceoffs),
        new("takeaways", row => row.Takeaways),
        new("giveaways", row => row.Giveaways),
        new("penalty-minutes", row => row.PenaltyMinutes),
        new("power-play-goals", row => row.PowerPlayGoals),
        new("shorthanded-goals", row => row.ShorthandedGoals),
        new("corsi-percentage", row => row.Values.CorsiPercentage),
        new("expected-goals-percentage", row => row.Values.ExpectedGoalsPercentage),
    ];
}