using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Saves;
using HockeySim.Simulation;
using HockeySim.Simulation.Randomness;

using Xunit;

using static HockeySim.Management.Tests.SaveTestData;
using static HockeySim.Management.Tests.SeasonAdvancementTests;

namespace HockeySim.Management.Tests;

/// <summary>
/// Playing the playoffs through Management: the overtime format each match is played with, saving
/// in the middle of a series, and continuing a loaded game to the champion. One game is played
/// into the first round and saved, then played on uninterrupted to the end.
/// </summary>
public sealed class PlayoffsTests(PlayoffsTests.MidSeriesGame game) : IClassFixture<PlayoffsTests.MidSeriesGame>
{
    [Fact]
    public void RegularSeasonMatchesUseRegularSeasonOvertimeAndPlayoffMatchesPlayoffOvertime()
    {
        var playoffStart = game.Final.Schedule.PlayoffMatches[0].Date;

        Assert.All(game.Formats.Where(played => played.Date < playoffStart), played => Assert.Equal(OvertimeFormat.RegularSeason, played.Format));
        Assert.All(game.Formats.Where(played => played.Date >= playoffStart), played => Assert.Equal(OvertimeFormat.Playoff, played.Format));
        Assert.Equal(game.Final.Season.Playoffs!.Results.Count, game.Formats.Count(played => played.Format == OvertimeFormat.Playoff));
    }

    [Fact]
    public void AGameSavedInTheMiddleOfASeriesLoadsExactlyAsSaved()
    {
        var loaded = new GameManager().LoadGame(new MemorySaveStore(game.Save));

        Assert.Equal(SeasonPhase.Playoffs, loaded.Season.Phase);
        Assert.Equal(2, loaded.Season.Playoffs!.Series[0].Games.Count);
        Assert.NotNull(loaded.Season.Playoffs.Series[0].NextGame);
        Assert.Equal(Describe(game.Saved), Describe(loaded));
    }

    [Fact]
    public void ContinuingAGameLoadedMidSeriesCrownsTheSameChampionAsUninterruptedPlay()
    {
        var resumed = new GameManager();
        resumed.LoadGame(new MemorySaveStore(game.Save));

        var final = PlayToTheEnd(resumed);

        Assert.True(final.Season.IsComplete);
        Assert.Equal(Describe(game.Final), Describe(final));
        Assert.Throws<InvalidOperationException>(resumed.AdvanceDay);
    }

    [Fact]
    public void ASavedPlayoffResultDecidedByAShootoutIsRejected()
    {
        var first = game.Save.PlayoffMatches[0];
        var corrupt = game.Save with
        {
            PlayoffMatches = [first with { Decision = MatchDecision.Shootout }, .. game.Save.PlayoffMatches.Skip(1)],
        };

        Assert.Throws<InvalidGameSaveException>(() => new GameManager().LoadGame(new MemorySaveStore(corrupt)));
    }

    [Fact]
    public void ASaveMissingAPlayoffResultIsRejected()
    {
        var corrupt = game.Save with { PlayoffMatches = game.Save.PlayoffMatches.SkipLast(1).ToList() };

        var exception = Assert.Throws<InvalidGameSaveException>(() => new GameManager().LoadGame(new MemorySaveStore(corrupt)));
        Assert.Contains("has no result", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayoffSnapshotsAreDetachedFromLaterPlay()
    {
        var playoffs = game.Saved.Season.Playoffs!;

        Assert.Equal(2, playoffs.Series[0].Games.Count);
        Assert.Equal(16, playoffs.Results.Count);
        Assert.Equal(24, game.Saved.Schedule.PlayoffMatches.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<PlayoffSeriesSnapshot>)playoffs.Series).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<CompletedMatchSnapshot>)playoffs.Series[0].Games).Clear());
    }

    private static GameSnapshot PlayToTheEnd(GameManager manager)
    {
        var snapshot = manager.GetSnapshot();
        while (!snapshot.Season.IsComplete)
        {
            snapshot = manager.AdvanceDayReplacingInjured();
        }

        return snapshot;
    }

    /// <summary>
    /// One game played until every first-round series has played two games, saved there, then
    /// played on to the champion. Every match's overtime format is recorded by date.
    /// </summary>
    public sealed class MidSeriesGame
    {
        public MidSeriesGame()
        {
            var simulator = new RecordingSimulator();
            var manager = new GameManager(simulator);
            StartAtOpeningDay(manager, seed: 60);

            var snapshot = manager.GetSnapshot();
            while (snapshot.Season.Playoffs?.Series[0].Games.Count != 2)
            {
                snapshot = manager.AdvanceDayReplacingInjured();
            }

            var store = new MemorySaveStore();
            manager.SaveGame(store);
            Save = store.Saved!;
            Saved = snapshot;
            Final = PlayToTheEnd(manager);
            Formats = simulator.Formats;
        }

        public GameSave Save { get; }

        public GameSnapshot Saved { get; }

        public GameSnapshot Final { get; }

        public IReadOnlyList<(DateOnly Date, OvertimeFormat Format)> Formats { get; }
    }

    private sealed class RecordingSimulator : IMatchSimulator
    {
        private readonly MatchSimulator _engine = new();

        public List<(DateOnly Date, OvertimeFormat Format)> Formats { get; } = [];

        public MatchResult Simulate(Match match, OvertimeFormat overtime, MatchHealth health, RandomState randomState)
        {
            Formats.Add((health.Date, overtime));
            return _engine.Simulate(match, overtime, health, randomState);
        }
    }
}