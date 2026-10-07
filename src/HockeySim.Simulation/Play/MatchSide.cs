using HockeySim.Domain;

namespace HockeySim.Simulation.Play;

/// <summary>
/// One team's transient state during a simulated match. The lineup is captured once at match
/// start, so the eligible players stay fixed for the whole match.
/// </summary>
internal sealed class MatchSide
{
    private readonly Dictionary<Player, SkaterState> _skatersByPlayer;
    private readonly Rotation[] _evenStrength;
    private readonly Rotation[] _threeOnThree;
    private Rotation[] _active;
    private OnIceSkater[]? _onIce;

    public MatchSide(Team team)
    {
        var lineup = team.Lineup;

        TeamId = team.Id;
        Goalie = lineup.StartingGoalie;
        Saving = PlayerStrength.Saving(Goalie);
        ReboundControl = Goalie.GetRating(Rating.GoalieReboundControl).Value;
        Goaltending = PlayerStrength.Goaltending(Goalie);

        Skaters = lineup.ForwardLines
            .SelectMany(line => line.Players)
            .Concat(lineup.DefencePairs.SelectMany(pair => pair.Players))
            .Select(player => new SkaterState(player))
            .ToList();
        _skatersByPlayer = Skaters.ToDictionary(skater => skater.Player);

        var forwards = new Rotation(
            lineup.ForwardLines.Select(line => new[]
            {
                Slot(line.LeftWing, SkaterRole.Wing),
                Slot(line.Centre, SkaterRole.Centre),
                Slot(line.RightWing, SkaterRole.Wing),
            }),
            MatchTuning.ForwardLineUsage,
            drainMultiplier: 1);
        var defence = new Rotation(
            lineup.DefencePairs.Select(pair => new[]
            {
                Slot(pair.LeftDefence, SkaterRole.Defence),
                Slot(pair.RightDefence, SkaterRole.Defence),
            }),
            MatchTuning.DefencePairUsage,
            MatchTuning.DefenceDrainMultiplier);
        _evenStrength = [forwards, defence];

        var threeOnThreeUnits = lineup.UnitsFor(SpecialSituation.ThreeOnThree)
            .Select(unit => unit.Players
                .Select((player, slot) => Slot(player, unit.Format.Roles[slot]))
                .ToArray());
        _threeOnThree = [new Rotation(threeOnThreeUnits, MatchTuning.ThreeOnThreeUnitUsage, drainMultiplier: 1)];
        _active = _evenStrength;

        // OrderByDescending is stable, so equally rated shooters keep their lineup order and
        // the shootout order is deterministic.
        ShootoutOrder = Skaters.Select(skater => skater.Player).OrderByDescending(PlayerStrength.Shootout).ToList();
    }

    public TeamId TeamId { get; }

    public Player Goalie { get; }

    /// <summary>The goalie's ability to stop shots on goal.</summary>
    public double Saving { get; }

    public double ReboundControl { get; }

    /// <summary>The goalie's overall strength in net, used in the shootout.</summary>
    public double Goaltending { get; }

    /// <summary>The dressed skaters in lineup order: forward lines, then defence pairs.</summary>
    public IReadOnlyList<SkaterState> Skaters { get; }

    public IReadOnlyList<Player> ShootoutOrder { get; }

    /// <summary>The skaters on the ice now, each once, forwards before defence.</summary>
    public IReadOnlyList<OnIceSkater> OnIce => _onIce ??= _active
        .SelectMany(rotation => rotation.OnIce)
        .DistinctBy(slot => slot.Skater)
        .ToArray();

    public OnIceSkater Centre => OnIce.First(slot => slot.Role == SkaterRole.Centre);

    /// <summary>Starts a period at five-on-five or three-on-three, sending out the starting groups.</summary>
    public void StartPeriod(bool threeOnThree, bool afterIntermission)
    {
        if (afterIntermission)
        {
            foreach (var skater in Skaters)
            {
                skater.RestForIntermission();
            }
        }

        _active = threeOnThree ? _threeOnThree : _evenStrength;
        foreach (var rotation in _active)
        {
            rotation.StartPeriod();
        }

        _onIce = null;
    }

    /// <summary>Changes every group that is due. Returns whether anyone changed.</summary>
    public bool ChangeOnTheFly() => Change(rotation => rotation.IsDue);

    /// <summary>Changes every group that has been out a while, as coaches do at a stoppage.</summary>
    public bool ChangeAtStoppage() => Change(rotation => rotation.ShouldChangeAtStoppage);

    /// <summary>Plays time: the skaters on the ice tire and the rest recover on the bench.</summary>
    public void Elapse(int seconds)
    {
        foreach (var rotation in _active)
        {
            rotation.Elapse(seconds);
        }

        foreach (var skater in Skaters)
        {
            if (!IsOnIce(skater))
            {
                skater.Rest(seconds);
            }
        }

        // A skater in two groups on the ice at once (possible in three-on-three units) skates once.
        foreach (var slot in OnIce)
        {
            slot.Skater.Skate(seconds, DrainMultiplierFor(slot.Skater));
        }
    }

    public double MeanOnIce(Func<SkaterState, double> strength)
    {
        var onIce = OnIce;
        var total = 0.0;
        foreach (var slot in onIce)
        {
            total += strength(slot.Skater) * slot.Skater.Performance;
        }

        return total / onIce.Count;
    }

    private bool Change(Func<Rotation, bool> shouldChange)
    {
        var changed = false;
        foreach (var rotation in _active)
        {
            if (shouldChange(rotation))
            {
                rotation.Change();
                changed = true;
            }
        }

        if (changed)
        {
            _onIce = null;
        }

        return changed;
    }

    private bool IsOnIce(SkaterState skater)
    {
        foreach (var slot in OnIce)
        {
            if (slot.Skater == skater)
            {
                return true;
            }
        }

        return false;
    }

    private double DrainMultiplierFor(SkaterState skater)
    {
        foreach (var rotation in _active)
        {
            foreach (var slot in rotation.OnIce)
            {
                if (slot.Skater == skater)
                {
                    return rotation.DrainMultiplier;
                }
            }
        }

        return 1;
    }

    private OnIceSkater Slot(Player player, SkaterRole role) => new(_skatersByPlayer[player], role);
}