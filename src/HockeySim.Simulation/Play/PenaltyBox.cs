using HockeySim.Simulation.Events;

namespace HockeySim.Simulation.Play;

/// <summary>
/// The timed penalties being served in a match, by both teams. Penalty time runs only while play
/// is on, and carries over from one period to the next.
/// </summary>
/// <remarks>
/// <para>
/// A team serves at most <see cref="MaximumShorthanded"/> penalties that leave it short at once,
/// so it never drops below three skaters; a further one waits and starts, at its full length,
/// when one of those ends. Coincidental penalties and misconducts never leave a team short, and
/// their clocks run at once, except that a misconduct starts only once the player's other
/// penalties are over.
/// </para>
/// <para>
/// A skater is off the ice from the moment a penalty is assessed until every penalty they were
/// given has ended, including any still waiting to start.
/// </para>
/// </remarks>
internal sealed class PenaltyBox
{
    public const int MaximumShorthanded = 2;

    private readonly List<ServedPenalty> _penalties = [];

    /// <summary>Any penalty is being served or waiting, so a new pair cannot be four-on-four.</summary>
    public bool HasManpowerPenalties => _penalties.Exists(penalty => penalty.AffectsManpower);

    /// <summary>The seconds until the next running penalty ends, if any is running.</summary>
    public int? SecondsUntilNextExpiry
    {
        get
        {
            int? soonest = null;
            foreach (var penalty in _penalties)
            {
                if (penalty.IsRunning && (soonest is null || penalty.RemainingSeconds < soonest))
                {
                    soonest = penalty.RemainingSeconds;
                }
            }

            return soonest;
        }
    }

    public void Add(ServedPenalty penalty)
    {
        _penalties.Add(penalty);
        StartWaitingPenalties();
    }

    /// <summary>The running penalties that leave the side short.</summary>
    public IEnumerable<ServedPenalty> RunningManpowerPenalties(MatchSide side) =>
        _penalties.Where(penalty => penalty.Side == side && penalty.AffectsManpower && penalty.IsRunning);

    public int ShorthandedBy(MatchSide side) => RunningManpowerPenalties(side).Count();

    public bool IsServing(SkaterState skater) => _penalties.Exists(penalty => penalty.Player == skater);

    public void Elapse(int seconds)
    {
        foreach (var penalty in _penalties)
        {
            if (penalty.IsRunning)
            {
                penalty.RemainingSeconds -= seconds;
            }
        }
    }

    /// <summary>Ends every penalty whose time is up and starts any waiting. Returns whether any ended.</summary>
    public bool ExpirePenalties()
    {
        var expired = _penalties.RemoveAll(penalty => penalty.IsRunning && penalty.RemainingSeconds <= 0);
        if (expired == 0)
        {
            return false;
        }

        StartWaitingPenalties();
        return true;
    }

    /// <summary>
    /// Ends the running minor that would expire first after the side concedes a power-play goal.
    /// A double minor ends only the minor being served: one with more than two minutes left
    /// starts its second minor afresh. Majors are always served in full.
    /// </summary>
    public bool EndMinorAfterPowerPlayGoal(MatchSide side)
    {
        var minor = RunningManpowerPenalties(side)
            .Where(penalty => penalty.Kind is PenaltyKind.Minor or PenaltyKind.DoubleMinor)
            .OrderBy(penalty => penalty.CurrentMinorRemainingSeconds)
            .FirstOrDefault();
        if (minor is null)
        {
            return false;
        }

        if (minor.Kind == PenaltyKind.DoubleMinor && minor.RemainingSeconds > ServedPenalty.MinorSeconds)
        {
            minor.RemainingSeconds = ServedPenalty.MinorSeconds;
            return false;
        }

        minor.RemainingSeconds = 0;
        return ExpirePenalties();
    }

    private void StartWaitingPenalties()
    {
        foreach (var penalty in _penalties)
        {
            if (penalty.IsRunning)
            {
                continue;
            }

            if (penalty.AffectsManpower)
            {
                penalty.IsRunning = ShorthandedBy(penalty.Side) < MaximumShorthanded;
            }
            else if (penalty.Kind == PenaltyKind.Misconduct)
            {
                penalty.IsRunning = !_penalties.Exists(other =>
                    other != penalty && other.Player == penalty.Player && other.Kind != PenaltyKind.Misconduct);
            }
            else
            {
                penalty.IsRunning = true;
            }
        }
    }
}

/// <summary>One timed penalty in the box.</summary>
internal sealed class ServedPenalty
{
    public const int MinorSeconds = 2 * 60;

    /// <param name="affectsManpower">
    /// Whether the team plays a skater short while it is served; false for coincidental penalties.
    /// </param>
    public ServedPenalty(MatchSide side, SkaterState player, PenaltyKind kind, bool affectsManpower)
    {
        if (kind is PenaltyKind.GameMisconduct or PenaltyKind.PenaltyShot)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Only timed penalties are served in the box.");
        }

        Side = side;
        Player = player;
        Kind = kind;
        AffectsManpower = affectsManpower && kind != PenaltyKind.Misconduct;
        RemainingSeconds = PenaltyEvent.MinutesFor(kind) * 60;
    }

    public MatchSide Side { get; }

    public SkaterState Player { get; }

    public PenaltyKind Kind { get; }

    public bool AffectsManpower { get; }

    public bool IsRunning { get; set; }

    public int RemainingSeconds { get; set; }

    /// <summary>The time left in the minor being served now: a double minor serves two in turn.</summary>
    public int CurrentMinorRemainingSeconds =>
        RemainingSeconds > MinorSeconds ? RemainingSeconds - MinorSeconds : RemainingSeconds;

    /// <summary>Already counted as a power-play opportunity for the other team.</summary>
    public bool CountedAsPowerPlay { get; set; }
}