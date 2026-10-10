using System.Globalization;

using HockeySim.Desktop.Players;
using HockeySim.Desktop.Schedule;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.TeamStatistics;

/// <summary>
/// Formats a team's season statistics for the team statistics page. Shot, attempt, and
/// expected-goal shares are at five-on-five so special-teams time does not distort them.
/// </summary>
public static class TeamStatisticsDisplay
{
    public static IReadOnlyList<SeasonStatViewModel> Rows(TeamSeasonStatisticsSnapshot statistics)
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
            new("CF%", MatchDisplay.Percentage(fiveOnFive.CorsiPercentage), "5-on-5 Corsi percentage"),
            new("FF%", MatchDisplay.Percentage(fiveOnFive.FenwickPercentage), "5-on-5 Fenwick percentage"),
            new("SF%", MatchDisplay.Percentage(fiveOnFive.ShotsPercentage), "5-on-5 share of shots on goal"),
            new("xGF%", MatchDisplay.Percentage(fiveOnFive.ExpectedGoalsPercentage), "5-on-5 share of expected goals"),
        ];
    }
}