using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.Tables;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;

using Xunit;

namespace HockeySim.Desktop.Tests;

/// <summary>
/// Roster tables by player group and column view, and the strip under them. The headless
/// walkthrough in <see cref="MainWindowViewTests"/> checks that the views show these columns.
/// </summary>
public sealed class RosterViewsTests
{
    // Far more days than an injury to a dressed player takes to arise with the test seed.
    private const int MaximumDays = 120;

    [Fact]
    public void SkaterTablesShowTheirColumnsInEachView()
    {
        Assert.Equal(
            ["number", "name", "position", "lineup", "age", "overall", "games-played", "goals", "assists", "points", "plus-minus",
                "penalty-minutes", "power-play-points", "shorthanded-points", "shots", "time-on-ice-per-game", "faceoff-percentage"],
            Keys(RosterTableColumns.Skaters(RosterColumns.Basic)));
        Assert.Equal(
            ["number", "name", "position", "lineup", "games-played", "corsi-for", "corsi-against", "corsi-percentage", "fenwick-for",
                "fenwick-against", "fenwick-percentage", "expected-goals-for", "expected-goals-against", "expected-goals-percentage",
                "individual-expected-goals"],
            Keys(RosterTableColumns.Skaters(RosterColumns.Advanced)));
        Assert.Equal(
            ["number", "name", "position", "lineup", "overall", "skating", "shot-power", "shot-accuracy", "puck-control", "passing",
                "offensive-awareness", "defensive-awareness", "checking", "shot-blocking", "stick-checking"],
            Keys(RosterTableColumns.Skaters(RosterColumns.Ratings)));
        Assert.Equal(
            ["number", "name", "position", "lineup", "age", "shoots", "height", "weight", "born", "birthplace", "nationality"],
            Keys(RosterTableColumns.Skaters(RosterColumns.Bio)));
    }

    [Fact]
    public void GoalieTablesShowTheirColumnsInEachView()
    {
        Assert.Equal(
            ["number", "name", "lineup", "age", "overall", "games-played", "shots-against", "saves", "goals-against", "save-percentage",
                "goals-against-average", "shutouts"],
            Keys(RosterTableColumns.Goalies(RosterColumns.Basic)));
        Assert.Equal(
            ["number", "name", "lineup", "games-played", "time-in-net", "shots-against", "goals-against", "expected-goals-against",
                "goals-saved-above-expected", "save-percentage", "expected-save-percentage"],
            Keys(RosterTableColumns.Goalies(RosterColumns.Advanced)));
        Assert.Equal(
            ["number", "name", "lineup", "overall", "reflex", "positioning", "rebound-control"],
            Keys(RosterTableColumns.Goalies(RosterColumns.Ratings)));
        Assert.Equal(
            ["number", "name", "lineup", "age", "catches", "height", "weight", "born", "birthplace", "nationality"],
            Keys(RosterTableColumns.Goalies(RosterColumns.Bio)));
    }

    [Fact]
    public void AllPlayersIsTheDefaultGroupAndRatingsTheDefaultView()
    {
        var roster = StartRoster();

        Assert.Equal(RosterGroup.AllPlayers, roster.Group);
        Assert.True(roster.ShowsRatings);
        Assert.True(roster.ShowsAllPlayers);
        Assert.True(roster.ShowsSkaterTable);
        Assert.True(roster.ShowsGoalieTable);
        Assert.Equal(20, roster.Skaters.Count);
        Assert.Equal(3, roster.Goalies.Count);
        Assert.Equal(23, roster.PlayerCount);
    }

