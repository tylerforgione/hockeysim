using HockeySim.Domain;

using Xunit;

using static HockeySim.Domain.MatchDecision;

namespace HockeySim.Domain.Tests;

/// <summary>
/// Ranks small groups of teams built from hand-written results. Each scenario equalizes the
/// criteria before the one under test and makes the later criteria favour the other order, so
/// the expected ranking holds only if that criterion decides it.
/// </summary>
public sealed class StandingsTests
{
    private static readonly DateOnly OpeningDay = new(2026, 10, 1);

    private readonly League _league = TestLeague.Create();
    private readonly List<(ScheduledMatch Match, int HomeScore, int AwayScore, MatchDecision Decision)> _games = [];
    private int _outsidersUsed;

    private Team A => _league.Teams[0];

    private Team B => _league.Teams[1];

    private Team C => _league.Teams[2];

    [Fact]
    public void BeforeAnyGameEveryTeamSharesFirstPlaceInLeagueOrder()
    {
        var season = new Season(_league, new SeasonSchedule([new ScheduledMatch(OpeningDay, A.Id, B.Id)]));

        var standings = season.RankStandings(_league.Teams.Select(team => team.Id).Reverse());

        Assert.Equal(_league.Teams.Select(team => team.Id), standings.Select(entry => entry.Record.TeamId));
        Assert.All(standings, entry => Assert.Equal(1, entry.Rank));
        Assert.All(standings, entry => Assert.Equal(0, entry.Record.GamesPlayed));
    }

    [Fact]
    public void MorePointsRankFirstEvenWithMoreGamesPlayed()
    {
        VsOutsider(A, 1, 0);
        VsOutsider(A, 1, 0);
        VsOutsider(B, 1, 0);

        AssertRanking(Play(), [B, A], expected: [A, B], ranks: [1, 2]);
    }

    [Fact]
    public void LevelOnPointsFewerGamesPlayedRanksFirst()
    {
        VsOutsider(A, 2, 1, Overtime);
        VsOutsider(B, 5, 0);
        VsOutsider(B, 0, 1);

        AssertRanking(Play(), [B, A], expected: [A, B], ranks: [1, 2]);
    }

    [Fact]
    public void ThenMoreRegulationWinsRankFirst()
    {
        VsOutsider(A, 1, 0);
        VsOutsider(A, 0, 5);
        VsOutsider(B, 2, 1, Overtime);
        VsOutsider(B, 0, 1);

        AssertRanking(Play(), [B, A], expected: [A, B], ranks: [1, 2]);
    }

    [Fact]
    public void ThenWinsExcludingShootoutsRankFirst()
    {
        VsOutsider(A, 2, 1, Overtime);
        VsOutsider(A, 0, 5);
        VsOutsider(B, 2, 1, Shootout);
        VsOutsider(B, 0, 1);

        AssertRanking(Play(), [B, A], expected: [A, B], ranks: [1, 2]);
    }

    [Fact]
    public void ThenMoreWinsRankFirst()
    {
        VsOutsider(A, 2, 1, Shootout);
        VsOutsider(A, 0, 5);
        VsOutsider(B, 1, 2, Overtime);
        VsOutsider(B, 1, 2, Shootout);

        AssertRanking(Play(), [B, A], expected: [A, B], ranks: [1, 2]);
    }

    [Fact]
    public void ThenTwoClubsAreSeparatedByPointsInGamesAgainstEachOther()
    {
        Game(A, B, 1, 0);
        Game(B, A, 0, 1);
        VsOutsider(A, 0, 9);
        VsOutsider(A, 0, 9);
        VsOutsider(B, 9, 0);
        VsOutsider(B, 9, 0);

        AssertRanking(Play(), [B, A], expected: [A, B], ranks: [1, 2]);
    }

    [Fact]
    public void AnOddGameExcludesTheFirstGameInTheCityThatHostedTheExtraGame()
    {
        // A hosts two of three meetings, so A's first home game (the second meeting) is excluded,
        // leaving B with both counted wins. Excluding any other meeting would leave them level.
        Game(B, A, 1, 0);
        Game(A, B, 1, 0);
        Game(A, B, 0, 1);
        VsOutsider(A, 9, 0);
        VsOutsider(B, 0, 1);

        AssertRanking(Play(), [A, B], expected: [B, A], ranks: [1, 2]);
    }

    [Fact]
    public void MoreThanTwoClubsAreSeparatedByShareOfAvailablePointsAmongThemselves()
    {
        // Unbalanced meetings: A played four games against the group, B and C two each. A and B
        // both earned 4 points, but B took all of its available points and A only half.
        Game(B, A, 2, 1);
        Game(A, B, 1, 2);
        Game(A, C, 9, 0);
        Game(C, A, 0, 9);
        VsOutsider(B, 0, 1);
        VsOutsider(B, 0, 1);
        VsOutsider(C, 1, 0);
        VsOutsider(C, 1, 0);

        AssertRanking(Play(), [A, B, C], expected: [B, A, C], ranks: [1, 2, 3]);
    }

    [Fact]
    public void ClubsThatBeatEachOtherInACycleAreLevelOnHeadToHeadAndGoToGoalDifferential()
    {
        Game(A, B, 1, 0);
        Game(B, A, 0, 1);
        Game(B, C, 1, 0);
        Game(C, B, 0, 1);
        Game(C, A, 5, 0);
        Game(A, C, 0, 5);

        AssertRanking(Play(), [A, B, C], expected: [C, B, A], ranks: [1, 2, 3]);
    }

