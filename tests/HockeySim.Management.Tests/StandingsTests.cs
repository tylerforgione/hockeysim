using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;

using Xunit;

using static HockeySim.Management.Tests.SeasonAdvancementTests;

namespace HockeySim.Management.Tests;

/// <summary>
/// Checks the standings exposed through snapshots. The ranking rules themselves are covered by
/// focused Domain scenarios; these tests check the tables are complete, consistent, and isolated.
/// </summary>
public sealed class StandingsTests
{
    [Fact]
    public void BeforeAnyMatchEveryTableListsItsTeamsLevelInLeagueOrder()
    {
        var snapshot = StartGame(new GameManager());

        AssertTablesMatchTheLeague(snapshot);
        var standings = snapshot.Season.Standings;
        Assert.Equal(snapshot.League.Teams.Select(team => team.Id), standings.League.Select(entry => entry.Record.TeamId));
        Assert.All(standings.League, entry =>
        {
            Assert.Equal(1, entry.Rank);
            Assert.Equal(0, entry.Record.GamesPlayed);
            Assert.Null(entry.Record.PointsPercentage);
        });
    }

    [Fact]
    public void MidseasonTablesAreRankedFromTheResultsSoFar()
    {
        var manager = new GameManager();
        var snapshot = StartGame(manager);
        for (var day = 0; day < 15; day++)
        {
            snapshot = manager.AdvanceDayReplacingInjured();
        }

        Assert.NotEmpty(snapshot.Season.Results);
        Assert.False(snapshot.Season.IsComplete);
        AssertTablesMatchTheLeague(snapshot);
        AssertEveryTableIsRanked(snapshot.Season.Standings);
        Assert.Equal(
            snapshot.Season.TeamRecords.OrderBy(record => record.TeamId.Value),
            snapshot.Season.Standings.League.Select(entry => entry.Record).OrderBy(record => record.TeamId.Value));
    }

