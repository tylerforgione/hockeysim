using HockeySim.Domain;

using Xunit;

namespace HockeySim.Domain.Tests;

public sealed class SeasonHealthTests
{
    private static readonly DateOnly OpeningDay = new(2026, 10, 1);

    private readonly League _league = TestLeague.Create();
    private readonly Team _first;
    private readonly Team _second;
    private readonly ScheduledMatch _opening;
    private readonly ScheduledMatch _rematch;
    private readonly Season _season;

    public SeasonHealthTests()
    {
        (_first, _second) = (_league.Teams[0], _league.Teams[1]);
        _opening = new ScheduledMatch(OpeningDay, _first.Id, _second.Id);

        // The day after opening day has no matches.
        _rematch = new ScheduledMatch(OpeningDay.AddDays(2), _second.Id, _first.Id);
        _season = new Season(_league, new SeasonSchedule([_opening, _rematch]));
    }

    [Fact]
    public void EveryRosteredPlayerStartsTheSeasonHealthyWithNoWear()
    {
        Assert.All(_league.Teams.SelectMany(team => team.Roster), player =>
        {
            var health = _season.HealthOf(player.Id);
            Assert.Empty(health.Injuries);
            Assert.All(Enum.GetValues<BodyPart>(), part => Assert.Equal(0, health.Wear(part)));
        });
        Assert.Throws<ArgumentException>(() => _season.HealthOf(new PlayerId(Guid.NewGuid())));
    }

    [Fact]
    public void CompletingADayAppliesItsInjuriesAndWearToThePlayers()
    {
        var player = Skater(_first, 0);

        _season.CompleteDay([WithHealth(
            TestResults.Create(_league, _opening, 3, 2),
            [Injury(_first, player, InjuryType.SprainedKnee, 10)],
            [new WearGain(player, BodyPart.Knee, 4), new WearGain(player, BodyPart.Shoulder, 1)])]);

        var health = _season.HealthOf(player);
        Assert.Equal([new Injury(InjuryType.SprainedKnee, OpeningDay, 10)], health.Injuries);
        Assert.Equal(4, health.Wear(BodyPart.Knee));
        Assert.Equal(1, health.Wear(BodyPart.Shoulder));
        Assert.False(health.CanPlayOn(_season.CurrentDate));
    }

    [Fact]
    public void InjuriesHealOverLeagueDaysWithoutMatches()
    {
        var player = Skater(_first, 0);
        _season.CompleteDay([WithHealth(
            TestResults.Create(_league, _opening, 3, 2),
            [Injury(_first, player, InjuryType.BruisedFoot, 2)],
            [])]);

        Assert.NotEmpty(_season.HealthOf(player).InjuriesOn(_season.CurrentDate));
        _season.CompleteDay([]);

        Assert.Empty(_season.HealthOf(player).InjuriesOn(_season.CurrentDate));
    }

    [Fact]
    public void APlayerWhoCannotPlayCannotAppear()
    {
        var player = Skater(_first, 0);
        _season.CompleteDay([WithHealth(
            TestResults.Create(_league, _opening, 3, 2),
            [Injury(_first, player, InjuryType.SprainedKnee, 10)],
            [])]);
        _season.CompleteDay([]);

        Assert.Throws<ArgumentException>(() => _season.CompleteDay([TestResults.Create(_league, _rematch, 3, 2)]));
        Assert.Equal(OpeningDay.AddDays(2), _season.CurrentDate);
    }

    [Fact]
    public void APlayerPlayingThroughAnInjuryCanAppear()
    {
        var player = Skater(_first, 0);
        _season.CompleteDay([WithHealth(
            TestResults.Create(_league, _opening, 3, 2),
            [Injury(_first, player, InjuryType.SprainedAnkle, 12)],
            [])]);
        _season.CompleteDay([]);

        _season.CompleteDay([TestResults.Create(_league, _rematch, 3, 2)]);

        Assert.True(_season.IsRegularSeasonComplete);
    }

