using HockeySim.Domain;

using Xunit;

using static HockeySim.Domain.MatchDecision;
using static HockeySim.Domain.PlayoffStatus;

namespace HockeySim.Domain.Tests;

/// <summary>
/// Wild-card qualification and clinch and elimination statuses on hand-built seasons. Teams
/// without scheduled games keep their record, so each scenario schedules only the games that
/// matter: results so far, then games still to play. East teams play the West, whose records
/// stay too low to matter unless a scenario says otherwise.
/// </summary>
public sealed class PlayoffRaceTests
{
    private static readonly DateOnly OpeningDay = new(2026, 10, 1);

    private readonly League _league = TestLeague.Create();
    private readonly List<(ScheduledMatch Match, int HomeScore, int AwayScore, MatchDecision Decision)> _played = [];
    private readonly List<(Team Home, Team Away)> _upcoming = [];
    private int _outsidersUsed;

    private IReadOnlyList<Team> Atlantic => _league.Conferences[0].Divisions[0].Teams;

    private IReadOnlyList<Team> Metro => _league.Conferences[0].Divisions[1].Teams;

    private IReadOnlyList<Team> West => [.. _league.Conferences[1].Divisions.SelectMany(division => division.Teams)];

    [Fact]
    public void QualifiersAreEachDivisionsTopThreeThenTheConferencesTwoBestOthers()
    {
        // Atlantic's fourth and fifth outscore Metro's second and third, so both wild cards go to
        // the Atlantic while Metro still sends its top three.
        int[] atlanticWins = [7, 6, 5, 4, 3];
        int[] metroWins = [3, 2, 1];
        foreach (var (team, wins) in Atlantic.Zip(atlanticWins).Concat(Metro.Zip(metroWins)))
        {
            Win(team, wins);
        }

        var standings = Play().RankWildCard(_league.Conferences[0]);

        Assert.Equal(
            [Atlantic.Take(3).Select(team => team.Id), Metro.Take(3).Select(team => team.Id)],
            standings.DivisionLeaders.Select(leaders => leaders.Teams.Select(entry => entry.Record.TeamId)));
        Assert.Equal(["Atlantic", "Metro"], standings.DivisionLeaders.Select(leaders => leaders.Division.Name));
        Assert.Equal([1, 2, 3], standings.DivisionLeaders[1].Teams.Select(entry => entry.Rank));

        var others = Atlantic.Skip(3).Concat(Metro.Skip(3)).ToList();
        Assert.Equal(others.Select(team => team.Id), standings.WildCardRace.Select(entry => entry.Record.TeamId));
        Assert.Equal([1, 2, 3, 3, 3, 3, 3, 3, 3, 3], standings.WildCardRace.Select(entry => entry.Rank));
        Assert.Equal(
            Atlantic.Take(3).Concat(Metro.Take(3)).Concat(Atlantic.Skip(3).Take(2)).Select(team => team.Id),
            standings.Qualifiers);
    }

    [Fact]
    public void RankingTheWildCardForAConferenceOutsideTheLeagueIsRejected()
    {
        Remaining(Atlantic[0], 1);

        Assert.Throws<ArgumentException>(() => Play().RankWildCard(TestLeague.Create().Conferences[0]));
    }

    [Fact]
    public void NoTeamIsDecidedWhileEveryTeamHasAGameLeft()
    {
        foreach (var team in Atlantic.Concat(Metro))
        {
            Remaining(team, 1);
        }

        Assert.All(Play().PlayoffStatuses().Values, status => Assert.Equal(Undecided, status));
    }

    [Theory]
    [InlineData(0, ClinchedBestRecord)]
    [InlineData(1, ClinchedConference)]
    [InlineData(2, ClinchedDivision)]
    [InlineData(3, ClinchedPlayoffSpot)]
    public void ALeaderClinchesEverythingNoRivalCanStillReach(int rivalsThatCanCatchUp, PlayoffStatus expected)
    {
        // The leader has 4 points and no games left. Each rival can still reach 4 points with the
        // same record, so only a later tie-breaker could separate them: first a West team, then a
        // Metro team, then two Atlantic teams, which still leaves the leader in the top three.
        Win(Atlantic[0], 2);
        Team[][] rivals = [[West[^1], West[^2]], [Metro[0], Metro[1]], [Atlantic[1], Atlantic[2]]];
        foreach (var pair in rivals.Take(rivalsThatCanCatchUp))
        {
            Upcoming(pair[0], pair[1]);
            Upcoming(pair[1], pair[0]);
        }

        // With nothing left to catch up, the season would be complete; one unrelated game keeps it open.
        Remaining(Atlantic[7], 1);

        Assert.Equal(expected, Play().PlayoffStatuses()[Atlantic[0].Id]);
    }

