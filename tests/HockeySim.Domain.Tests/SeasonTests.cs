using HockeySim.Domain;

using Xunit;

namespace HockeySim.Domain.Tests;

public sealed class SeasonTests
{
    private static readonly DateOnly OpeningDay = new(2026, 10, 1);

    private readonly League _league = TestLeague.Create();
    private readonly Team _first;
    private readonly Team _second;
    private readonly Team _third;
    private readonly Team _fourth;
    private readonly ScheduledMatch _openingFirst;
    private readonly ScheduledMatch _openingSecond;
    private readonly ScheduledMatch _finalMatch;
    private readonly Season _season;

    public SeasonTests()
    {
        (_first, _second, _third, _fourth) = (_league.Teams[0], _league.Teams[1], _league.Teams[2], _league.Teams[3]);
        _openingFirst = new ScheduledMatch(OpeningDay, _first.Id, _second.Id);
        _openingSecond = new ScheduledMatch(OpeningDay, _third.Id, _fourth.Id);

        // The day after opening day has no matches.
        _finalMatch = new ScheduledMatch(OpeningDay.AddDays(2), _second.Id, _first.Id);
        _season = new Season(_league, new SeasonSchedule([_openingFirst, _openingSecond, _finalMatch]));
    }

    [Fact]
    public void ANewSeasonStartsOnOpeningDayWithNothingPlayed()
    {
        Assert.Equal(OpeningDay, _season.CurrentDate);
        Assert.False(_season.IsComplete);
        Assert.Empty(_season.CompletedMatches);
        Assert.Empty(_season.SkaterStatistics);
        Assert.Empty(_season.GoalieStatistics);
        Assert.Equal(_league.Teams.Select(team => team.Id), _season.TeamRecords.Select(record => record.TeamId));
        Assert.All(_season.TeamRecords, record => Assert.Equal(0, record.GamesPlayed));
        Assert.Equal([_openingFirst, _openingSecond], _season.CurrentDateMatches);
    }

    [Fact]
    public void CompletingADayRecordsEveryResultInScheduleOrderAndMovesToTheNextDay()
    {
        var second = Result(_openingSecond, homeGoals: 1, awayGoals: 4);
        var first = Result(_openingFirst, homeGoals: 3, awayGoals: 2);

        _season.CompleteDay([second, first]);

        Assert.Equal([first, second], _season.CompletedMatches);
        Assert.Equal(OpeningDay.AddDays(1), _season.CurrentDate);
        Assert.Equal(4, _season.TeamRecords.Count(record => record.GamesPlayed == 1));
        Assert.Equal(4 * 18, _season.SkaterStatistics.Count);
        Assert.Equal(4, _season.GoalieStatistics.Count);
    }

    [Fact]
    public void ADayWithoutMatchesOnlyMovesTheDate()
    {
        PlayOpeningDay();

        Assert.Empty(_season.CurrentDateMatches);
        _season.CompleteDay([]);

        Assert.Equal(OpeningDay.AddDays(2), _season.CurrentDate);
        Assert.Equal(2, _season.CompletedMatches.Count);
    }

    [Fact]
    public void APartialDayIsRejectedWithoutChangingTheSeason()
    {
        var error = Record.Exception(() => _season.CompleteDay([Result(_openingFirst, 3, 2)]));

        Assert.IsType<ArgumentException>(error);
        AssertNothingPlayed();
    }

    [Fact]
    public void AResultForAnotherDayIsRejected()
    {
        var error = Record.Exception(() => _season.CompleteDay(
            [Result(_openingFirst, 3, 2), Result(_finalMatch, 1, 0)]));

        Assert.IsType<ArgumentException>(error);
        AssertNothingPlayed();
    }

    [Fact]
    public void TheSameMatchCannotBeResolvedTwiceInOneDay()
    {
        var error = Record.Exception(() => _season.CompleteDay(
            [Result(_openingFirst, 3, 2), Result(_openingFirst, 1, 0)]));

        Assert.IsType<ArgumentException>(error);
        AssertNothingPlayed();
    }