    [Fact]
    public void EarlierStandingsAreUnchangedByAdvancingAndCannotBeModified()
    {
        var manager = new GameManager();
        var before = StartGame(manager);

        var after = manager.AdvanceDay();

        Assert.All(before.Season.Standings.League, entry => Assert.Equal(0, entry.Record.GamesPlayed));
        Assert.All(after.Season.Standings.League, entry => Assert.Equal(1, entry.Record.GamesPlayed));
        var standings = after.Season.Standings;
        Assert.Throws<NotSupportedException>(() => ((IList<StandingsEntrySnapshot>)standings.League).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IList<ConferenceStandingsSnapshot>)standings.Conferences).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IList<StandingsEntrySnapshot>)standings.Conferences[0].Teams).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IList<DivisionStandingsSnapshot>)standings.Conferences[0].Divisions).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IList<StandingsEntrySnapshot>)standings.Conferences[0].Divisions[0].Teams).Clear());
    }

    [Fact]
    public void TheWildCardViewListsEachDivisionsTopThreeThenTheRestOfItsConference()
    {
        var manager = new GameManager();
        StartGame(manager);
        GameSnapshot snapshot = null!;
        for (var day = 0; day < 15; day++)
        {
            snapshot = manager.AdvanceDayReplacingInjured();
        }

        var standings = snapshot.Season.Standings;
        Assert.Equal(standings.Conferences.Select(conference => conference.Name), standings.WildCard.Select(view => view.Name));
        foreach (var (conference, view) in standings.Conferences.Zip(standings.WildCard))
        {
            Assert.Equal(2, view.WildCardCount);
            Assert.Equal(conference.Divisions.Select(division => division.Name), view.DivisionLeaders.Select(division => division.Name));
            foreach (var (division, leaders) in conference.Divisions.Zip(view.DivisionLeaders))
            {
                Assert.Equal(division.Teams.Take(3), leaders.Teams);
            }

            // The rest of the conference, ranked among themselves.
            var leaderIds = view.DivisionLeaders.SelectMany(division => division.Teams).Select(entry => entry.Record.TeamId).ToHashSet();
            Assert.Equal(10, view.WildCardRace.Count);
            Assert.Equal(
                conference.Teams.Select(entry => entry.Record.TeamId).Where(teamId => !leaderIds.Contains(teamId)).Select(teamId => teamId.Value).Order(),
                view.WildCardRace.Select(entry => entry.Record.TeamId.Value).Order());
            AssertRanked(view.WildCardRace);
        }

        Assert.Throws<NotSupportedException>(() => ((IList<WildCardStandingsSnapshot>)standings.WildCard).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IList<DivisionStandingsSnapshot>)standings.WildCard[0].DivisionLeaders).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IList<StandingsEntrySnapshot>)standings.WildCard[0].WildCardRace).Clear());
    }

    [Fact]
    public void NoTeamHasClinchedOrBeenEliminatedOnOpeningDay()
    {
        var snapshot = StartGame(new GameManager());

        Assert.All(snapshot.Season.Standings.League, entry => Assert.Equal(PlayoffStatus.Undecided, entry.PlayoffStatus));
    }

    /// <summary>
    /// Each conference and division table holds exactly that group's teams, in league structure order.
    /// </summary>
    internal static void AssertTablesMatchTheLeague(GameSnapshot snapshot)
    {
        var standings = snapshot.Season.Standings;
        Assert.Equal(
            snapshot.League.Teams.Select(team => team.Id.Value).Order(),
            standings.League.Select(entry => entry.Record.TeamId.Value).Order());
        Assert.Equal(
            snapshot.League.Conferences.Select(conference => conference.Name),
            standings.Conferences.Select(conference => conference.Name));

        foreach (var (conference, table) in snapshot.League.Conferences.Zip(standings.Conferences))
        {
            Assert.Equal(
                conference.Divisions.SelectMany(division => division.Teams).Select(team => team.Id.Value).Order(),
                table.Teams.Select(entry => entry.Record.TeamId.Value).Order());
            Assert.Equal(conference.Divisions.Select(division => division.Name), table.Divisions.Select(division => division.Name));

            foreach (var (division, divisionTable) in conference.Divisions.Zip(table.Divisions))
            {
                Assert.Equal(
                    division.Teams.Select(team => team.Id.Value).Order(),
                    divisionTable.Teams.Select(entry => entry.Record.TeamId.Value).Order());
            }
        }
    }

    internal static void AssertEveryTableIsRanked(StandingsSnapshot standings)
    {
        AssertRanked(standings.League);
        foreach (var conference in standings.Conferences)
        {
            AssertRanked(conference.Teams);
            foreach (var division in conference.Divisions)
            {
                AssertRanked(division.Teams);
            }
        }
    }

    /// <summary>
    /// Checks the ordering the per-team criteria imply: more points first, then fewer games, and
    /// a shared rank only for teams level on every per-team column. Head-to-head can reorder
    /// teams level on those columns, so it is not checked here.
    /// </summary>
    private static void AssertRanked(IReadOnlyList<StandingsEntrySnapshot> table)
    {
        Assert.Equal(1, table[0].Rank);
        for (var index = 1; index < table.Count; index++)
        {
            var (previous, current) = (table[index - 1], table[index]);
            Assert.True(PerTeamKey(previous.Record).CompareTo(PerTeamKey(current.Record)) >= 0);

            if (current.Rank == previous.Rank)
            {
                Assert.Equal(
                    (PerTeamKey(previous.Record), previous.Record.GoalDifferential, previous.Record.GoalsFor),
                    (PerTeamKey(current.Record), current.Record.GoalDifferential, current.Record.GoalsFor));
            }
            else
            {
                Assert.Equal(index + 1, current.Rank);
            }
        }
    }

    private static (int, int, int, int, int) PerTeamKey(TeamRecordSnapshot record) =>
        (record.Points, -record.GamesPlayed, record.RegulationWins, record.RegulationAndOvertimeWins, record.Wins);
}