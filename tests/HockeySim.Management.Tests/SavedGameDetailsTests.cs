using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Saves;

using Xunit;

using static HockeySim.Management.Tests.SeasonAdvancementTests;

namespace HockeySim.Management.Tests;

/// <summary>
/// The details saved for a save's list entry. A team other than the played one is made the
/// managed team by editing the captured save before loading it, so every playoff outcome is
/// checked against one shared played season.
/// </summary>
public sealed class SavedGameDetailsTests(FullSeasonTests.CompletedSeason completed, PlayoffsTests.MidSeriesGame midSeries)
    : IClassFixture<FullSeasonTests.CompletedSeason>, IClassFixture<PlayoffsTests.MidSeriesGame>
{
    [Fact]
    public void ANewGameIsInThePreseasonWithNoRecord()
    {
        var manager = new GameManager();
        var snapshot = StartGame(manager);

        var details = Capture(manager);

        Assert.Equal(SeasonPhase.Preseason, details.Phase);
        Assert.Equal(snapshot.Season.CurrentDate, details.CurrentDate);
        Assert.Equal((0, 0, 0, 0), (details.Wins, details.Losses, details.OvertimeLosses, details.Points));
        Assert.Equal(SavedPlayoffProgress.NotInPlayoffs, details.Playoffs);
        Assert.Null(details.PlayoffRound);
        AssertIdentity(snapshot, details);
    }

    [Fact]
    public void AGameInTheRegularSeasonHasTheTeamsRecordAndDivisionRank()
    {
        var manager = new GameManager();
        StartAtOpeningDay(manager);
        var snapshot = manager.GetSnapshot();
        for (var day = 0; day < 30; day++)
        {
            snapshot = manager.AdvanceDayReplacingInjured();
        }


        var details = Capture(manager);

        var record = snapshot.Season.TeamRecords.Single(record => record.TeamId == snapshot.ManagedTeamId);
        Assert.Equal(SeasonPhase.RegularSeason, details.Phase);
        Assert.Equal(snapshot.Season.CurrentDate, details.CurrentDate);
        Assert.True(record.GamesPlayed > 0);
        Assert.Equal(record.Wins, details.Wins);
        Assert.Equal(record.RegulationLosses, details.Losses);
        Assert.Equal(record.OvertimeLosses + record.ShootoutLosses, details.OvertimeLosses);
        Assert.Equal(record.Points, details.Points);
        Assert.Equal(DivisionEntry(snapshot, snapshot.ManagedTeamId).Rank, details.DivisionRank);
        Assert.Equal(SavedPlayoffProgress.NotInPlayoffs, details.Playoffs);
        AssertIdentity(snapshot, details);
    }

    [Fact]
    public void ATeamThatWonTheFinalIsTheChampion()
    {
        var champion = completed.Snapshot.Season.Playoffs!.ChampionId!.Value;

        var details = CaptureAs(champion);

        Assert.Equal(SavedPlayoffProgress.Champion, details.Playoffs);
        Assert.Equal(PlayoffRound.Final, details.PlayoffRound);
        AssertRegularSeasonFinish(champion, details);
    }

    [Fact]
    public void TheTeamThatLostTheFinalIsEliminatedInTheFinal()
    {
        var final = completed.Snapshot.Season.Playoffs!.Series[^1];
        var runnerUp = final.HigherRanked.TeamId == final.WinnerId ? final.LowerRanked.TeamId : final.HigherRanked.TeamId;

        var details = CaptureAs(runnerUp);

        Assert.Equal(SavedPlayoffProgress.Eliminated, details.Playoffs);
        Assert.Equal(PlayoffRound.Final, details.PlayoffRound);
    }

    [Fact]
    public void ATeamThatLostInTheFirstRoundIsEliminatedInTheFirstRound()
    {
        var series = completed.Snapshot.Season.Playoffs!.Series.First(series => series.Round == PlayoffRound.FirstRound);
        var loser = series.HigherRanked.TeamId == series.WinnerId ? series.LowerRanked.TeamId : series.HigherRanked.TeamId;

        var details = CaptureAs(loser);

        Assert.Equal(SavedPlayoffProgress.Eliminated, details.Playoffs);
        Assert.Equal(PlayoffRound.FirstRound, details.PlayoffRound);
        AssertRegularSeasonFinish(loser, details);
    }

    [Fact]
    public void ATeamThatMissedThePlayoffsIsNotInThemAndKeepsItsDivisionRank()
    {
        var qualifiers = completed.Snapshot.Season.Playoffs!.Series
            .SelectMany(series => new[] { series.HigherRanked.TeamId, series.LowerRanked.TeamId })
            .ToHashSet();
        var missed = completed.Snapshot.League.Teams.Select(team => team.Id).First(id => !qualifiers.Contains(id));

        var details = CaptureAs(missed);

        Assert.Equal(SeasonPhase.Playoffs, details.Phase);
        Assert.Equal(SavedPlayoffProgress.NotInPlayoffs, details.Playoffs);
        Assert.Null(details.PlayoffRound);
        AssertRegularSeasonFinish(missed, details);
    }

    [Fact]
    public void ATeamPlayingAFirstRoundSeriesIsAlive()
    {
        var seed = midSeries.Saved.Season.Playoffs!.Series[0].HigherRanked.TeamId;

        var details = CaptureAs(seed, midSeries.Save);

        Assert.Equal(SeasonPhase.Playoffs, details.Phase);
        Assert.Equal(SavedPlayoffProgress.Alive, details.Playoffs);
        Assert.Equal(PlayoffRound.FirstRound, details.PlayoffRound);
    }

    private static SavedGameDetails Capture(GameManager manager)
    {
        var store = new MemorySaveStore();
        manager.SaveGame(store);
        return store.SavedDetails!;
    }

    private SavedGameDetails CaptureAs(TeamId teamId)
    {
        var store = new MemorySaveStore();
        completed.Manager.SaveGame(store);
        return CaptureAs(teamId, store.Saved!);
    }

    private static SavedGameDetails CaptureAs(TeamId teamId, GameSave save)
    {
        var manager = new GameManager();
        manager.LoadGame(new MemorySaveStore(save with { ManagedTeamId = teamId }));
        return Capture(manager);
    }

    private void AssertRegularSeasonFinish(TeamId teamId, SavedGameDetails details)
    {
        var snapshot = completed.Snapshot;
        var record = snapshot.Season.TeamRecords.Single(record => record.TeamId == teamId);
        Assert.Equal(record.Wins, details.Wins);
        Assert.Equal(record.RegulationLosses, details.Losses);
        Assert.Equal(record.OvertimeLosses + record.ShootoutLosses, details.OvertimeLosses);
        Assert.Equal(record.Points, details.Points);
        Assert.Equal(DivisionEntry(snapshot, teamId).Rank, details.DivisionRank);
        Assert.Equal(snapshot.League.Teams.Single(team => team.Id == teamId).Name, details.TeamName);
    }

    private static void AssertIdentity(GameSnapshot snapshot, SavedGameDetails details)
    {
        var team = ManagedTeam(snapshot);
        Assert.Equal(team.Name, details.TeamName);
        Assert.Equal(team.Colours.Primary.ToString(), details.PrimaryColour);
        Assert.Equal(team.Colours.Secondary.ToString(), details.SecondaryColour);
        Assert.Equal(DivisionName(snapshot, team.Id), details.DivisionName);
    }

    private static string DivisionName(GameSnapshot snapshot, TeamId teamId) =>
        snapshot.Season.Standings.Conferences
            .SelectMany(conference => conference.Divisions)
            .Single(division => division.Teams.Any(entry => entry.Record.TeamId == teamId)).Name;

    private static StandingsEntrySnapshot DivisionEntry(GameSnapshot snapshot, TeamId teamId) =>
        snapshot.Season.Standings.Conferences
            .SelectMany(conference => conference.Divisions)
            .SelectMany(division => division.Teams)
            .Single(entry => entry.Record.TeamId == teamId);
}