    [Fact]
    public void EachGroupListsItsPlayersInItsTables()
    {
        var session = GameTestData.StartSession();
        var roster = new TeamRosterViewModel(session, session.ManagedTeam.Id);
        int Count(Position position) => session.ManagedTeam.Roster.Count(player => player.Position == position);

        roster.ShowGroupCommand.Execute(RosterGroup.Skaters);
        Assert.True(roster.ShowsSkaterTable);
        Assert.False(roster.ShowsGoalieTable);
        Assert.Equal(20, roster.Skaters.Count);
        Assert.All(roster.Skaters, row => Assert.NotEqual(Position.Goalie, row.Player.Position));
        Assert.Empty(roster.Goalies);
        Assert.Equal(20, roster.PlayerCount);

        roster.ShowGroupCommand.Execute(RosterGroup.Forwards);
        Assert.True(roster.ShowsForwardsGroup);
        Assert.All(roster.Skaters, row => Assert.True(row.Player.Position is Position.Centre or Position.Wing));
        Assert.Equal(Count(Position.Centre) + Count(Position.Wing), roster.Skaters.Count);
        Assert.Empty(roster.Goalies);

        roster.ShowGroupCommand.Execute(RosterGroup.Defence);
        Assert.All(roster.Skaters, row => Assert.Equal(Position.Defence, row.Player.Position));
        Assert.Equal(Count(Position.Defence), roster.Skaters.Count);
        Assert.False(roster.ShowsGoalieTable);

        roster.ShowGroupCommand.Execute(RosterGroup.Goalies);
        Assert.False(roster.ShowsSkaterTable);
        Assert.True(roster.ShowsGoalieTable);
        Assert.Empty(roster.Skaters);
        Assert.Equal(3, roster.Goalies.Count);
        Assert.Equal(3, roster.PlayerCount);

        roster.ShowGroupCommand.Execute(RosterGroup.AllPlayers);
        Assert.Equal(20, roster.Skaters.Count);
        Assert.Equal(3, roster.Goalies.Count);
    }

    [Fact]
    public void TheForwardsGroupFiltersCentresAndWingers()
    {
        var roster = StartRoster();
        var centres = roster.Skaters.Count(row => row.Player.Position == Position.Centre);
        var wingers = roster.Skaters.Count(row => row.Player.Position == Position.Wing);
        roster.ShowGroupCommand.Execute(RosterGroup.Forwards);
        Assert.Equal(ForwardFilter.AllForwards, roster.ForwardPosition);
        Assert.Equal(centres + wingers, roster.Skaters.Count);

        roster.ForwardFilterIndex = 1;
        Assert.Equal(ForwardFilter.Centres, roster.ForwardPosition);
        Assert.Equal(centres, roster.Skaters.Count);
        Assert.All(roster.Skaters, row => Assert.Equal(Position.Centre, row.Player.Position));

        roster.ForwardFilterIndex = 2;
        Assert.Equal(wingers, roster.Skaters.Count);
        Assert.All(roster.Skaters, row => Assert.Equal(Position.Wing, row.Player.Position));

        // The filter applies to the Forwards group alone.
        roster.ShowGroupCommand.Execute(RosterGroup.Skaters);
        Assert.Equal(20, roster.Skaters.Count);
        roster.ShowGroupCommand.Execute(RosterGroup.Defence);
        Assert.All(roster.Skaters, row => Assert.Equal(Position.Defence, row.Player.Position));
        roster.ShowGroupCommand.Execute(RosterGroup.Forwards);
        Assert.Equal(wingers, roster.Skaters.Count);
    }

