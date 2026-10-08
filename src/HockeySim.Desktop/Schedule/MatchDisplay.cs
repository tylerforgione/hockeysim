using System.Globalization;

using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Schedule;

/// <summary>
/// Formats dates, decisions, and results consistently across the shell, home page, and schedule.
/// </summary>
public static class MatchDisplay
{
    /// <summary>The dash shown for a value that is not yet defined, never a misleading zero.</summary>
    public const string Undefined = "—";

    public static string ShortDate(DateOnly date) => date.ToString("ddd, MMM d", CultureInfo.CurrentCulture);

    public static string LongDate(DateOnly date) => date.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture);

    /// <summary>
    /// The suffix that follows a final score, such as "OT"; empty for a regulation result.
    /// </summary>
    public static string DecisionSuffix(MatchDecision decision) => decision switch
    {
        MatchDecision.Regulation => string.Empty,
        MatchDecision.Overtime => "OT",
        MatchDecision.Shootout => "SO",
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown decision."),
    };

    public static string FinalLabel(MatchDecision decision) => decision switch
    {
        MatchDecision.Regulation => "Final",
        MatchDecision.Overtime => "Final · Overtime",
        MatchDecision.Shootout => "Final · Shootout",
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown decision."),
    };

    /// <summary>
    /// Describes a result from one team's side, such as "W 4–2" or "L 2–3 OT".
    /// </summary>
    public static string ResultFor(CompletedMatchSnapshot result, TeamId teamId)
    {
        var (team, opponent) = result.Home.TeamId == teamId ? (result.Home, result.Away) : (result.Away, result.Home);
        var outcome = result.WinnerId == teamId ? "W" : "L";
        var suffix = DecisionSuffix(result.Decision);
        var score = $"{outcome} {team.Score}–{opponent.Score}";
        return suffix.Length == 0 ? score : $"{score} {suffix}";
    }

    /// <summary>
    /// Formats a save percentage the way hockey tables do (".912"), or a dash before any shot so
    /// an undefined percentage is never shown as a number.
    /// </summary>
    public static string SavePercentage(int saves, int shotsAgainst) =>
        SavePercentage(shotsAgainst == 0 ? null : saves / (double)shotsAgainst);

    /// <summary>Formats a save percentage (".912"), or a dash while it is undefined.</summary>
    public static string SavePercentage(double? savePercentage) =>
        savePercentage is { } value ? value.ToString(".000", CultureInfo.CurrentCulture) : Undefined;

    /// <summary>
    /// Formats a share as a percentage to one decimal place without the sign ("52.3"), the way
    /// Corsi, faceoff, and special-teams percentages are shown, or a dash while it is undefined.
    /// </summary>
    public static string Percentage(double? share) =>
        share is { } value ? (value * 100).ToString("0.0", CultureInfo.CurrentCulture) : Undefined;

    /// <summary>Formats a goals-against average ("2.71"), or a dash before any time in net.</summary>
    public static string GoalsAgainstAverage(double? average) =>
        average is { } value ? value.ToString("0.00", CultureInfo.CurrentCulture) : Undefined;

    /// <summary>
    /// Formats goals saved above expected with its sign ("+3.42", "-1.10"), so a goalie above or
    /// below expectation is clear at a glance.
    /// </summary>
    public static string GoalsSavedAboveExpected(double goalsSavedAboveExpected)
    {
        // Round first so a tiny negative value is not shown as "-0.00".
        var rounded = Math.Round(goalsSavedAboveExpected, 2);
        return rounded > 0
            ? string.Create(CultureInfo.CurrentCulture, $"+{rounded:0.00}")
            : (rounded == 0 ? 0 : rounded).ToString("0.00", CultureInfo.CurrentCulture);
    }

    /// <summary>Formats an average time on ice, or a dash before a first appearance.</summary>
    public static string TimeOnIce(TimeSpan? time) => time is { } value ? TimeOnIce(value) : Undefined;

    /// <summary>Formats when something happened as period and clock, such as "2nd 14:05" or "OT 3:12".</summary>
    public static string PeriodTime(int period, TimeSpan timeInPeriod) =>
        $"{PeriodName(period)} {TimeOnIce(timeInPeriod)}";

    /// <summary>
    /// Names a period: "1st" to "3rd" in regulation, "OT" for the first overtime, then "2OT" and on
    /// for further playoff overtime periods.
    /// </summary>
    public static string PeriodName(int period) => period switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        4 => "OT",
        _ => string.Create(CultureInfo.CurrentCulture, $"{period - 3}OT"),
    };

    /// <summary>
    /// Formats time on ice as minutes and seconds ("17:42"). Minutes keep counting past an hour,
    /// as they do in long overtime.
    /// </summary>
    public static string TimeOnIce(TimeSpan time) =>
        string.Create(CultureInfo.CurrentCulture, $"{(int)time.TotalMinutes}:{time.Seconds:00}");

    /// <summary>Formats plus/minus with its sign, such as "+2", "0", or "-1".</summary>
    public static string PlusMinus(int plusMinus) =>
        plusMinus > 0
            ? string.Create(CultureInfo.CurrentCulture, $"+{plusMinus}")
            : plusMinus.ToString(CultureInfo.CurrentCulture);

    /// <summary>Formats an expected-goal total to two decimal places ("0.42").</summary>
    public static string ExpectedGoals(double expectedGoals) =>
        expectedGoals.ToString("0.00", CultureInfo.CurrentCulture);

    /// <summary>Formats a team's power play as goals of opportunities ("1/3").</summary>
    public static string PowerPlay(int goals, int opportunities) =>
        string.Create(CultureInfo.CurrentCulture, $"{goals}/{opportunities}");

    /// <summary>
    /// Formats faceoffs as won and lost ("8–5"), or a dash for a skater who took none.
    /// </summary>
    public static string Faceoffs(int won, int lost) =>
        won + lost == 0
            ? Undefined
            : string.Create(CultureInfo.CurrentCulture, $"{won}–{lost}");

    /// <summary>
    /// Marks a goal's situation: "PP", "SH", or "PS" (penalty shot), with "EN" for an empty net;
    /// empty for an even-strength goal with a goalie in net.
    /// </summary>
    public static string GoalSituationLabel(GoalSituation situation, bool isEmptyNet)
    {
        var label = situation switch
        {
            GoalSituation.EvenStrength => string.Empty,
            GoalSituation.PowerPlay => "PP",
            GoalSituation.Shorthanded => "SH",
            GoalSituation.PenaltyShot => "PS",
            _ => throw new ArgumentOutOfRangeException(nameof(situation), situation, "Unknown goal situation."),
        };
        return isEmptyNet ? $"{label} EN".TrimStart() : label;
    }

    public static string InfractionName(Infraction infraction) => infraction switch
    {
        Infraction.Hooking => "Hooking",
        Infraction.Tripping => "Tripping",
        Infraction.Holding => "Holding",
        Infraction.Interference => "Interference",
        Infraction.Slashing => "Slashing",
        Infraction.HighSticking => "High-sticking",
        Infraction.CrossChecking => "Cross-checking",
        Infraction.Roughing => "Roughing",
        Infraction.Boarding => "Boarding",
        Infraction.Charging => "Charging",
        Infraction.Elbowing => "Elbowing",
        Infraction.DelayOfGame => "Delay of game",
        Infraction.Fighting => "Fighting",
        _ => throw new ArgumentOutOfRangeException(nameof(infraction), infraction, "Unknown infraction."),
    };

    /// <summary>
    /// Describes a penalty's length and kind, such as "2 min", "5 min major", or "Penalty shot".
    /// </summary>
    public static string PenaltyDescription(PenaltyKind kind, int minutes) => kind switch
    {
        PenaltyKind.Minor => string.Create(CultureInfo.CurrentCulture, $"{minutes} min"),
        PenaltyKind.DoubleMinor => string.Create(CultureInfo.CurrentCulture, $"{minutes} min double minor"),
        PenaltyKind.Major => string.Create(CultureInfo.CurrentCulture, $"{minutes} min major"),
        PenaltyKind.Misconduct => string.Create(CultureInfo.CurrentCulture, $"{minutes} min misconduct"),
        PenaltyKind.GameMisconduct => "Game misconduct",
        PenaltyKind.PenaltyShot => "Penalty shot",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown penalty kind."),
    };
}