namespace HockeySim.Simulation.Play;

/// <summary>
/// The groups a coach rotates through one set of positions: the forward lines, the defence pairs,
/// or the units for one special situation. One group is on the ice at a time.
/// </summary>
/// <remarks>
/// Each group has a target share of the rotation's ice time. When the group on the ice is due to
/// change, the coach sends out the rested group furthest behind its target share, so stronger
/// groups play more without anyone skating until exhausted. A low-stamina group tires sooner,
/// so its shifts are shorter and it plays less.
/// </remarks>
internal sealed class Rotation
{
    private readonly OnIceSkater[][] _groups;
    private readonly double[] _usage;
    private readonly int[] _deployedSeconds;
    private int _elapsedSeconds;

    public Rotation(IEnumerable<OnIceSkater[]> groups, double[] usage, double drainMultiplier)
    {
        _groups = groups.ToArray();
        _usage = usage;
        _deployedSeconds = new int[_groups.Length];
        DrainMultiplier = drainMultiplier;
    }

    public double DrainMultiplier { get; }

    public IReadOnlyList<OnIceSkater> OnIce => _groups[Current];

    public int Current { get; private set; }

    public int ShiftSeconds { get; private set; }

    /// <summary>The group on the ice is tired or has been out as long as a shift should last.</summary>
    public bool IsDue => ShiftSeconds >= MatchTuning.LongestShiftSeconds || MeanEnergy(Current) < MatchTuning.TiredEnergy;

    /// <summary>The group on the ice has been out long enough to change at a stoppage.</summary>
    public bool ShouldChangeAtStoppage => IsDue || ShiftSeconds >= MatchTuning.StoppageChangeSeconds;

    public void Elapse(int seconds)
    {
        ShiftSeconds += seconds;
        _deployedSeconds[Current] += seconds;
        _elapsedSeconds += seconds;
    }

    /// <summary>
    /// Sends out a group at the start of a period or when its strength state begins; any group may
    /// go, including the one that was last on the ice.
    /// </summary>
    public void Deploy() => SendOut(ChooseGroup(excluded: -1));

    /// <summary>Replaces the group on the ice with the next one.</summary>
    public void Change() => SendOut(ChooseGroup(excluded: Current));

    private void SendOut(int group)
    {
        Current = group;
        ShiftSeconds = 0;
    }

    private int ChooseGroup(int excluded)
    {
        var best = -1;
        for (var group = 0; group < _groups.Length; group++)
        {
            if (group == excluded || MeanEnergy(group) < MatchTuning.ReadyEnergy)
            {
                continue;
            }

            if (best < 0 || Deficit(group) > Deficit(best))
            {
                best = group;
            }
        }

        if (best >= 0)
        {
            return best;
        }

        // Nobody is ready: send out the most rested group.
        for (var group = 0; group < _groups.Length; group++)
        {
            if (group != excluded && (best < 0 || MeanEnergy(group) > MeanEnergy(best)))
            {
                best = group;
            }
        }

        return best < 0 ? Current : best;
    }

    private double Deficit(int group) => (_usage[group] * _elapsedSeconds) - _deployedSeconds[group];

    private double MeanEnergy(int group)
    {
        var total = 0.0;
        foreach (var slot in _groups[group])
        {
            total += slot.Skater.Energy;
        }

        return total / _groups[group].Length;
    }
}