    [Fact]
    public async Task TheSortSurvivesSwitchingGroupAndView()
    {
        var (_, shell) = await PlayDays(2);
        var roster = shell.Roster.Roster;
        roster.ShowColumnsCommand.Execute(RosterColumns.Basic);
        roster.SortSkatersCommand.Execute("points");
        roster.SortGoaliesCommand.Execute("shutouts");

        roster.ShowGroupCommand.Execute(RosterGroup.Defence);
        Assert.Equal(new TableSort("points", SortDirection.Descending), roster.SkaterSort);
        Assert.Equal(roster.Skaters.Select(row => row.Season.Points).OrderDescending(), roster.Skaters.Select(row => row.Season.Points));

        roster.ShowGroupCommand.Execute(RosterGroup.Goalies);
        Assert.Equal(new TableSort("shutouts", SortDirection.Descending), roster.GoalieSort);

        // Advanced has neither column (points, shutouts): no arrow and the default order, with the sorts remembered.
        roster.ShowColumnsCommand.Execute(RosterColumns.Advanced);
        Assert.Null(roster.SkaterSort);
        Assert.Null(roster.GoalieSort);
        Assert.Equal(new RosterSorts(new TableSort("points", SortDirection.Descending), new TableSort("shutouts", SortDirection.Descending)), roster.Sorts);

        // Both columns exist in Basic again, whatever the group.
        roster.ShowGroupCommand.Execute(RosterGroup.AllPlayers);
        roster.ShowColumnsCommand.Execute(RosterColumns.Basic);
        Assert.Equal(new TableSort("points", SortDirection.Descending), roster.SkaterSort);
        Assert.Equal(new TableSort("shutouts", SortDirection.Descending), roster.GoalieSort);
        Assert.Equal(roster.Skaters.Select(row => row.Season.Points).OrderDescending(), roster.Skaters.Select(row => row.Season.Points));
    }

    [Fact]
    public async Task TheSortCarriesAcrossTeamsAndDaysWithTheGroupAndFilter()
    {
        var (_, shell) = await PlayDays(1);
        var teams = shell.Teams;
        teams.Roster.ShowGroupCommand.Execute(RosterGroup.Forwards);
        teams.Roster.ForwardFilterIndex = 2;
        teams.Roster.SortSkatersCommand.Execute("overall");

        teams.ShowNextTeamCommand.Execute(null);
        await shell.AdvanceDayCommand.ExecuteAsync(null);

        Assert.Equal(RosterGroup.Forwards, teams.Roster.Group);
        Assert.Equal(ForwardFilter.Wingers, teams.Roster.ForwardPosition);
        Assert.Equal(new TableSort("overall", SortDirection.Descending), teams.Roster.SkaterSort);
        Assert.All(teams.Roster.Skaters, row => Assert.Equal(Position.Wing, row.Player.Position));
    }

    [Fact]
    public void BioColumnsSortByHeightWeightBirthDatePlaceAndNationality()
    {
        var roster = StartRoster();
        roster.ShowColumnsCommand.Execute(RosterColumns.Bio);

        roster.SortSkatersCommand.Execute("height");
        Assert.Equal(new TableSort("height", SortDirection.Descending), roster.SkaterSort);
        Assert.Equal(roster.Skaters.Select(row => row.Player.Biography.Height.Inches).OrderDescending(), roster.Skaters.Select(row => row.Player.Biography.Height.Inches));

        roster.SortSkatersCommand.Execute("weight");
        Assert.Equal(roster.Skaters.Select(row => row.Player.Biography.Weight.Pounds).OrderDescending(), roster.Skaters.Select(row => row.Player.Biography.Weight.Pounds));

        roster.SortSkatersCommand.Execute("born");
        Assert.Equal(SortDirection.Ascending, roster.SkaterSort!.Value.Direction);
        Assert.Equal(roster.Skaters.Select(row => row.Player.Biography.BirthDate).Order(), roster.Skaters.Select(row => row.Player.Biography.BirthDate));

        roster.SortSkatersCommand.Execute("birthplace");
        Assert.Equal(roster.Skaters.Select(row => row.Birthplace).Order(StringComparer.Ordinal), roster.Skaters.Select(row => row.Birthplace));

        roster.SortGoaliesCommand.Execute("catches");
        Assert.Equal(roster.Goalies.Select(row => row.Handedness).Order(StringComparer.Ordinal), roster.Goalies.Select(row => row.Handedness));
        roster.SortGoaliesCommand.Execute("nationality");
        Assert.Equal(roster.Goalies.Select(row => row.Nationality).Order(StringComparer.Ordinal), roster.Goalies.Select(row => row.Nationality));

        var skater = roster.Skaters[0];
        Assert.Matches(@"^\d+' \d+""$", skater.Height);
        Assert.EndsWith(" lb", skater.Weight, StringComparison.Ordinal);
        Assert.Equal(PlayerDisplay.FormatBirthplace(skater.Player.Biography.Birthplace), skater.Birthplace);
    }

