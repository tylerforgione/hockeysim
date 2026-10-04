using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Management.Scheduling;

/// <summary>
/// Generates the regular season in two stages: <see cref="MeetingPlanner"/> decides who plays
/// whom and where, then a calendar assigns each meeting a date. Keeping venues independent of
/// dates lets a richer calendar replace <see cref="RoundCalendar"/> without changing the balance.
/// Authentic NHL dates, travel, rest, arena, and rotation constraints are out of scope.
/// </summary>
internal static class ScheduleGenerator
{
    // A simplified calendar input derived from the season year rather than the wall clock.
    private const int OpeningMonth = 10;
    private const int OpeningDayOfMonth = 1;

    public static SeasonSchedule Create(League league, ControlledRandom random)
    {
        ArgumentNullException.ThrowIfNull(league);
        ArgumentNullException.ThrowIfNull(random);

        var meetings = MeetingPlanner.Create(league, random);
        var openingDay = new DateOnly(league.SeasonYear, OpeningMonth, OpeningDayOfMonth);
        return new SeasonSchedule(RoundCalendar.Assign(league, meetings, openingDay, random));
    }
}