    [Theory]
    [InlineData(Overtime, ClinchedDivision)]
    [InlineData(Regulation, ClinchedPlayoffSpot)]
    public void ARivalThatCanOnlyDrawLevelOnPointsLosesOnRegulationWins(MatchDecision rivalWin, PlayoffStatus expected)
    {
        // The leader finishes on 4 points with two regulation wins. The rival can reach 4 points
        // with the same games played. After an overtime win it can have one regulation win at
        // most, so the leader stays ahead; after a regulation win it could equal every per-team
        // criterion, leaving head-to-head and goals to decide, so the division is not yet certain.
        Win(Atlantic[0], 2);
        Win(Atlantic[1], 1, rivalWin);
        Remaining(Atlantic[1], 1);
        KeepTheConferenceAndLeagueOpen();

        Assert.Equal(expected, Play().PlayoffStatuses()[Atlantic[0].Id]);
    }

    [Theory]
    [InlineData(4, ClinchedPlayoffSpot)]
    [InlineData(5, Undecided)]
    public void ATeamOutsideItsDivisionsTopThreeClinchesAWildCardOnceAtMostOneContenderCanPassIt(
        int metroTeamsThatCanCatchUp,
        PlayoffStatus expected)
    {
        // Three Atlantic teams are out of reach, so the fourth needs a wild card. Metro sends three
        // teams through its division, so its fourth could pass for one wild card, and only a fifth
        // could take the other.
        var team = Atlantic[3];
        foreach (var leader in Atlantic.Take(3))
        {
            Win(leader, 3);
        }

        Win(team, 2);
        foreach (var rival in Metro.Take(metroTeamsThatCanCatchUp))
        {
            Remaining(rival, 2);
        }

        Assert.Equal(expected, Play().PlayoffStatuses()[team.Id]);
    }

    [Theory]
    [InlineData(3, 5, Eliminated)]
    [InlineData(4, 4, Eliminated)]
    [InlineData(3, 4, Undecided)]
    [InlineData(2, 7, Undecided)]
    public void ATeamIsEliminatedOnceThreeDivisionRivalsAndTwoWildCardContendersAreOutOfReach(
        int atlanticTeamsAhead,
        int metroTeamsAhead,
        PlayoffStatus expected)
    {
        // Teams ahead beyond each division's three leaders are wild-card contenders. With only two
        // Atlantic teams ahead, the team can still finish third in its division.
        var team = Atlantic[7];
        Remaining(team, 1);
        foreach (var rival in Atlantic.Take(atlanticTeamsAhead).Concat(Metro.Take(metroTeamsAhead)))
        {
            Win(rival, 2);
        }

        Assert.Equal(expected, Play().PlayoffStatuses()[team.Id]);
    }

    [Fact]
    public void ALateLossEliminatesATeamThatCouldOnlyDrawLevel()
    {
        // Eight teams finish on 2 points with one regulation win. Winning its last game in
        // regulation would bring the team level with them on every per-team criterion, so it is
        // still alive; losing it leaves it out of reach.
        var team = Atlantic[7];
        foreach (var rival in Atlantic.Take(3).Concat(Metro.Take(5)))
        {
            Win(rival, 1);
        }

        Remaining(team, 1);
        Upcoming(West[^1], West[^2]);
        var season = Play();
        Assert.Equal(Undecided, season.PlayoffStatuses()[team.Id]);

        var finalGame = Assert.Single(season.CurrentDateMatches);
        season.CompleteDay([TestResults.FromFinalScore(_league, finalGame, 0, 1, Regulation)]);

        Assert.False(season.IsComplete);
        Assert.Equal(Eliminated, season.PlayoffStatuses()[team.Id]);
    }

