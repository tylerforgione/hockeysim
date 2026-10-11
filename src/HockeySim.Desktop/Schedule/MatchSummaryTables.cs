using System.Globalization;

using HockeySim.Desktop.Players;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Matches;

namespace HockeySim.Desktop.Schedule;

/// <summary>
/// Builds the box score's summary tables: the game by period, the teams compared, and the three
/// stars.
/// </summary>
public static class MatchSummaryTables
{
    /// <summary>The share shown when neither team has any of a figure: an even split.</summary>
    private const double EvenShare = 0.5;

    /// <summary>
    /// Goals, shots, expected goals, and the power play (goals of opportunities) for each period
    /// played and in total, away first. Only the goals table has a shootout column, since the
    /// shootout's one deciding goal is in the score alone.
    /// </summary>
    public static IReadOnlyList<PeriodTableViewModel> ByPeriod(CompletedMatchSnapshot result, string awayInitials, string homeInitials)
    {
        ArgumentNullException.ThrowIfNull(result);

        var periods = Enumerable.Range(1, result.Home.Periods.Count).ToList();
        var periodNames = periods.Select(MatchDisplay.PeriodName).ToList();
        var isShootout = result.Decision == MatchDecision.Shootout;
        var sides = new[] { (Initials: awayInitials, Side: result.Away), (Initials: homeInitials, Side: result.Home) };

        int GoalsIn(CompletedMatchTeamSnapshot side, int period, bool powerPlayOnly = false) =>
            result.Goals.Count(goal => goal.TeamId == side.TeamId && goal.Period == period
                && (!powerPlayOnly || goal.Situation == GoalSituation.PowerPlay));

        return
        [
            new PeriodTableViewModel(
                "GOALS",
                isShootout ? [.. periodNames, "SO"] : periodNames,
                sides.Select(entry => new PeriodRowViewModel(
                    entry.Initials,
                    periods.Select(period => Number(GoalsIn(entry.Side, period)))
                        .Concat(isShootout ? [Number(entry.Side.TeamId == result.WinnerId ? 1 : 0)] : [])
                        .ToList(),
                    Number(entry.Side.Score))).ToList()),
            new PeriodTableViewModel(
                "SHOTS",
                periodNames,
                sides.Select(entry => new PeriodRowViewModel(
                    entry.Initials,
                    entry.Side.Periods.Select(period => Number(period.Shots)).ToList(),
                    Number(entry.Side.Shots))).ToList()),
            new PeriodTableViewModel(
                "EXPECTED GOALS",
                periodNames,
                sides.Select(entry => new PeriodRowViewModel(
                    entry.Initials,
                    entry.Side.Periods.Select(period => MatchDisplay.ExpectedGoals(period.ExpectedGoals)).ToList(),
                    MatchDisplay.ExpectedGoals(entry.Side.Skaters.Sum(skater => skater.ExpectedGoals)))).ToList()),
            new PeriodTableViewModel(
                "POWER PLAY",
                periodNames,
                sides.Select(entry => new PeriodRowViewModel(
                    entry.Initials,
                    entry.Side.Periods
                        .Select(period => MatchDisplay.PowerPlay(GoalsIn(entry.Side, period.Period, powerPlayOnly: true), period.PowerPlayOpportunities))
                        .ToList(),
                    MatchDisplay.PowerPlay(entry.Side.PowerPlayGoals, entry.Side.PowerPlayOpportunities))).ToList()),
        ];
    }