    [Fact]
    public void AnAlreadyCompletedMatchCannotBeAppliedAgain()
    {
        var first = Result(_openingFirst, 3, 2);
        _season.CompleteDay([first, Result(_openingSecond, 1, 0)]);

        Assert.Throws<ArgumentException>(() => _season.CompleteDay([first]));
        Assert.Equal(2, _season.CompletedMatches.Count);
        Assert.Equal(OpeningDay.AddDays(1), _season.CurrentDate);
    }

    [Fact]
    public void AResultNamingAPlayerFromAnotherRosterIsRejectedWithoutChangingTheSeason()
    {
        var valid = Result(_openingFirst, 1, 0);
        var outsider = _third.Lineup.ForwardLines[0].Centre.Id;
        var skaters = valid.Home.Skaters.Skip(1).Prepend(new SkaterBoxScore(outsider, 1, 0));
        var home = new CompletedMatchTeam(_first.Id, 1, valid.Home.Shots, skaters, valid.Home.Goalie);
        var invalid = new CompletedMatch(_openingFirst, home, valid.Away, MatchDecision.Regulation);

        var error = Record.Exception(() => _season.CompleteDay([invalid, Result(_openingSecond, 1, 0)]));

        Assert.IsType<ArgumentException>(error);
        AssertNothingPlayed();
    }

    [Fact]
    public void AResultCreditingAGoalieAsASkaterIsRejected()
    {
        var valid = Result(_openingFirst, 1, 0);
        var backup = new SkaterBoxScore(_first.Lineup.BackupGoalie.Id, 0, 0);
        var home = new CompletedMatchTeam(
            _first.Id, 1, valid.Home.Shots, valid.Home.Skaters.Append(backup), valid.Home.Goalie);
        var invalid = new CompletedMatch(_openingFirst, home, valid.Away, MatchDecision.Regulation);

        Assert.Throws<ArgumentException>(() => _season.CompleteDay([invalid, Result(_openingSecond, 1, 0)]));
        AssertNothingPlayed();
    }

    [Theory]
    [InlineData(MatchDecision.Regulation, 2, 0)]
    [InlineData(MatchDecision.Overtime, 2, 1)]
    [InlineData(MatchDecision.Shootout, 2, 1)]
    public void StandingsPointsDependOnHowTheMatchWasDecided(MatchDecision decision, int winnerPoints, int loserPoints)
    {
        var result = decision switch
        {
            MatchDecision.Regulation => Result(_openingFirst, 4, 1),
            MatchDecision.Overtime => Result(_openingFirst, 3, 2, decision),
            _ => Result(_openingFirst, 2, 2, decision, shootoutWinnerIsHome: true),
        };

        _season.CompleteDay([result, Result(_openingSecond, 1, 0)]);

        var winner = RecordOf(_first);
        var loser = RecordOf(_second);
        Assert.Equal(winnerPoints, winner.Points);
        Assert.Equal(loserPoints, loser.Points);
        Assert.Equal(1, winner.Wins);
        Assert.Equal(decision == MatchDecision.Regulation ? 1 : 0, loser.RegulationLosses);
        Assert.Equal(decision == MatchDecision.Overtime ? 1 : 0, loser.OvertimeLosses);
        Assert.Equal(decision == MatchDecision.Shootout ? 1 : 0, loser.ShootoutLosses);
    }

    [Fact]
    public void AShootoutDecidingGoalCountsForTheTeamButNoPlayer()
    {
        var shootout = Result(_openingFirst, 2, 2, MatchDecision.Shootout, shootoutWinnerIsHome: false);

        _season.CompleteDay([shootout, Result(_openingSecond, 1, 0)]);

        var winner = RecordOf(_second);
        Assert.Equal(3, winner.GoalsFor);
        Assert.Equal(2, winner.GoalsAgainst);
        Assert.Equal(3, RecordOf(_first).GoalsAgainst);
        Assert.Equal(2, SkaterGoals(_second));
        Assert.Equal(2, _season.GoalieStatistics.Single(goalie => goalie.TeamId == _first.Id).GoalsAgainst);
    }

