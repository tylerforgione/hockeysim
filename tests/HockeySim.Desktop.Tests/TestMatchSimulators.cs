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
/// The held match waits for the test, however long the test takes to get there: on a busy CI
/// runner the test's own continuation can wait many seconds for a thread, and a deadline here
/// would fail the day underneath it. Disposing the engine, as a test that ends or fails early
/// does, abandons the day instead. Only the wait for the day to start is timed, generously,
/// because starting the day's background work can take seconds on slow runners.
/// </remarks>
internal sealed class GatedMatchSimulator : IMatchSimulator, IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly MatchSimulator _engine = new();
    private readonly ManualResetEventSlim _release = new();
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _abandoned;

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
        _release.Wait();
        if (_abandoned)
        {
            throw new OperationCanceledException("The test ended without releasing the league day.");
        }

        return _engine.Simulate(match, overtime, health, randomState);
    }

    /// <summary>
    /// Abandons a day still held. The event is left for the collector rather than disposed,
    /// because the background day may still be waking on it.
    /// </summary>
    public void Dispose()
    {
        _abandoned = !_release.IsSet;
        _release.Set();
    }
}
/// <summary>
/// Plays the given number of matches with the real engine, then fails every match after them, so
/// a run of days fails part-way.
/// </summary>
internal sealed class FailingAfterMatchSimulator(int matchesBeforeFailing) : IMatchSimulator
{
    private readonly MatchSimulator _engine = new();
    private int _calls;

    public MatchResult Simulate(Match match, OvertimeFormat overtime, MatchHealth health, RandomState randomState) =>
        ++_calls > matchesBeforeFailing
            ? throw new InvalidOperationException(FailingMatchSimulator.FailureMessage)
            : _engine.Simulate(match, overtime, health, randomState);
}