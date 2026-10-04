using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.NewGame;
using HockeySim.Simulation.Randomness;

using Xunit;

namespace HockeySim.Management.Tests;

public sealed class ScheduleTests
{
    private const string ManagedTeamName = "Halifax Mariners";

    public static TheoryData<ulong> Seeds => new() { 1, 84, 1344, 987654321, ulong.MaxValue };

    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryTeamPlaysEachOpponentTheRequiredNumberOfTimes(ulong seed)
    {
        var snapshot = StartGame(seed);
        var meetings = snapshot.Schedule.Matches
            .GroupBy(match => PairKey(match.HomeTeamId, match.AwayTeamId))
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal(1344, snapshot.Schedule.Matches.Count);
        foreach (var (team, opponent) in DistinctPairs(snapshot.League))
        {
            Assert.Equal(
                ExpectedMeetings(snapshot.League, team, opponent),
                meetings.GetValueOrDefault(PairKey(team, opponent)));
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryTeamPlays42HomeAnd42AwayMatches(ulong seed)
    {
        var snapshot = StartGame(seed);
        var matches = snapshot.Schedule.Matches;

        Assert.All(snapshot.League.Teams, team =>
        {
            Assert.Equal(42, matches.Count(match => match.HomeTeamId == team.Id));
            Assert.Equal(42, matches.Count(match => match.AwayTeamId == team.Id));
        });
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void VenuesAreBalancedWithinEachPairOfOpponents(ulong seed)
    {
        var snapshot = StartGame(seed);
        var matches = snapshot.Schedule.Matches;

        foreach (var (team, opponent) in DistinctPairs(snapshot.League))
        {
            var meetings = ExpectedMeetings(snapshot.League, team, opponent);
            var teamHosts = matches.Count(match => match.HomeTeamId == team && match.AwayTeamId == opponent);
            var opponentHosts = matches.Count(match => match.HomeTeamId == opponent && match.AwayTeamId == team);

            // A three-meeting pair cannot split evenly; one side hosts twice.
            Assert.True(
                Math.Abs(teamHosts - opponentHosts) == meetings % 2,
                $"{teamHosts} home and {opponentHosts} away in {meetings} meetings.");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void EachTeamHostsTwiceAgainstHalfItsCrossDivisionConferenceOpponents(ulong seed)
    {
        var snapshot = StartGame(seed);
        var matches = snapshot.Schedule.Matches;

        Assert.All(snapshot.League.Teams, team =>
        {
            var crossDivisionOpponents = snapshot.League.Teams
                .Where(opponent => ExpectedMeetings(snapshot.League, team.Id, opponent.Id) == 3)
                .ToList();

            Assert.Equal(8, crossDivisionOpponents.Count);
            Assert.Equal(4, crossDivisionOpponents.Count(opponent =>
                matches.Count(match => match.HomeTeamId == team.Id && match.AwayTeamId == opponent.Id) == 2));
        });
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void ScheduleUsesValidTeamsWithoutSelfMatchesOrSameDayConflicts(ulong seed)
    {
        var snapshot = StartGame(seed);
        var teamIds = snapshot.League.Teams.Select(team => team.Id).ToHashSet();

        Assert.All(snapshot.Schedule.Matches, match =>
        {
            Assert.Contains(match.HomeTeamId, teamIds);
            Assert.Contains(match.AwayTeamId, teamIds);
            Assert.NotEqual(match.HomeTeamId, match.AwayTeamId);
        });
        Assert.All(snapshot.Schedule.Matches.GroupBy(match => match.Date), day =>
        {
            var teamsPlaying = day.SelectMany(match => new[] { match.HomeTeamId, match.AwayTeamId }).ToList();
            Assert.Equal(teamsPlaying.Count, teamsPlaying.Distinct().Count());
        });
    }

    [Fact]
    public void CalendarStartsOnOctoberFirstWithEveryTeamPlayingEveryOtherDay()
    {
        var snapshot = StartGame(2026);
        var matches = snapshot.Schedule.Matches;
        var dates = matches.Select(match => match.Date).Distinct().ToList();

        Assert.Equal(matches.OrderBy(match => match.Date).Select(match => match.Date), matches.Select(match => match.Date));
        Assert.Equal(84, dates.Count);
        Assert.Equal(
            Enumerable.Range(0, 84).Select(round => new DateOnly(2026, 10, 1).AddDays(round * 2)),
            dates);
        Assert.All(matches.GroupBy(match => match.Date), day => Assert.Equal(16, day.Count()));
    }

    [Fact]
    public void CalendarFollowsTheSeasonYear()
    {
        var snapshot = new GameManager().StartNewGame(
            new NewGameCommand(2031, new RandomState(5), ManagedTeamName));

        Assert.Equal(new DateOnly(2031, 10, 1), snapshot.Schedule.Matches[0].Date);
    }

    [Fact]
    public void EquivalentInputsCreateTheSameSchedule()
    {
        var first = StartGame(424242);
        var second = StartGame(424242);

        Assert.Equal(first.Schedule.Matches, second.Schedule.Matches);
        Assert.Equal(first.RandomState, second.RandomState);
    }

    [Fact]
    public void DifferentRandomStateCreatesADifferentSchedule()
    {
        var first = StartGame(1);
        var second = StartGame(2);

        // Team identities differ between seeds, so compare the schedule by team name.
        Assert.NotEqual(CreateFingerprint(first), CreateFingerprint(second));
    }

    [Fact]
    public void ScheduleIsReadOnlyAndUnaffectedByManagedTeamSelection()
    {
        var manager = new GameManager();
        var initial = manager.StartNewGame(new NewGameCommand(2026, new RandomState(7), ManagedTeamName));
        var matches = Assert.IsAssignableFrom<IList<ScheduledMatchSnapshot>>(initial.Schedule.Matches);

        Assert.Throws<NotSupportedException>(() => matches.Clear());

        var otherTeam = initial.League.Teams.Single(team => team.Name == "Seattle Evergreens");
        var updated = manager.SelectManagedTeam(otherTeam.Id);

        Assert.Equal(initial.Schedule.Matches, updated.Schedule.Matches);
    }

    private static GameSnapshot StartGame(ulong seed) =>
        new GameManager().StartNewGame(new NewGameCommand(2026, new RandomState(seed), ManagedTeamName));

    private static int ExpectedMeetings(LeagueSnapshot league, TeamId team, TeamId opponent)
    {
        var (teamConference, teamDivision) = Locate(league, team);
        var (opponentConference, opponentDivision) = Locate(league, opponent);

        if (teamDivision == opponentDivision)
        {
            return 4;
        }

        return teamConference == opponentConference ? 3 : 2;
    }

    private static (ConferenceSnapshot Conference, DivisionSnapshot Division) Locate(
        LeagueSnapshot league,
        TeamId teamId) =>
        league.Conferences
            .SelectMany(conference => conference.Divisions.Select(division => (conference, division)))
            .Single(entry => entry.division.Teams.Any(team => team.Id == teamId));

    private static IEnumerable<(TeamId Team, TeamId Opponent)> DistinctPairs(LeagueSnapshot league) =>
        league.Teams.SelectMany(
            (team, index) => league.Teams.Skip(index + 1).Select(opponent => (team.Id, opponent.Id)));

    private static (TeamId, TeamId) PairKey(TeamId first, TeamId second) =>
        first.Value.CompareTo(second.Value) < 0 ? (first, second) : (second, first);

    private static string CreateFingerprint(GameSnapshot snapshot)
    {
        var names = snapshot.League.Teams.ToDictionary(team => team.Id, team => team.Name);
        return string.Join(
            '|',
            snapshot.Schedule.Matches.Select(match =>
                $"{match.Date:O}:{names[match.HomeTeamId]}:{names[match.AwayTeamId]}"));
    }
}