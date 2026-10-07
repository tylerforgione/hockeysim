using HockeySim.Domain;
using HockeySim.Simulation;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Desktop.Tests;

/// <summary>
/// Fails every match, so a league day can never be applied.
/// </summary>
internal sealed class FailingMatchSimulator : IMatchSimulator
{
    public const string FailureMessage = "The match engine is unavailable.";

    public MatchResult Simulate(Match match, OvertimeFormat overtime, RandomState randomState) =>
        throw new InvalidOperationException(FailureMessage);
}

/// <summary>
/// Holds the first match of each day until released, so a test can observe a day in progress.
/// </summary>
internal sealed class GatedMatchSimulator : IMatchSimulator, IDisposable
{
    private readonly MatchSimulator _engine = new();
    private readonly ManualResetEventSlim _release = new();
    private readonly ManualResetEventSlim _entered = new();

    public void WaitUntilPlaying()
    {
        if (!_entered.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("The league day did not start.");
        }
    }

    public void Release() => _release.Set();

    public MatchResult Simulate(Match match, OvertimeFormat overtime, RandomState randomState)
    {
        _entered.Set();
        if (!_release.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("The test did not release the league day.");
        }

        return _engine.Simulate(match, overtime, randomState);
    }

    public void Dispose()
    {
        _release.Dispose();
        _entered.Dispose();
    }
}