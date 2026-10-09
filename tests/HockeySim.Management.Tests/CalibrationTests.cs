using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Simulation;
using HockeySim.Simulation.Randomness;

using Xunit;

using static HockeySim.Management.Tests.SeasonAdvancementTests;

namespace HockeySim.Management.Tests;

/// <summary>
/// Plays one generated league's regular season and checks its league averages against the NHL
/// calibration targets. The season is played once and shared.
/// </summary>
public sealed class CalibrationTests(CalibrationTests.CalibratedSeason season)
    : IClassFixture<CalibrationTests.CalibratedSeason>
{
    public static TheoryData<string> TargetNames { get; } = new(NhlTargets.All.Select(target => target.Name));

    [Theory]
    [MemberData(nameof(TargetNames))]
    public void ALeagueAverageIsCloseToItsNhlTarget(string name)
    {
        var target = NhlTargets.All.Single(target => target.Name == name);

        Assert.InRange(target.Measure(season.Measurements), target.Lowest, target.Highest);
    }

    /// <summary>Writes every measurement beside its target to the test output, for tuning.</summary>
    [Fact]
    public void ReportsEveryMeasurementAgainstItsTarget()
    {
        foreach (var target in NhlTargets.All)
        {
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"{target.Name,-50} {target.Measure(season.Measurements),8:0.000}  NHL {target.Nhl,8:0.000} ± {target.Tolerance:0.000}");
        }

        Assert.Equal(1344, season.Results.Count);
    }

    public sealed class CalibratedSeason
    {
        // Far more than the calendar needs, so a season that never completes fails instead of hanging.
        private const int MaximumAdvances = 366;

        public CalibratedSeason()
        {
            var simulator = new RecordingMatchSimulator();
            var manager = new GameManager(simulator);
            var snapshot = StartGame(manager, seed: 2026);
            for (var advances = 0; !snapshot.Season.IsComplete && advances < MaximumAdvances; advances++)
            {
                snapshot = manager.AdvanceDayReplacingInjured();
            }

            Results = simulator.Results;
            Measurements = new LeagueMeasurements(
                Results, snapshot.Season.TeamRecords, simulator.PlayersOut, simulator.SidesPlayingHurt);
        }

        public IReadOnlyList<MatchResult> Results { get; }

        internal LeagueMeasurements Measurements { get; }
    }

    /// <summary>
    /// Plays matches with the real engine and keeps every full result, play-by-play included, and
    /// how many rostered players were out injured for each match.
    /// </summary>
    private sealed class RecordingMatchSimulator : IMatchSimulator
    {
        private readonly MatchSimulator _simulator = new();
        private readonly List<MatchResult> _results = [];

        public IReadOnlyList<MatchResult> Results => _results;

        /// <summary>Rostered players who could not play, summed over both teams of every match.</summary>
        public int PlayersOut { get; private set; }

        /// <summary>Teams that dressed a player playing through an injury, summed over every match.</summary>
        public int SidesPlayingHurt { get; private set; }

        public MatchResult Simulate(Match match, OvertimeFormat overtime, MatchHealth health, RandomState randomState)
        {
            PlayersOut += match.Home.Roster.Concat(match.Away.Roster).Count(player => !health.CanPlay(player.Id));
            SidesPlayingHurt += new[] { match.Home, match.Away }.Count(team => team.Lineup.DressedPlayers.Any(player =>
                health.CanPlay(player.Id) && health.For(player.Id).InjuriesOn(health.Date).Count > 0));
            var result = _simulator.Simulate(match, overtime, health, randomState);
            _results.Add(result);
            return result;
        }
    }
}