    [Fact]
    public void SeasonTotalsAccumulateAcrossMatches()
    {
        PlayOpeningDay();
        _season.CompleteDay([]);
        _season.CompleteDay([Result(_finalMatch, 0, 5)]);

        var first = RecordOf(_first);
        Assert.Equal(2, first.GamesPlayed);
        Assert.Equal(2, first.RegulationWins);
        Assert.Equal(4, first.Points);
        Assert.Equal(8, first.GoalsFor);

        var scorer = _season.SkaterStatistics.Single(skater => skater.PlayerId == FirstSkater(_first));
        Assert.Equal(2, scorer.GamesPlayed);
        Assert.Equal(8, scorer.Goals);

        var goalie = _season.GoalieStatistics.Single(goalie => goalie.PlayerId == _first.Lineup.StartingGoalie.Id);
        Assert.Equal(2, goalie.GamesPlayed);
        Assert.Equal(1, goalie.GoalsAgainst);
        Assert.Equal(goalie.ShotsAgainst - 1, goalie.Saves);
    }

    [Fact]
    public void AfterTheFinalMatchTheSeasonIsCompleteAndRejectsFurtherDays()
    {
        PlayOpeningDay();
        _season.CompleteDay([]);
        _season.CompleteDay([Result(_finalMatch, 2, 1)]);

        Assert.True(_season.IsComplete);
        Assert.Equal(OpeningDay.AddDays(3), _season.CurrentDate);
        Assert.Throws<InvalidOperationException>(() => _season.CompleteDay([]));
        Assert.Equal(OpeningDay.AddDays(3), _season.CurrentDate);
    }