    [Fact]
    public void FilteringOutTheSelectedPlayerSelectsTheFirstVisiblePlayer()
    {
        var roster = StartRoster();
        var goalie = roster.Goalies[1];
        roster.SelectPlayer(goalie.Player.Id);
        Assert.Same(goalie, roster.SelectedGoalie);

        roster.ShowGroupCommand.Execute(RosterGroup.Skaters);
        Assert.Same(roster.Skaters[0], roster.SelectedSkater);
        Assert.Null(roster.SelectedGoalie);
        Assert.Equal(roster.Skaters[0].Player.Id, roster.SelectedPlayer!.Id);

        var defenceman = roster.Skaters.First(row => row.Player.Position == Position.Defence);
        roster.SelectPlayer(defenceman.Player.Id);
        roster.ShowGroupCommand.Execute(RosterGroup.Defence);
        Assert.Same(defenceman, roster.SelectedSkater);

        roster.ShowGroupCommand.Execute(RosterGroup.Forwards);
        Assert.Same(roster.Skaters[0], roster.SelectedSkater);
        Assert.True(roster.SelectedSkater!.Player.Position is Position.Centre or Position.Wing);

        var centre = roster.Skaters.First(row => row.Player.Position == Position.Centre);
        roster.SelectPlayer(centre.Player.Id);
        roster.ForwardFilterIndex = 2;
        Assert.Equal(Position.Wing, roster.Skaters[0].Player.Position);
        Assert.Same(roster.Skaters[0], roster.SelectedSkater);

        roster.ShowGroupCommand.Execute(RosterGroup.Goalies);
        Assert.Same(roster.Goalies[0], roster.SelectedGoalie);
        Assert.Null(roster.SelectedSkater);
    }

    [Fact]
    public void SelectionPersistsAcrossGroupViewAndSortWhileVisible()
    {
        var roster = StartRoster();
        var goalie = roster.Goalies[2];
        roster.SelectPlayer(goalie.Player.Id);

        roster.ShowGroupCommand.Execute(RosterGroup.Goalies);
        roster.ShowColumnsCommand.Execute(RosterColumns.Bio);
        roster.SortGoaliesCommand.Execute("age");
        roster.ShowGroupCommand.Execute(RosterGroup.AllPlayers);

        Assert.Equal(goalie.Player.Id, roster.SelectedPlayer!.Id);
        Assert.Same(roster.Goalies.Single(row => row.Player.Id == goalie.Player.Id), roster.SelectedGoalie);
    }

    [Fact]
    public void SelectingAPlayerTheGroupHidesShowsAllPlayers()
    {
        var session = GameTestData.StartSession();
        var roster = new TeamRosterViewModel(session, session.ManagedTeam.Id);
        var goalie = roster.Goalies[0].Player.Id;
        roster.ShowGroupCommand.Execute(RosterGroup.Defence);

        roster.SelectPlayer(goalie);

        Assert.Equal(RosterGroup.AllPlayers, roster.Group);
        Assert.Equal(goalie, roster.SelectedGoalie!.Player.Id);
    }

