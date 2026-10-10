using System.Globalization;

using HockeySim.Desktop.Players;
using HockeySim.Desktop.Schedule;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Roster;

/// <summary>
/// Formats a team's season statistics for the strip above its roster. Shot, attempt, and
/// expected-goal shares are at five-on-five so special-teams time does not distort them.
/// </summary>
public static class TeamStatisticsDisplay
{
    /// <summary>Statistics for a team that has not played, such as one that missed the playoffs.</summary>
    public static TeamSeasonStatisticsSnapshot None(TeamId teamId) =>
        new(teamId, 0, 0, 0, null, 0, 0, null, 0, 0, 0, null, SituationalShotTotals.None);

    public static IReadOnlyList<SeasonStatViewModel> Strip(TeamSeasonStatisticsSnapshot statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);

        var fiveOnFive = statistics.ShotTotals.FiveOnFive;
        return
        [
            new("PP%", MatchDisplay.Percentage(statistics.PowerPlayPercentage), string.Create(
                CultureInfo.CurrentCulture,
                $"Power-play percentage: {statistics.PowerPlayGoals} goals on {statistics.PowerPlayOpportunities} opportunities")),
            new("PK%", MatchDisplay.Percentage(statistics.PenaltyKillPercentage), string.Create(
                CultureInfo.CurrentCulture,
                $"Penalty-kill percentage: {statistics.PowerPlayGoalsAgainst} goals against in {statistics.TimesShorthanded} times shorthanded")),
            new("FO%", MatchDisplay.Percentage(statistics.FaceoffPercentage), "Faceoff percentage"),
            new("CF%", MatchDisplay.Percentage(fiveOnFive.CorsiPercentage), "5-on-5 Corsi percentage: share of shot attempts"),
            new("FF%", MatchDisplay.Percentage(fiveOnFive.FenwickPercentage), "5-on-5 Fenwick percentage: share of unblocked attempts"),
            new("SF%", MatchDisplay.Percentage(fiveOnFive.ShotsPercentage), "5-on-5 share of shots on goal"),
            new("xGF%", MatchDisplay.Percentage(fiveOnFive.ExpectedGoalsPercentage), "5-on-5 share of expected goals"),
        ];
    }
}