    /// <summary>
    /// The teams' shots, attempts, expected goals, five-on-five expected-goal share, faceoffs won,
    /// hits, blocks, giveaways, and penalty minutes, each with the away team's share of the two.
    /// </summary>
    public static IReadOnlyList<ComparisonRowViewModel> Comparison(CompletedMatchSnapshot result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var (away, home) = (result.Away, result.Home);
        var awayFiveOnFiveShare = away.ShotTotals.FiveOnFive.ExpectedGoalsPercentage;
        return
        [
            Counts("Shots", away.Shots, home.Shots),
            Counts("Shot attempts", Sum(away, skater => skater.ShotAttempts), Sum(home, skater => skater.ShotAttempts)),
            Amounts(
                "Expected goals",
                away.Skaters.Sum(skater => skater.ExpectedGoals),
                home.Skaters.Sum(skater => skater.ExpectedGoals),
                MatchDisplay.ExpectedGoals),
            new ComparisonRowViewModel(
                "5-on-5 xGF%",
                MatchDisplay.Percentage(awayFiveOnFiveShare),
                MatchDisplay.Percentage(1 - awayFiveOnFiveShare),
                awayFiveOnFiveShare ?? EvenShare),
            Counts("Faceoffs won", Sum(away, skater => skater.FaceoffsWon), Sum(home, skater => skater.FaceoffsWon)),
            Counts("Hits", Sum(away, skater => skater.Hits), Sum(home, skater => skater.Hits)),
            Counts("Blocked shots", Sum(away, skater => skater.BlockedShots), Sum(home, skater => skater.BlockedShots)),
            Counts("Giveaways", Sum(away, skater => skater.Giveaways), Sum(home, skater => skater.Giveaways)),
            Counts("Penalty minutes", away.PenaltyMinutes, home.PenaltyMinutes),
        ];

        static int Sum(CompletedMatchTeamSnapshot side, Func<SkaterBoxScoreSnapshot, int> figure) => side.Skaters.Sum(figure);

        static ComparisonRowViewModel Counts(string label, int awayValue, int homeValue) =>
            Amounts(label, awayValue, homeValue, value => Number((int)value));
    }

    /// <summary>
    /// A star's row: its place, the player, and what earned it: points and shots for a skater,
    /// saves for a goalie.
    /// </summary>
    public static StarRowViewModel Star(
        int place,
        PlayerGameScore star,
        CompletedMatchSnapshot result,
        PlayerSnapshot player,
        string teamInitials)
    {
        ArgumentNullException.ThrowIfNull(star);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(player);

        var side = result.Home.TeamId == star.TeamId ? result.Home : result.Away;
        string line;
        if (side.Goalie.PlayerId == star.PlayerId)
        {
            var goalie = side.Goalie;
            line = string.Create(
                CultureInfo.CurrentCulture,
                $"{goalie.Saves} saves on {goalie.ShotsAgainst} shots · {MatchDisplay.SavePercentage(goalie.Saves, goalie.ShotsAgainst)}");
        }
        else
        {
            var skater = side.Skaters.Single(skater => skater.PlayerId == star.PlayerId);
            line = string.Create(
                CultureInfo.CurrentCulture,
                $"{skater.Goals} G · {skater.Assists} A · {skater.Shots} SOG · {MatchDisplay.TimeOnIce(skater.TimeOnIce)} TOI");
        }

        return new StarRowViewModel(
            StarName(place),
            PlayerDisplay.FullName(player),
            PlayerDisplay.PositionAbbreviation(player.Position),
            teamInitials,
            line,
            star.Score.ToString("0.00", CultureInfo.CurrentCulture));
    }

    private static ComparisonRowViewModel Amounts(string label, double awayValue, double homeValue, Func<double, string> format) =>
        new(label, format(awayValue), format(homeValue), awayValue + homeValue == 0 ? EvenShare : awayValue / (awayValue + homeValue));

    private static string Number(int value) => value.ToString(CultureInfo.CurrentCulture);

    private static string StarName(int place) => place switch
    {
        1 => "1st star",
        2 => "2nd star",
        3 => "3rd star",
        _ => throw new ArgumentOutOfRangeException(nameof(place), place, "There are three stars."),
    };
}

/// <param name="Title">What the table counts, such as "SHOTS".</param>
/// <param name="Periods">The column headings: each period played, then any shootout.</param>
public sealed record PeriodTableViewModel(string Title, IReadOnlyList<string> Periods, IReadOnlyList<PeriodRowViewModel> Rows);

/// <param name="Team">The team's initials.</param>
/// <param name="Cells">One figure for each heading of the table.</param>
/// <param name="Total">The match total.</param>
public sealed record PeriodRowViewModel(string Team, IReadOnlyList<string> Cells, string Total);

/// <param name="AwayShare">The away team's share of the two figures, from zero to one; even when both are zero.</param>
public sealed record ComparisonRowViewModel(string Label, string AwayValue, string HomeValue, double AwayShare);

/// <param name="Star">The star's place, such as "1st star".</param>
/// <param name="Line">What the player did: points, shots, and ice time, or saves.</param>
/// <param name="GameScore">The game score that ranked the player, to two decimals.</param>
public sealed record StarRowViewModel(string Star, string Name, string Position, string Team, string Line, string GameScore);