    [Fact]
    public void TheStripSummarisesASkater()
    {
        var roster = StartRoster();
        var row = roster.Skaters.First(candidate => candidate.Player.Position == Position.Centre);
        roster.SelectPlayer(row.Player.Id);
        var player = row.Player;
        var strip = roster.SelectedStrip!;

        Assert.Equal($"#{player.Number}", strip.Number);
        Assert.Equal(PlayerDisplay.FullName(player), strip.Name);
        Assert.Equal("Centre", strip.Position);
        Assert.Equal(
            [
                ("SHOOTS", PlayerDisplay.HandednessName(player.Biography.Handedness)),
                ("AGE", player.Age.ToString(System.Globalization.CultureInfo.CurrentCulture)),
                ("HEIGHT", PlayerDisplay.FormatHeight(player.Biography.Height)),
                ("WEIGHT", PlayerDisplay.FormatWeight(player.Biography.Weight)),
                ("NATIONALITY", PlayerDisplay.CountryName(player.Biography.Nationality)),
            ],
            strip.Facts.Select(fact => (fact.Label, fact.Value)));
        Assert.Equal(
            ["OVR", "SKA", "SHP", "SHA", "PUC", "PAS", "OFF", "DEF", "CHK", "BLK", "STK"],
            strip.Ratings.Select(rating => rating.Label));
        Assert.Equal(
            [player.Overall, player.Ratings[Rating.Skating], player.Ratings[Rating.ShotPower], player.Ratings[Rating.ShotAccuracy],
                player.Ratings[Rating.PuckControl], player.Ratings[Rating.Passing], player.Ratings[Rating.OffensiveAwareness],
                player.Ratings[Rating.DefensiveAwareness], player.Ratings[Rating.Checking], player.Ratings[Rating.ShotBlocking],
                player.Ratings[Rating.StickChecking]],
            strip.Ratings.Select(rating => rating.Value));
        Assert.False(strip.HasInjury);
        Assert.Null(strip.InjuryMarker);
    }

    [Fact]
    public void TheStripSummarisesAGoalie()
    {
        var roster = StartRoster();
        var row = roster.Goalies[0];
        roster.SelectPlayer(row.Player.Id);
        var player = row.Player;
        var strip = roster.SelectedStrip!;

        Assert.Equal("Goalie", strip.Position);
        Assert.Equal("CATCHES", strip.Facts[0].Label);
        Assert.Equal(["CATCHES", "AGE", "HEIGHT", "WEIGHT", "NATIONALITY"], strip.Facts.Select(fact => fact.Label));
        Assert.Equal(["OVR", "REF", "POS", "REB"], strip.Ratings.Select(rating => rating.Label));
        Assert.Equal(
            [player.Overall, player.Ratings[Rating.GoalieReflex], player.Ratings[Rating.GoaliePositioning], player.Ratings[Rating.GoalieReboundControl]],
            strip.Ratings.Select(rating => rating.Value));
        Assert.Equal(PlayerDisplay.FullName(player), strip.Name);
    }

    [Fact]
    public void TheStripMarksAnInjuredPlayerWithoutHiddenValues()
    {
        var session = SessionWhereAPlayerCannotPlay();
        var outPlayer = session.ManagedTeam.Roster.First(player => !player.CanPlay);
        var roster = new TeamRosterViewModel(session, session.ManagedTeam.Id, outPlayer.Id);
        var strip = roster.SelectedStrip!;

        Assert.True(strip.HasInjury);
        Assert.True(strip.IsOut);
        Assert.False(strip.IsPlayingHurt);
        Assert.Equal("OUT", strip.InjuryMarker);
        Assert.Equal(InjuryDisplay.Summary(outPlayer), strip.InjurySummary);
        Assert.Contains("expected back", strip.InjurySummary, StringComparison.Ordinal);

        // Durability and wear are hidden, so the strip has no such figure.
        var text = strip.Facts.Select(fact => fact.Label).Concat(strip.Ratings.Select(rating => rating.Name)).Append(strip.InjurySummary);
        Assert.DoesNotContain(text, value =>
            value.Contains("durab", StringComparison.OrdinalIgnoreCase) || value.Contains("wear", StringComparison.OrdinalIgnoreCase));

        var healthy = new TeamRosterViewModel(session, session.ManagedTeam.Id, session.ManagedTeam.Roster.First(player => player.Injuries.Count == 0).Id);
        Assert.False(healthy.SelectedStrip!.HasInjury);
    }