    [Fact]
    public void OnceTheSeasonIsCompleteTheFinalStandingsDecideEveryTeamIncludingHeadToHead()
    {
        // Metro's fourth and fifth finish level on every per-team criterion for the last wild
        // card. The fifth has the better goal differential, but the fourth won both meetings.
        foreach (var team in Atlantic.Take(4).Concat(Metro.Take(3)))
        {
            Win(team, 3);
        }

        var (fourth, fifth) = (Metro[3], Metro[4]);
        Game(fourth, fifth, 1, 0);
        Game(fifth, fourth, 0, 1);
        Lose(fourth, 2);
        Win(fifth, 2, score: 5);

        var season = Play();
        var statuses = season.PlayoffStatuses();

        Assert.True(season.IsComplete);
        Assert.Equal(ClinchedBestRecord, statuses[Atlantic[0].Id]);
        Assert.Equal(ClinchedDivision, statuses[Metro[0].Id]);
        Assert.Equal(ClinchedPlayoffSpot, statuses[Atlantic[3].Id]);
        Assert.Equal(ClinchedPlayoffSpot, statuses[fourth.Id]);
        Assert.Equal(Eliminated, statuses[fifth.Id]);
        Assert.DoesNotContain(Undecided, statuses.Values);
        Assert.Equal(16, statuses.Values.Count(status => status >= ClinchedPlayoffSpot));
        Assert.Equal(
            _league.Conferences.SelectMany(conference => season.RankWildCard(conference).Qualifiers).OrderBy(id => id.Value),
            statuses.Where(status => status.Value >= ClinchedPlayoffSpot).Select(status => status.Key).OrderBy(id => id.Value));

        // One best record, one conference winner per conference, one division winner per division.
        Assert.Single(statuses.Values, status => status == ClinchedBestRecord);
        Assert.All(_league.Conferences, conference => Assert.Single(
            conference.Divisions.SelectMany(division => division.Teams),
            team => statuses[team.Id] >= ClinchedConference));
        Assert.All(_league.Conferences.SelectMany(conference => conference.Divisions), division => Assert.Single(
            division.Teams,
            team => statuses[team.Id] >= ClinchedDivision));
    }

    /// <summary>A West team and a Metro team can each still reach 4 points.</summary>
    private void KeepTheConferenceAndLeagueOpen()
    {
        foreach (var (first, second) in new[] { (West[^1], West[^2]), (Metro[0], Metro[1]) })
        {
            Upcoming(first, second);
            Upcoming(second, first);
        }
    }

    /// <summary>Records a completed game on its own date.</summary>
    private void Game(Team home, Team away, int homeScore, int awayScore, MatchDecision decision = Regulation)
    {
        var match = new ScheduledMatch(OpeningDay.AddDays(_played.Count), home.Id, away.Id);
        _played.Add((match, homeScore, awayScore, decision));
    }

    /// <summary>Records home wins over West teams, a shootout's score including the deciding goal.</summary>
    private void Win(Team team, int times, MatchDecision decision = Regulation, int score = 1)
    {
        for (var game = 0; game < times; game++)
        {
            Game(team, NextOutsider(), decision == Regulation ? score : score + 1, decision == Regulation ? 0 : score, decision);
        }
    }

    /// <summary>Records home regulation losses to West teams.</summary>
    private void Lose(Team team, int times)
    {
        for (var game = 0; game < times; game++)
        {
            Game(team, NextOutsider(), 0, 1);
        }
    }

    /// <summary>Schedules home games against West teams after every completed game.</summary>
    private void Remaining(Team team, int games)
    {
        for (var game = 0; game < games; game++)
        {
            Upcoming(team, NextOutsider());
        }
    }

    private void Upcoming(Team home, Team away) => _upcoming.Add((home, away));

    private Team NextOutsider() => West[_outsidersUsed++ % West.Count];

    private Season Play()
    {
        var upcoming = _upcoming.Select((game, index) =>
            new ScheduledMatch(OpeningDay.AddDays(_played.Count + index), game.Home.Id, game.Away.Id));
        var season = new Season(_league, new SeasonSchedule(_played.Select(game => game.Match).Concat(upcoming)));
        foreach (var (match, homeScore, awayScore, decision) in _played)
        {
            season.CompleteDay([TestResults.FromFinalScore(_league, match, homeScore, awayScore, decision)]);
        }

        return season;
    }
}