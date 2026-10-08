using HockeySim.Domain;
using HockeySim.Simulation.Events;

namespace HockeySim.Simulation.Tests;

/// <summary>
/// An independent statement of when a team may pull its goalie to tie a match, replayed over the
/// play-by-play: only in the third period, trailing by one goal with two minutes or less left, or
/// by two with three and a half minutes or less. Delayed penalties pull goalies too, at any time;
/// this rule says nothing about those.
/// </summary>
internal sealed class GoaliePullRule
{
    public const int OneGoalDownSeconds = 2 * 60;
    public const int TwoGoalsDownSeconds = (3 * 60) + 30;
    private const int PeriodSeconds = 20 * 60;

    private readonly MatchResult _result;
    private readonly Dictionary<MatchEvent, (int Home, int Away)> _scoresBefore = new(ReferenceEqualityComparer.Instance);

    public GoaliePullRule(MatchResult result)
    {
        _result = result;
        var (home, away) = (0, 0);
        foreach (var matchEvent in result.Events)
        {
            _scoresBefore[matchEvent] = (home, away);
            if (matchEvent is GoalEvent goal)
            {
                (home, away) = goal.TeamId == result.Home.TeamId ? (home + 1, away) : (home, away + 1);
            }
        }
    }

    /// <summary>How many goals the team trailed by just before the event; negative when leading.</summary>
    public int Deficit(MatchEvent matchEvent, TeamId teamId)
    {
        var (home, away) = _scoresBefore[matchEvent];
        return teamId == _result.Home.TeamId ? away - home : home - away;
    }

    public static int SecondsLeft(MatchEvent matchEvent) => PeriodSeconds - (int)matchEvent.TimeInPeriod.TotalSeconds;

    /// <summary>Whether the rule lets the team have its goalie pulled to tie the match at the event.</summary>
    public bool MayPull(MatchEvent matchEvent, TeamId teamId)
    {
        if (matchEvent.Period != MatchResult.RegulationPeriodCount)
        {
            return false;
        }

        return Deficit(matchEvent, teamId) switch
        {
            1 => SecondsLeft(matchEvent) <= OneGoalDownSeconds,
            2 => SecondsLeft(matchEvent) <= TwoGoalsDownSeconds,
            _ => false,
        };
    }

    public static bool IsPulled(MatchResult result, MatchEvent matchEvent, TeamId teamId) =>
        (teamId == result.Home.TeamId ? matchEvent.OnIce.HomeGoalie : matchEvent.OnIce.AwayGoalie) is null;
}