    [Fact]
    public async Task ExpectedSavePercentageIsWhatAnAverageGoalieWouldSave()
    {
        var (session, shell) = await PlayDays(2);
        var roster = shell.Roster.Roster;
        roster.ShowColumnsCommand.Execute(RosterColumns.Advanced);
        var starter = roster.Goalies.Single(row => row.Player.Id == session.ManagedTeam.Lineup.StartingGoalieId);
        var statistics = session.Snapshot.Season.GoalieStatistics.Single(goalie => goalie.PlayerId == starter.Player.Id);

        Assert.True(statistics.ShotsAgainst > 0);
        var expected = 1 - statistics.ExpectedGoalsAgainst / statistics.ShotsAgainst;
        Assert.Equal(expected, starter.Season.ExpectedSavePercentageValue);
        Assert.Equal(MatchDisplay.SavePercentage(expected), starter.Season.ExpectedSavePercentage);

        var backup = roster.Goalies.Single(row => row.Player.Id == session.ManagedTeam.Lineup.BackupGoalieId);
        Assert.Null(backup.Season.ExpectedSavePercentageValue);
        Assert.Equal("—", backup.Season.ExpectedSavePercentage);

        roster.SortGoaliesCommand.Execute("expected-save-percentage");
        Assert.Equal(starter, roster.Goalies[0]);
        Assert.Null(roster.Goalies[^1].Season.ExpectedSavePercentageValue);
    }

    [Fact]
    public async Task TheLinesLinkOpensTheRosterTeamsLines()
    {
        var (session, shell) = await PlayDays(1);
        var other = shell.Teams.Teams.First(team => !team.IsManaged);
        shell.Teams.SelectTeam(other.Id);
        shell.OpenPageCommand.Execute(ShellPage.Teams);
        Assert.Equal(ShellPage.Teams, shell.CurrentPageKind);

        Assert.True(shell.Teams.Roster.OpenLinesCommand.CanExecute(null));
        shell.Teams.Roster.OpenLinesCommand.Execute(null);

        Assert.Equal(ShellPage.Lines, shell.CurrentPageKind);
        Assert.Equal(other.Id, shell.Lines.SelectedTeam.Id);

        shell.OpenPageCommand.Execute(ShellPage.Roster);
        shell.Roster.Roster.OpenLinesCommand.Execute(null);
        Assert.Equal(ShellPage.Lines, shell.CurrentPageKind);
        Assert.Equal(session.ManagedTeam.Id, shell.Lines.SelectedTeam.Id);
    }

    [Fact]
    public void TheLinesLinkNeedsSomewhereToOpen()
    {
        var roster = StartRoster();

        Assert.False(roster.OpenLinesCommand.CanExecute(null));
    }

    private static TeamRosterViewModel StartRoster()
    {
        var session = GameTestData.StartSession();
        return new TeamRosterViewModel(session, session.ManagedTeam.Id);
    }

    private static IEnumerable<string> Keys(IReadOnlyList<TableColumn<PlayerRowViewModel>> columns) =>
        columns.Select(column => column.Key);

    private static GameSession SessionWhereAPlayerCannotPlay()
    {
        var manager = new GameManager();
        GameTestData.StartSession(manager);
        for (var day = 0; day < MaximumDays; day++)
        {
            if (manager.GetSnapshot().League.Teams.Single(team => team.Id == manager.GetSnapshot().ManagedTeamId).Roster.Any(player => !player.CanPlay))
            {
                return new GameSession(manager, "Injuries");
            }

            manager.AdvanceDay();
        }

        throw new InvalidOperationException("No managed player was ever unable to play.");
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