    [Fact]
    public void ClubsLeftLevelAfterHeadToHeadSeparatesPartOfTheGroupGoToGoalDifferential()
    {
        // Among all three, A takes 8 of 8 points while B and C take 3 of 8 each. B beat C in
        // their own meetings, but the procedure continues to goal differential, where C leads.
        Game(A, B, 3, 1);
        Game(B, A, 1, 3);
        Game(A, C, 2, 1, Overtime);
        Game(C, A, 1, 2);
        Game(B, C, 2, 1);
        Game(C, B, 2, 1, Overtime);
        VsOutsider(A, 0, 1);
        VsOutsider(A, 0, 1);
        VsOutsider(A, 1, 2, Overtime);
        VsOutsider(B, 1, 0);
        VsOutsider(B, 1, 0);
        VsOutsider(B, 2, 1, Overtime);
        VsOutsider(C, 9, 0);
        VsOutsider(C, 9, 0);
        VsOutsider(C, 9, 0);

        AssertRanking(Play(), [A, B, C], expected: [A, C, B], ranks: [1, 2, 3]);
    }

    [Fact]
    public void HeadToHeadIsSkippedWhenATiedClubHasNotPlayedTheOthers()
    {
        Game(A, B, 1, 0);
        Game(B, A, 0, 1);
        VsOutsider(A, 0, 5);
        VsOutsider(A, 0, 5);
        VsOutsider(B, 9, 0);
        VsOutsider(B, 9, 0);
        VsOutsider(C, 1, 0);
        VsOutsider(C, 1, 0);
        VsOutsider(C, 0, 1);
        VsOutsider(C, 0, 1);

        AssertRanking(Play(), [A, B, C], expected: [B, C, A], ranks: [1, 2, 3]);
    }

    [Fact]
    public void ASingleMeetingIsAnOddGameAndLeavesTwoClubsLevelOnHeadToHead()
    {
        Game(A, B, 1, 0);
        VsOutsider(A, 0, 5);
        VsOutsider(B, 9, 0);

        AssertRanking(Play(), [A, B], expected: [B, A], ranks: [1, 2]);
    }

    [Fact]
    public void ThenGreaterGoalDifferentialRanksFirst()
    {
        VsOutsider(A, 2, 0);
        VsOutsider(B, 5, 4);

        AssertRanking(Play(), [B, A], expected: [A, B], ranks: [1, 2]);
    }

    [Fact]
    public void ThenMoreGoalsForRankFirst()
    {
        VsOutsider(A, 5, 4);
        VsOutsider(B, 2, 1);

        AssertRanking(Play(), [B, A], expected: [A, B], ranks: [1, 2]);
    }

    [Fact]
    public void ShootoutResultsCountAsOneGoalForTheWinnerAndAgainstTheLoser()
    {
        // Counting the deciding goals, both are level on differential and B has more goals for.
        // Counting player goals alone would put A ahead on differential.
        VsOutsider(A, 1, 0, Shootout);
        VsOutsider(A, 0, 1, Shootout);
        VsOutsider(B, 1, 0, Shootout);
        VsOutsider(B, 1, 2, Overtime);

        var season = Play();

        var a = season.TeamRecords.Single(record => record.TeamId == A.Id);
        Assert.Equal((1, 1, 1, 1), (a.ShootoutWins, a.ShootoutLosses, a.GoalsFor, a.GoalsAgainst));
        AssertRanking(season, [A, B], expected: [B, A], ranks: [1, 2]);
    }

    [Fact]
    public void TeamsLevelOnEveryCriterionShareARankInLeagueOrder()
    {
        VsOutsider(A, 1, 0);
        VsOutsider(B, 1, 0);
        VsOutsider(C, 0, 1);

        AssertRanking(Play(), [C, B, A], expected: [A, B, C], ranks: [1, 1, 3]);
    }

    [Fact]
    public void RankingATeamOutsideTheLeagueIsRejected()
    {
        var season = new Season(_league, new SeasonSchedule([new ScheduledMatch(OpeningDay, A.Id, B.Id)]));

        Assert.Throws<ArgumentException>(() => season.RankStandings([A.Id, new TeamId(Guid.NewGuid())]));
    }

    private static void AssertRanking(Season season, Team[] group, Team[] expected, int[] ranks)
    {
        var standings = season.RankStandings(group.Select(team => team.Id));

        Assert.Equal(expected.Select(team => team.Id), standings.Select(entry => entry.Record.TeamId));
        Assert.Equal(ranks, standings.Select(entry => entry.Rank));
    }

    /// <summary>
    /// Records a game on its own date. Scores are final, so a shootout winner's includes the
    /// deciding goal.
    /// </summary>
    private void Game(Team home, Team away, int homeScore, int awayScore, MatchDecision decision = Regulation)
    {
        var match = new ScheduledMatch(OpeningDay.AddDays(_games.Count), home.Id, away.Id);
        _games.Add((match, homeScore, awayScore, decision));
    }

    /// <summary>Records a home game against a team from the other conference.</summary>
    private void VsOutsider(Team team, int score, int opponentScore, MatchDecision decision = Regulation)
    {
        var outsider = _league.Teams[16 + (_outsidersUsed++ % 16)];
        Game(team, outsider, score, opponentScore, decision);
    }

    private Season Play()
    {
        var season = new Season(_league, new SeasonSchedule(_games.Select(game => game.Match)));
        foreach (var (match, homeScore, awayScore, decision) in _games)
        {
            season.CompleteDay([TestResults.FromFinalScore(_league, match, homeScore, awayScore, decision)]);
        }

        return season;
    }
}