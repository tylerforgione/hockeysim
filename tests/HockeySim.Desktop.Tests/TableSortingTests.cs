using HockeySim.Desktop.Game;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.Tables;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class TableSortingTests
{
    [Fact]
    public async Task TablesStartInTheirDefaultOrderWithNoIndicator()
    {
        var (_, shell) = await PlayDays(2);
        var roster = shell.Roster.Roster;

        Assert.Null(roster.SkaterSort);
        Assert.Null(roster.GoalieSort);
        Assert.Null(shell.Schedule.MatchesSort);
        Assert.Equal(
            roster.Skaters.OrderBy(row => row.Player.Position).ThenBy(row => row.Player.LastName, StringComparer.Ordinal).Select(row => row.Player.Id),
            roster.Skaters.Select(row => row.Player.Id));
        Assert.Equal(shell.Schedule.Matches.OrderBy(match => match.Date), shell.Schedule.Matches);
    }

    [Fact]
    public async Task FigureColumnsSortHighestFirstThenReverse()
    {
        var (_, shell) = await PlayDays(2);
        var roster = shell.Roster.Roster;

        roster.SortSkatersCommand.Execute("overall");
        Assert.Equal(new TableSort("overall", SortDirection.Descending), roster.SkaterSort);
        Assert.Equal(roster.Skaters.OrderByDescending(row => row.Overall).Select(row => row.Overall), roster.Skaters.Select(row => row.Overall));

        roster.SortSkatersCommand.Execute("overall");
        Assert.Equal(new TableSort("overall", SortDirection.Ascending), roster.SkaterSort);
        Assert.Equal(roster.Skaters.OrderBy(row => row.Overall).Select(row => row.Overall), roster.Skaters.Select(row => row.Overall));

        roster.SortSkatersCommand.Execute("skating");
        Assert.Equal(new TableSort("skating", SortDirection.Descending), roster.SkaterSort);
    }

    [Fact]
    public async Task PercentagesTimesAndSignedFiguresSortByTheirValuesWithDashesLast()
    {
        var (_, shell) = await PlayDays(2);
        var roster = shell.Roster.Roster;
        roster.ShowColumnsCommand.Execute(RosterColumns.Basic);
        Assert.Contains(roster.Skaters, row => row.Season.TimeOnIcePerGameValue is null);

        foreach (var direction in new[] { SortDirection.Descending, SortDirection.Ascending })
        {
            roster.SortSkatersCommand.Execute("time-on-ice-per-game");
            AssertDefinedThenUndefined(roster.Skaters, row => row.Season.TimeOnIcePerGameValue, direction);
        }

        roster.SortSkatersCommand.Execute("faceoff-percentage");
        AssertDefinedThenUndefined(roster.Skaters, row => row.Season.FaceoffPercentageValue, SortDirection.Descending);
        roster.SortSkatersCommand.Execute("faceoff-percentage");
        AssertDefinedThenUndefined(roster.Skaters, row => row.Season.FaceoffPercentageValue, SortDirection.Ascending);

        roster.SortSkatersCommand.Execute("plus-minus");
        var plusMinus = roster.Skaters.Select(row => row.Season.PlusMinusValue).ToList();
        Assert.Equal(plusMinus.OrderByDescending(value => value), plusMinus);
        Assert.Contains(plusMinus, value => value < 0);
    }

    [Fact]
    public async Task ExpectedGoalsAndOnIceSharesSortNumerically()
    {
        var (_, shell) = await PlayDays(3);
        var roster = shell.Roster.Roster;
        roster.ShowColumnsCommand.Execute(RosterColumns.Advanced);

        roster.SortSkatersCommand.Execute("individual-expected-goals");
        var expected = roster.Skaters.Select(row => row.Season.IndividualExpectedGoalsValue).ToList();
        Assert.Equal(expected.OrderByDescending(value => value), expected);

        roster.SortSkatersCommand.Execute("expected-goals-percentage");
        AssertDefinedThenUndefined(roster.Skaters, row => row.Season.OnIceExpectedGoalsPercentageValue, SortDirection.Descending);
        roster.SortSkatersCommand.Execute("expected-goals-percentage");
        AssertDefinedThenUndefined(roster.Skaters, row => row.Season.OnIceExpectedGoalsPercentageValue, SortDirection.Ascending);
    }

    [Fact]
    public async Task GoalieColumnsSortNumericallyWithDashesLast()
    {
        var (_, shell) = await PlayDays(3);
        var roster = shell.Roster.Roster;
        roster.ShowColumnsCommand.Execute(RosterColumns.Basic);
        Assert.Contains(roster.Goalies, row => row.Season.SavePercentageValue is null);

        roster.SortGoaliesCommand.Execute("save-percentage");
        AssertDefinedThenUndefined(roster.Goalies, row => row.Season.SavePercentageValue, SortDirection.Descending);
        roster.SortGoaliesCommand.Execute("save-percentage");
        AssertDefinedThenUndefined(roster.Goalies, row => row.Season.SavePercentageValue, SortDirection.Ascending);

        roster.ShowColumnsCommand.Execute(RosterColumns.Advanced);
        roster.SortGoaliesCommand.Execute("goals-saved-above-expected");
        var saved = roster.Goalies.Select(row => row.Season.GoalsSavedAboveExpectedValue).ToList();
        Assert.Equal(saved.OrderByDescending(value => value), saved);
        roster.SortGoaliesCommand.Execute("time-in-net");
        var time = roster.Goalies.Select(row => row.Season.TimeInNetValue).ToList();
        Assert.Equal(time.OrderByDescending(value => value), time);
    }

    [Fact]
    public async Task TextColumnsSortAscendingFirst()
    {
        var (_, shell) = await PlayDays(1);
        var roster = shell.Roster.Roster;

        roster.SortSkatersCommand.Execute("name");
        Assert.Equal(SortDirection.Ascending, roster.SkaterSort!.Value.Direction);
        Assert.Equal(
            roster.Skaters.OrderBy(row => row.Player.LastName, StringComparer.Ordinal).ThenBy(row => row.Player.FirstName, StringComparer.Ordinal).Select(row => row.Player.Id),
            roster.Skaters.Select(row => row.Player.Id));

        roster.SortSkatersCommand.Execute("position");
        Assert.Equal(roster.Skaters.Select(row => row.Player.Position).Order(), roster.Skaters.Select(row => row.Player.Position));

        roster.ShowColumnsCommand.Execute(RosterColumns.Bio);
        roster.SortSkatersCommand.Execute("nationality");
        Assert.Equal(roster.Skaters.Select(row => row.Nationality).Order(StringComparer.Ordinal), roster.Skaters.Select(row => row.Nationality));

        roster.SortSkatersCommand.Execute("shoots");
        Assert.Equal(roster.Skaters.Select(row => row.Handedness).Order(StringComparer.Ordinal), roster.Skaters.Select(row => row.Handedness));
        Assert.Equal("L", roster.Skaters[0].Handedness);

        roster.SortSkatersCommand.Execute("shoots");
        Assert.Equal("R", roster.Skaters[0].Handedness);
    }

    [Fact]
    public async Task LineupSortsInLineupOrderWithScratchesLast()
    {
        var (session, shell) = await PlayDays(1);
        var roster = shell.Roster.Roster;
        var lineup = session.ManagedTeam.Lineup;

        roster.SortSkatersCommand.Execute("lineup");

        var rows = roster.Skaters;
        Assert.Equal(lineup.ForwardLines[0].LeftWingId, rows[0].Player.Id);
        Assert.Equal(lineup.ForwardLines[0].CentreId, rows[1].Player.Id);
        Assert.Equal(lineup.ForwardLines[1].LeftWingId, rows[3].Player.Id);
        Assert.Equal(lineup.DefencePairs[0].LeftDefenceId, rows[12].Player.Id);
        Assert.All(rows.SkipWhile(row => !row.IsScratched), row => Assert.True(row.IsScratched));
        Assert.Contains(rows, row => row.IsScratched);

        roster.SortGoaliesCommand.Execute("lineup");
        Assert.Equal(lineup.StartingGoalieId, roster.Goalies[0].Player.Id);
        roster.SortGoaliesCommand.Execute("lineup");
        Assert.Equal(lineup.StartingGoalieId, roster.Goalies[^1].Player.Id);
    }

    [Fact]
    public async Task TiesKeepTheDefaultOrderInBothDirections()
    {
        var (_, shell) = await PlayDays(1);
        var roster = shell.Roster.Roster;
        roster.ShowColumnsCommand.Execute(RosterColumns.Basic);
        var defaultOrder = roster.Skaters.Select(row => row.Player.Id).ToList();

        roster.SortSkatersCommand.Execute("age");
        AssertStable(roster.Skaters.Select(row => (row.Player.Id, row.Age)), defaultOrder, descending: true);
        roster.SortSkatersCommand.Execute("age");
        AssertStable(roster.Skaters.Select(row => (row.Player.Id, row.Age)), defaultOrder, descending: false);
    }

    [Fact]
    public async Task SortingKeepsThePlayerSelected()
    {
        var (_, shell) = await PlayDays(1);
        var roster = shell.Roster.Roster;
        var player = roster.Skaters[5].Player.Id;
        roster.SelectPlayer(player);

        roster.SortSkatersCommand.Execute("overall");
        roster.SortSkatersCommand.Execute("overall");

        Assert.Equal(player, roster.SelectedPlayer!.Id);
        Assert.Same(roster.Skaters.Single(row => row.Player.Id == player), roster.SelectedSkater);

        var goalie = roster.Goalies[1].Player.Id;
        roster.SelectPlayer(goalie);
        roster.SortSkatersCommand.Execute("name");
        Assert.Equal(goalie, roster.SelectedPlayer!.Id);
        roster.SortGoaliesCommand.Execute("name");
        roster.SortGoaliesCommand.Execute("name");
        Assert.Same(roster.Goalies.Single(row => row.Player.Id == goalie), roster.SelectedGoalie);
    }

    [Fact]
    public async Task OneSortHoldsAcrossColumnViewsWhereTheColumnExists()
    {
        var (_, shell) = await PlayDays(1);
        var roster = shell.Roster.Roster;

        roster.SortSkatersCommand.Execute("overall");
        roster.SortGoaliesCommand.Execute("overall");
        roster.ShowColumnsCommand.Execute(RosterColumns.Basic);
        Assert.Equal(new TableSort("overall", SortDirection.Descending), roster.SkaterSort);
        Assert.Equal(new TableSort("overall", SortDirection.Descending), roster.GoalieSort);
        Assert.Equal(roster.Skaters.Select(row => row.Overall).OrderDescending(), roster.Skaters.Select(row => row.Overall));

        // Advanced has no OVR: the default order and no arrow, but the sort is still remembered.
        roster.ShowColumnsCommand.Execute(RosterColumns.Advanced);
        Assert.Null(roster.SkaterSort);
        Assert.Null(roster.GoalieSort);
        Assert.Equal(new TableSort("overall", SortDirection.Descending), roster.Sorts.Skaters);
        Assert.Equal(
            roster.Skaters.OrderBy(row => row.Player.Position).ThenBy(row => row.Player.LastName, StringComparer.Ordinal).Select(row => row.Player.Id),
            roster.Skaters.Select(row => row.Player.Id));

        roster.ShowColumnsCommand.Execute(RosterColumns.Ratings);
        Assert.Equal(new TableSort("overall", SortDirection.Descending), roster.SkaterSort);
        Assert.Equal(roster.Skaters.Select(row => row.Overall).OrderDescending(), roster.Skaters.Select(row => row.Overall));
    }

    [Fact]
    public async Task AHeaderClickInAViewWithoutTheRememberedColumnStartsANewSort()
    {
        var (_, shell) = await PlayDays(1);
        var roster = shell.Roster.Roster;
        roster.SortSkatersCommand.Execute("overall");
        roster.ShowColumnsCommand.Execute(RosterColumns.Advanced);

        roster.SortSkatersCommand.Execute("corsi-for");

        Assert.Equal(new TableSort("corsi-for", SortDirection.Descending), roster.SkaterSort);
        roster.ShowColumnsCommand.Execute(RosterColumns.Ratings);
        Assert.Null(roster.SkaterSort);
        roster.ShowColumnsCommand.Execute(RosterColumns.Advanced);
        Assert.Equal(new TableSort("corsi-for", SortDirection.Descending), roster.SkaterSort);
    }

    [Fact]
    public async Task TheRosterSortSurvivesARefreshAfterADay()
    {
        var (_, shell) = await PlayDays(1);
        shell.Roster.Roster.ShowColumnsCommand.Execute(RosterColumns.Basic);
        shell.Roster.Roster.SortSkatersCommand.Execute("points");
        shell.Roster.Roster.SortSkatersCommand.Execute("points");
        var selected = shell.Roster.Roster.Skaters[3].Player.Id;
        shell.Roster.SelectPlayer(selected);

        await shell.AdvanceDayCommand.ExecuteAsync(null);

        var roster = shell.Roster.Roster;
        Assert.Equal(new TableSort("points", SortDirection.Ascending), roster.SkaterSort);
        Assert.Equal(roster.Skaters.Select(row => row.Season.Points).Order(), roster.Skaters.Select(row => row.Season.Points));
        Assert.Equal(selected, roster.SelectedPlayer!.Id);
    }

    [Fact]
    public async Task TheRosterSortSurvivesSwitchingTeams()
    {
        var (_, shell) = await PlayDays(1);
        shell.Teams.Roster.SortSkatersCommand.Execute("overall");
        shell.Teams.Roster.SortGoaliesCommand.Execute("overall");

        shell.Teams.ShowNextTeamCommand.Execute(null);

        var roster = shell.Teams.Roster;
        Assert.NotEqual(shell.Teams.Teams.Single(team => team.IsManaged).Id, shell.Teams.SelectedTeam.Id);
        Assert.Equal(new TableSort("overall", SortDirection.Descending), roster.SkaterSort);
        Assert.Equal(roster.Skaters.Select(row => row.Overall).OrderDescending(), roster.Skaters.Select(row => row.Overall));
        Assert.Equal(roster.Goalies.Select(row => row.Overall).OrderDescending(), roster.Goalies.Select(row => row.Overall));
    }

    [Fact]
    public async Task AnUnknownColumnIsRejected()
    {
        var (_, shell) = await PlayDays(1);

        Assert.Throws<ArgumentException>(() => shell.Roster.Roster.SortSkatersCommand.Execute("catches"));
    }

    [Fact]
    public async Task TheScheduleSortsByEveryColumnWithUnplayedMatchesLast()
    {
        var (_, shell) = await PlayDays(2);
        var schedule = shell.Schedule;
        Assert.Contains(schedule.Matches, match => !match.IsCompleted);

        schedule.SortMatchesCommand.Execute("result");
        Assert.Equal(new TableSort("result", SortDirection.Descending), schedule.MatchesSort);
        AssertDefinedThenUndefined(schedule.Matches, match => match.GoalDifferential, SortDirection.Descending);
        schedule.SortMatchesCommand.Execute("result");
        AssertDefinedThenUndefined(schedule.Matches, match => match.GoalDifferential, SortDirection.Ascending);

        schedule.SortMatchesCommand.Execute("opponent");
        Assert.Equal(SortDirection.Ascending, schedule.MatchesSort!.Value.Direction);
        Assert.Equal(schedule.Matches.Select(match => match.Opponent).Order(StringComparer.Ordinal), schedule.Matches.Select(match => match.Opponent));

        schedule.SortMatchesCommand.Execute("venue");
        Assert.Equal(schedule.Matches.Select(match => match.Venue).Order(StringComparer.Ordinal), schedule.Matches.Select(match => match.Venue));

        schedule.SortMatchesCommand.Execute("date");
        schedule.SortMatchesCommand.Execute("date");
        Assert.Equal(schedule.Matches.Select(match => match.Date).OrderDescending(), schedule.Matches.Select(match => match.Date));
        schedule.SortMatchesCommand.Execute("phase");
        Assert.Equal(schedule.Matches.Select(match => match.Date).Order(), schedule.Matches.Select(match => match.Date));
    }

    [Fact]
    public async Task TheScheduleSortSurvivesSwitchingTeamsRefreshingAndKeepsTheSelectedMatch()
    {
        var (session, shell) = await PlayDays(2);
        var schedule = shell.Schedule;
        schedule.SortMatchesCommand.Execute("opponent");
        var selected = schedule.Matches.First(match => match.IsCompleted);
        schedule.SelectedMatch = selected;

        schedule.SortMatchesCommand.Execute("opponent");

        Assert.Equal(selected.Date, schedule.SelectedMatch!.Date);
        Assert.NotNull(schedule.SelectedResult);

        var other = schedule.Teams.First(team => !team.IsManaged);
        schedule.ShowTeam(other.Id);
        Assert.Equal(new TableSort("opponent", SortDirection.Descending), schedule.MatchesSort);
        Assert.Equal(schedule.Matches.Select(match => match.Opponent).OrderDescending(StringComparer.Ordinal), schedule.Matches.Select(match => match.Opponent));

        await shell.AdvanceDayCommand.ExecuteAsync(null);
        Assert.Equal(new TableSort("opponent", SortDirection.Descending), schedule.MatchesSort);
        Assert.Equal(schedule.Matches.Select(match => match.Opponent).OrderDescending(StringComparer.Ordinal), schedule.Matches.Select(match => match.Opponent));
        Assert.Equal(other.Id, schedule.SelectedTeam.Id);
        Assert.NotEqual(session.ManagedTeam.Id, schedule.SelectedTeam.Id);
    }

    [Fact]
    public async Task TheBoxScoreSortsBothTeamsAndCarriesToAnotherMatch()
    {
        var (_, shell) = await PlayDays(2);
        var schedule = shell.Schedule;
        var completed = schedule.Matches.Where(match => match.IsCompleted).ToList();
        schedule.SelectedMatch = completed[0];
        var detail = schedule.SelectedResult!;
        Assert.Null(detail.SkaterSort);

        detail.Away.SortSkatersCommand.Execute("time-on-ice");

        var expected = new TableSort("time-on-ice", SortDirection.Descending);
        Assert.Equal(expected, detail.SkaterSort);
        Assert.Equal(expected, detail.Home.Sort);
        Assert.Equal(expected, detail.Away.Sort);
        foreach (var side in new[] { detail.Home, detail.Away })
        {
            var times = side.Skaters.Select(row => row.Values.TimeOnIce).ToList();
            Assert.Equal(times.OrderDescending(), times);
        }

        detail.Home.SortSkatersCommand.Execute("time-on-ice");
        Assert.Equal(SortDirection.Ascending, detail.Away.Sort!.Value.Direction);

        schedule.SelectedMatch = completed[1];
        var next = schedule.SelectedResult!;
        Assert.NotSame(detail, next);
        Assert.Equal(new TableSort("time-on-ice", SortDirection.Ascending), next.SkaterSort);
        var nextTimes = next.Home.Skaters.Select(row => row.Values.TimeOnIce).ToList();
        Assert.Equal(nextTimes.Order(), nextTimes);
    }

    [Fact]
    public async Task BoxScoreFaceoffsSortByWonThenFewerLostWithDashesLast()
    {
        var (_, shell) = await PlayDays(2);
        shell.Schedule.SelectedMatch = shell.Schedule.Matches.First(match => match.IsCompleted);
        var side = shell.Schedule.SelectedResult!.Home;
        Assert.Contains(side.Skaters, row => row.Faceoffs == MatchDisplay.Undefined);

        side.SortSkatersCommand.Execute("faceoffs");
        AssertDefinedThenUndefined(side.Skaters, row => row.Values.Faceoffs, SortDirection.Descending);
        Assert.All(side.Skaters.Where(row => row.Values.Faceoffs is null), row => Assert.Equal(MatchDisplay.Undefined, row.Faceoffs));

        side.SortSkatersCommand.Execute("plus-minus");
        var plusMinus = side.Skaters.Select(row => row.Values.PlusMinus).ToList();
        Assert.Equal(plusMinus.OrderDescending(), plusMinus);

        side.SortSkatersCommand.Execute("corsi-percentage");
        AssertDefinedThenUndefined(side.Skaters, row => row.Values.CorsiPercentage, SortDirection.Descending);

        side.SortSkatersCommand.Execute("position");
        var positions = side.Skaters.Select(row => row.Values.Position).ToList();
        Assert.Equal(positions.Order(), positions);
    }

    private static void AssertDefinedThenUndefined<T, TKey>(IEnumerable<T> rows, Func<T, TKey?> key, SortDirection direction)
        where TKey : struct, IComparable<TKey>
    {
        var keys = rows.Select(key).ToList();
        var defined = keys.TakeWhile(value => value is not null).Select(value => value!.Value).ToList();
        Assert.Equal(keys.Count, defined.Count + keys.Skip(defined.Count).Count());
        Assert.All(keys.Skip(defined.Count), value => Assert.Null(value));
        Assert.Equal(
            direction == SortDirection.Descending ? defined.OrderByDescending(value => value) : defined.OrderBy(value => value),
            defined);
    }

    private static void AssertStable(IEnumerable<(PlayerId Id, int Value)> rows, List<PlayerId> defaultOrder, bool descending)
    {
        var expected = defaultOrder.Select((id, index) => (id, index));
        var sorted = rows.ToList();
        var values = sorted.ToDictionary(row => row.Id, row => row.Value);
        var ordered = descending
            ? expected.OrderByDescending(entry => values[entry.id]).ThenBy(entry => entry.index)
            : expected.OrderBy(entry => values[entry.id]).ThenBy(entry => entry.index);
        Assert.Equal(ordered.Select(entry => entry.id), sorted.Select(row => row.Id));
    }

    private static async Task<(GameSession Session, GameShellViewModel Shell)> PlayDays(int days)
    {
        var session = GameTestData.StartSession(new GameManager());
        var shell = new GameShellViewModel(session);
        for (var day = 0; day < days; day++)
        {
            await shell.AdvanceDayCommand.ExecuteAsync(null);
        }

        return (session, shell);
    }
}