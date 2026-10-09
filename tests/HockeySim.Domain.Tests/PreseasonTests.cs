using Xunit;

namespace HockeySim.Domain.Tests;

public sealed class PreseasonTests
{
    private static readonly DateOnly OpeningDay = new(2026, 10, 1);
    private static readonly DateOnly FirstPreseasonDay = OpeningDay.AddDays(-3);

    private readonly League _league = TestLeague.Create();
    private readonly Team _first;
    private readonly Team _second;
    private readonly ScheduledMatch _firstExhibition;
    private readonly ScheduledMatch _lastExhibition;
    private readonly ScheduledMatch _opening;
    private readonly Season _season;

    public PreseasonTests()
    {
        (_first, _second) = (_league.Teams[0], _league.Teams[1]);
        _firstExhibition = new ScheduledMatch(FirstPreseasonDay, _first.Id, _second.Id);

        // The day after the first exhibition has no matches.
        _lastExhibition = new ScheduledMatch(OpeningDay.AddDays(-1), _second.Id, _first.Id);
        _opening = new ScheduledMatch(OpeningDay, _first.Id, _second.Id);
        _season = new Season(
            _league,
            new SeasonSchedule([_firstExhibition, _lastExhibition]),
            new SeasonSchedule([_opening]));
    }

    [Fact]
    public void ASeasonWithAPreseasonStartsOnItsFirstDay()
    {
        Assert.Equal(FirstPreseasonDay, _season.CurrentDate);
        Assert.Equal(OpeningDay, _season.OpeningDay);
        Assert.Equal(SeasonPhase.Preseason, _season.Phase);
        Assert.Equal([_firstExhibition], _season.CurrentDateMatches);
        Assert.Empty(_season.PreseasonMatches);
    }

    [Fact]
    public void ASeasonWithoutAPreseasonStartsOnOpeningDay()
    {
        var season = new Season(_league, new SeasonSchedule([_opening]));

        Assert.Equal(OpeningDay, season.CurrentDate);
        Assert.Equal(SeasonPhase.RegularSeason, season.Phase);
        Assert.Empty(season.PreseasonSchedule.Matches);
    }

    [Fact]
    public void PreseasonResultsAreKeptButCountTowardNothing()
    {
        var exhibition = TestResults.Create(_league, _firstExhibition, 5, 1);

        _season.CompleteDay([exhibition]);

        Assert.Equal([exhibition], _season.PreseasonMatches);
        Assert.Empty(_season.CompletedMatches);
        Assert.All(_season.TeamRecords, record => Assert.Equal(0, record.GamesPlayed));
        Assert.Empty(_season.SkaterStatistics);
        Assert.Empty(_season.GoalieStatistics);
        Assert.All(_season.RankStandings(_league.Teams.Select(team => team.Id)), entry => Assert.Equal(0, entry.Record.Points));
        Assert.Equal(FirstPreseasonDay.AddDays(1), _season.CurrentDate);
    }

    [Fact]
    public void AdvancingPastTheLastPreseasonDayLeadsToOpeningDay()
    {
        _season.CompleteDay([TestResults.Create(_league, _firstExhibition, 2, 1)]);
        _season.CompleteDay([]);

        Assert.Equal(SeasonPhase.Preseason, _season.Phase);
        Assert.Equal([_lastExhibition], _season.CurrentDateMatches);
        _season.CompleteDay([TestResults.Create(_league, _lastExhibition, 3, 4)]);

        Assert.Equal(OpeningDay, _season.CurrentDate);
        Assert.Equal(SeasonPhase.RegularSeason, _season.Phase);
        Assert.Equal([_opening], _season.CurrentDateMatches);
        Assert.Equal(2, _season.PreseasonMatches.Count);

        var opening = TestResults.Create(_league, _opening, 3, 2);
        _season.CompleteDay([opening]);

        Assert.Equal([opening], _season.CompletedMatches);
        Assert.Equal(2, _season.PreseasonMatches.Count);
        Assert.True(_season.IsComplete);
    }

    [Fact]
    public void APreseasonMatchCannotInjureOrWearAnyoneAndNothingIsApplied()
    {
        var exhibition = TestResults.Create(_league, _firstExhibition, 2, 1);
        var player = TestResults.FirstSkater(_first);
        var injured = WithHealth(
            exhibition,
            [new MatchInjury(1, TimeSpan.FromMinutes(5), _first.Id, player, InjuryType.BruisedFoot, InjuryCause.BlockedShot, 3)],
            []);
        var worn = WithHealth(exhibition, [], [new WearGain(player, BodyPart.Knee, 1)]);

        Assert.Throws<ArgumentException>(() => _season.CompleteDay([injured]));
        Assert.Throws<ArgumentException>(() => _season.CompleteDay([worn]));

        Assert.Equal(FirstPreseasonDay, _season.CurrentDate);
        Assert.Empty(_season.PreseasonMatches);
        Assert.Empty(_season.HealthOf(player).Injuries);
        Assert.Equal(0, _season.HealthOf(player).Wear(BodyPart.Knee));
    }

    [Fact]
    public void APreseasonMatchMustBeBeforeOpeningDayAndBetweenLeagueTeams()
    {
        var regularSeason = new SeasonSchedule([_opening]);

        Assert.Throws<ArgumentException>(() => new Season(
            _league, new SeasonSchedule([new ScheduledMatch(OpeningDay, _second.Id, _first.Id)]), regularSeason));
        Assert.Throws<ArgumentException>(() => new Season(
            _league, new SeasonSchedule([new ScheduledMatch(OpeningDay.AddDays(5), _second.Id, _first.Id)]), regularSeason));
        Assert.Throws<ArgumentException>(() => new Season(
            _league,
            new SeasonSchedule([new ScheduledMatch(FirstPreseasonDay, _first.Id, new TeamId(Guid.NewGuid()))]),
            regularSeason));
    }

    private static CompletedMatch WithHealth(CompletedMatch match, IEnumerable<MatchInjury> injuries, IEnumerable<WearGain> wear) =>
        new(match.ScheduledMatch, match.Home, match.Away, match.Decision, match.Goals, match.Penalties, new MatchHealthChanges(injuries, wear));
}