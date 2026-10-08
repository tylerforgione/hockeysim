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

    public MatchResult Simulate(Match match, OvertimeFormat overtime, MatchHealth health, RandomState randomState) =>
        throw new InvalidOperationException(FailureMessage);
}

/// <summary>
/// Holds the first match of each day until released, so a test can observe a day in progress.
/// </summary>
/// <remarks>
/// The timeouts only bound a broken test; they are generous because CI runs test projects in
/// parallel on slow runners, where starting the day's background work can take seconds.
/// </remarks>
internal sealed class GatedMatchSimulator : IMatchSimulator, IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly MatchSimulator _engine = new();
    private readonly ManualResetEventSlim _release = new();
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task WaitUntilPlayingAsync()
    {
        try
        {
            await _entered.Task.WaitAsync(Timeout);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException("The league day did not start.", exception);
        }
    }

    public void Release() => _release.Set();

    public MatchResult Simulate(Match match, OvertimeFormat overtime, MatchHealth health, RandomState randomState)
    {
        _entered.TrySetResult();
        if (!_release.Wait(Timeout))
        {
            throw new TimeoutException("The test did not release the league day.");
        }

        return _engine.Simulate(match, overtime, health, randomState);
    }

    public void Dispose() => _release.Dispose();
}