    [Fact]
    public void InjuriesThatWouldLeaveFewerThanEighteenAbleSkatersAreRejectedWithNothingApplied()
    {
        // The test rosters have twenty skaters, so a third skater out breaks the cap.
        var injuries = Enumerable.Range(0, 3)
            .Select(index => Injury(_first, Skater(_first, index), InjuryType.SprainedKnee, 10))
            .ToList();

        Assert.Throws<ArgumentException>(() => _season.CompleteDay([WithHealth(TestResults.Create(_league, _opening, 3, 2), injuries, [])]));
        Assert.Equal(OpeningDay, _season.CurrentDate);
        Assert.Empty(_season.HealthOf(Skater(_first, 0)).Injuries);

        _season.CompleteDay([WithHealth(TestResults.Create(_league, _opening, 3, 2), injuries.Take(2), [])]);
        Assert.Equal(2, _first.Roster.Count(player => !_season.HealthOf(player.Id).CanPlayOn(_season.CurrentDate)));
    }

    [Fact]
    public void InjuriesThatWouldLeaveFewerThanTwoAbleGoaliesAreRejected()
    {
        // The test rosters carry three goalies, so the starter can be lost but not a second one.
        var starter = _first.Lineup.StartingGoalie.Id;
        _season.CompleteDay([WithHealth(
            TestResults.Create(_league, _opening, 3, 2),
            [Injury(_first, starter, InjuryType.GroinStrain, 21)],
            [])]);
        _season.CompleteDay([]);

        var backupStarts = TestResults.Create(_league, _rematch, 3, 2);
        var firstSide = backupStarts.Away;
        var backup = _first.Lineup.BackupGoalie.Id;
        var withBackup = new CompletedMatchTeam(
            firstSide.TeamId,
            firstSide.Score,
            firstSide.Shots,
            firstSide.PowerPlayOpportunities,
            firstSide.Skaters,
            new GoalieBoxScore(backup, firstSide.Goalie.ShotsAgainst, firstSide.Goalie.GoalsAgainst, firstSide.Goalie.ExpectedGoalsAgainst, firstSide.Goalie.TimeOnIce),
            firstSide.ShotTotals);
        var rematch = TestResults.Match(_rematch, backupStarts.Home, withBackup);

        Assert.Throws<ArgumentException>(() => _season.CompleteDay([WithHealth(
            rematch,
            [Injury(_first, backup, InjuryType.GroinStrain, 21)],
            [])]));
        _season.CompleteDay([rematch]);
    }

    [Fact]
    public void OnlyAnAppearingPlayerOfTheTeamCanBeInjuredOrTakeWear()
    {
        var scratch = _first.Roster.First(player => !_first.Lineup.DressedPlayers.Contains(player) && player.Position != Position.Goalie).Id;
        var result = TestResults.Create(_league, _opening, 3, 2);

        Assert.Throws<ArgumentException>(() => WithHealth(result, [Injury(_first, scratch, InjuryType.BruisedFoot, 3)], []));
        Assert.Throws<ArgumentException>(() => WithHealth(result, [], [new WearGain(scratch, BodyPart.Foot, 1)]));
        Assert.Throws<ArgumentException>(() => WithHealth(result, [Injury(_second, Skater(_first, 0), InjuryType.BruisedFoot, 3)], []));
    }

    private static PlayerId Skater(Team team, int index) =>
        team.Lineup.ForwardLines.SelectMany(line => line.Players).ElementAt(index).Id;

    private static MatchInjury Injury(Team team, PlayerId player, InjuryType type, int days) =>
        new(1, TimeSpan.FromMinutes(5), team.Id, player, type, InjuryCause.Hit, days);

    private static CompletedMatch WithHealth(CompletedMatch match, IEnumerable<MatchInjury> injuries, IEnumerable<WearGain> wear) =>
        new(match.ScheduledMatch, match.Home, match.Away, match.Decision, match.Goals, match.Penalties, new MatchHealthChanges(injuries, wear));
}