    [Fact]
    public void SeasonCollectionsCannotBeModified()
    {
        PlayOpeningDay();

        Assert.Throws<NotSupportedException>(() => ((IList<CompletedMatch>)_season.CompletedMatches).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<TeamRecord>)_season.TeamRecords).Clear());
    }

    [Fact]
    public void ASeasonRejectsAnEmptyScheduleOrTeamsOutsideTheLeague()
    {
        var stranger = new TeamId(Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => new Season(_league, new SeasonSchedule([])));
        Assert.Throws<ArgumentException>(() => new Season(
            _league, new SeasonSchedule([new ScheduledMatch(OpeningDay, _first.Id, stranger)])));
    }

    [Fact]
    public void ACompletedMatchMustBeDecisive()
    {
        Assert.Throws<ArgumentException>(() => Result(_openingFirst, 2, 2));
    }

    [Fact]
    public void AMatchDecidedAfterRegulationMustBeWonByOneGoal()
    {
        var regulation = Result(_openingFirst, 3, 1);

        Assert.Throws<ArgumentException>(() => new CompletedMatch(
            _openingFirst, regulation.Home, regulation.Away, MatchDecision.Overtime));
    }

    [Fact]
    public void OnlyAShootoutWinnerMayScoreMoreThanItsPlayers()
    {
        var shootout = Result(_openingFirst, 1, 1, MatchDecision.Shootout, shootoutWinnerIsHome: true);

        Assert.Throws<ArgumentException>(() => new CompletedMatch(
            _openingFirst, shootout.Home, shootout.Away, MatchDecision.Overtime));
    }

    [Fact]
    public void ACompletedMatchMustBeBetweenTheScheduledTeams()
    {
        var reversed = Result(_openingFirst, 2, 1);

        Assert.Throws<ArgumentException>(() => new CompletedMatch(
            _openingFirst, reversed.Away, reversed.Home, MatchDecision.Regulation));
    }

    [Fact]
    public void EachGoalieMustFaceTheOpponentsShotsAndGoals()
    {
        var valid = Result(_openingFirst, 2, 1);
        var goalie = new GoalieBoxScore(valid.Home.Goalie.PlayerId, valid.Away.Shots + 1, valid.Home.Goalie.GoalsAgainst);
        var home = new CompletedMatchTeam(_first.Id, 2, valid.Home.Shots, valid.Home.Skaters, goalie);

        Assert.Throws<ArgumentException>(() => new CompletedMatch(
            _openingFirst, home, valid.Away, MatchDecision.Regulation));
    }

    [Fact]
    public void ATeamCannotRecordMoreThanTwoAssistsPerGoal()
    {
        var skaters = new[]
        {
            new SkaterBoxScore(FirstSkater(_first), 1, 0),
            new SkaterBoxScore(_first.Lineup.ForwardLines[0].Centre.Id, 0, 2),
            new SkaterBoxScore(_first.Lineup.ForwardLines[0].RightWing.Id, 0, 1),
        };

        Assert.Throws<ArgumentException>(() => new CompletedMatchTeam(
            _first.Id, 1, 10, skaters, new GoalieBoxScore(_first.Lineup.StartingGoalie.Id, 10, 0)));
    }

    private void PlayOpeningDay() =>
        _season.CompleteDay([Result(_openingFirst, 3, 1), Result(_openingSecond, 2, 1)]);

    private void AssertNothingPlayed()
    {
        Assert.Equal(OpeningDay, _season.CurrentDate);
        Assert.Empty(_season.CompletedMatches);
        Assert.Empty(_season.SkaterStatistics);
        Assert.All(_season.TeamRecords, record => Assert.Equal(0, record.GamesPlayed));
    }

    private TeamRecord RecordOf(Team team) => _season.TeamRecords.Single(record => record.TeamId == team.Id);

    private int SkaterGoals(Team team) =>
        _season.SkaterStatistics.Where(skater => skater.TeamId == team.Id).Sum(skater => skater.Goals);

    private static PlayerId FirstSkater(Team team) => team.Lineup.ForwardLines[0].LeftWing.Id;

    /// <summary>
    /// Builds a consistent result in which each side's first forward scores every player goal.
    /// A shootout adds the deciding goal to the winner's score.
    /// </summary>
    private CompletedMatch Result(
        ScheduledMatch scheduled,
        int homeGoals,
        int awayGoals,
        MatchDecision decision = MatchDecision.Regulation,
        bool shootoutWinnerIsHome = true)
    {
        const int shots = 30;
        var home = _league.Teams.Single(team => team.Id == scheduled.HomeTeamId);
        var away = _league.Teams.Single(team => team.Id == scheduled.AwayTeamId);
        var homeBonus = decision == MatchDecision.Shootout && shootoutWinnerIsHome ? 1 : 0;
        var awayBonus = decision == MatchDecision.Shootout && !shootoutWinnerIsHome ? 1 : 0;

        return new CompletedMatch(
            scheduled,
            Side(home, homeGoals + homeBonus, homeGoals, opponentGoals: awayGoals),
            Side(away, awayGoals + awayBonus, awayGoals, opponentGoals: homeGoals),
            decision);

        CompletedMatchTeam Side(Team team, int score, int playerGoals, int opponentGoals)
        {
            var skaters = team.Lineup.ForwardLines.SelectMany(line => line.Players)
                .Concat(team.Lineup.DefencePairs.SelectMany(pair => pair.Players))
                .Select(player => new SkaterBoxScore(player.Id, player.Id == FirstSkater(team) ? playerGoals : 0, 0));
            var goalie = new GoalieBoxScore(team.Lineup.StartingGoalie.Id, shots, opponentGoals);
            return new CompletedMatchTeam(team.Id, score, shots, skaters, goalie);
        }
    }
}