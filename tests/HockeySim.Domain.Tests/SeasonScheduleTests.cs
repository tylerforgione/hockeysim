using HockeySim.Domain;

using Xunit;

namespace HockeySim.Domain.Tests;

public sealed class SeasonScheduleTests
{
    private static readonly DateOnly OpeningDay = new(2026, 10, 1);

    private static readonly TeamId First = new(Guid.NewGuid());
    private static readonly TeamId Second = new(Guid.NewGuid());
    private static readonly TeamId Third = new(Guid.NewGuid());
    private static readonly TeamId Fourth = new(Guid.NewGuid());

    [Fact]
    public void ScheduledMatchRejectsATeamPlayingItself()
    {
        Assert.Throws<ArgumentException>(() => new ScheduledMatch(OpeningDay, First, First));
    }

    [Fact]
    public void ScheduledMatchRejectsAnEmptyTeamIdentity()
    {
        Assert.Throws<ArgumentException>(() => new ScheduledMatch(OpeningDay, First, new TeamId(Guid.Empty)));
    }

    [Fact]
    public void ScheduleRejectsATeamPlayingTwiceOnOneDate()
    {
        var matches = new[]
        {
            new ScheduledMatch(OpeningDay, First, Second),
            new ScheduledMatch(OpeningDay, Third, First),
        };

        Assert.Throws<ArgumentException>(() => new SeasonSchedule(matches));
    }

    [Fact]
    public void ScheduleAllowsATeamToPlayOnDifferentDates()
    {
        var schedule = new SeasonSchedule(new[]
        {
            new ScheduledMatch(OpeningDay, First, Second),
            new ScheduledMatch(OpeningDay, Third, Fourth),
            new ScheduledMatch(OpeningDay.AddDays(1), Second, First),
        });

        Assert.Equal(3, schedule.Matches.Count);
    }

    [Fact]
    public void ScheduleOrdersMatchesChronologicallyAndKeepsSameDateOrder()
    {
        var late = new ScheduledMatch(OpeningDay.AddDays(2), First, Second);
        var earlyFirst = new ScheduledMatch(OpeningDay, Third, Fourth);
        var earlySecond = new ScheduledMatch(OpeningDay, First, Second);

        var schedule = new SeasonSchedule(new[] { late, earlyFirst, earlySecond });

        Assert.Equal(new[] { earlyFirst, earlySecond, late }, schedule.Matches);
    }

    [Fact]
    public void ScheduleMatchesCannotBeModified()
    {
        var schedule = new SeasonSchedule(new[] { new ScheduledMatch(OpeningDay, First, Second) });
        var matches = Assert.IsAssignableFrom<IList<ScheduledMatch>>(schedule.Matches);

        Assert.Throws<NotSupportedException>(() => matches.Clear());
    }
}