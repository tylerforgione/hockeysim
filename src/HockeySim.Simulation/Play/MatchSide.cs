using HockeySim.Domain;

namespace HockeySim.Simulation.Play;

/// <summary>
/// One team's transient state during a simulated match. The lineup is captured once at match
/// start, so the eligible players stay fixed for the whole match.
/// </summary>
/// <remarks>
/// Each strength state has its own rotation: the forward lines and defence pairs at five-on-five,
/// and the lineup's units for every special situation. A skater who is serving a penalty or has
/// been ejected cannot go on, so the most rested available skater of the same kind (forward or
/// defence) takes their place in the group.
/// </remarks>
internal sealed class MatchSide
{
    private readonly PenaltyBox _penaltyBox;
    private readonly Dictionary<Player, SkaterState> _skatersByPlayer;
    private readonly IReadOnlyList<SkaterState> _extraAttackers;
    private readonly IReadOnlyList<SkaterState> _shootoutOrder;
    private readonly Rotation[] _evenStrength;
    private readonly Dictionary<SpecialSituation, Rotation[]> _specialSituations;
    private Rotation[] _active;
    private SpecialSituation? _situation;
    private OnIceSkater[]? _onIce;
    private double[] _onIceDrain = [];
    private int? _availableSkaterCount;

    public MatchSide(Team team, PenaltyBox penaltyBox)
    {
        var lineup = team.Lineup;

        _penaltyBox = penaltyBox;
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
        _extraAttackers = lineup.ExtraAttackers.Select(player => _skatersByPlayer[player]).ToList();

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

        _specialSituations = SpecialSituationFormat.All.ToDictionary(
            format => format.Situation,
            format => new[]
            {
                new Rotation(
                    lineup.UnitsFor(format.Situation).Select(unit => unit.Players
                        .Select((player, slot) => Slot(player, unit.Format.Roles[slot]))
                        .ToArray()),
                    MatchTuning.UnitUsage(format.Situation),
                    drainMultiplier: 1),
            });
        _active = _evenStrength;

        // OrderByDescending is stable, so equally rated shooters keep their lineup order and
        // the shootout order is deterministic.
        _shootoutOrder = Skaters.OrderByDescending(skater => PlayerStrength.Shootout(skater.Player)).ToList();
    }

    public TeamId TeamId { get; }

    public Player Goalie { get; }

    /// <summary>The goalie's ability to stop shots on goal.</summary>
    public double Saving { get; }

    public double ReboundControl { get; }

    /// <summary>The goalie's overall strength in net, used in the shootout and on penalty shots.</summary>
    public double Goaltending { get; }

    /// <summary>The dressed skaters in lineup order: forward lines, then defence pairs.</summary>
    public IReadOnlyList<SkaterState> Skaters { get; }

    /// <summary>
    /// The shootout shooters in order: dressed skaters by shootout strength, without anyone ejected
    /// or still serving a penalty when overtime ends.
    /// </summary>
    public IReadOnlyList<Player> ShootoutOrder =>
        _shootoutOrder.Where(IsAvailable).Select(skater => skater.Player).ToList();

    /// <summary>
    /// The goalie is on the bench for an extra attacker, during a delayed penalty or late in a match
    /// the team is losing. Either way there is one extra attacker.
    /// </summary>
    public bool IsGoaliePulled => IsGoaliePulledForDelayedPenalty || IsGoaliePulledToTieTheMatch;

    /// <summary>Pulled while the opponent's delayed penalty is pending.</summary>
    public bool IsGoaliePulledForDelayedPenalty { get; private set; }

    /// <summary>Pulled late in regulation because the team is trailing.</summary>
    public bool IsGoaliePulledToTieTheMatch { get; private set; }

    public int GoalieTimeOnIceSeconds { get; private set; }

    /// <inheritdoc cref="MatchTeamResult.PowerPlayOpportunities"/>
    public int PowerPlayOpportunities { get; private set; }

    /// <summary>Skaters who could go on the ice: neither serving a penalty nor ejected.</summary>
    public int AvailableSkaterCount => _availableSkaterCount ??= Skaters.Count(IsAvailable);

    /// <summary>The skaters on the ice now, each once, forwards before defence, then any extra attacker.</summary>
    public IReadOnlyList<OnIceSkater> OnIce => _onIce ??= ComposeOnIce();

    public OnIceSkater Centre => OnIce.First(slot => slot.Role == SkaterRole.Centre);

    public void RestForIntermission()
    {
        foreach (var skater in Skaters)
        {
            skater.RestForIntermission();
        }
    }

    /// <summary>
    /// Plays the strength state with the group that suits it. A new period, or a change of strength
    /// state, sends out a group from the new rotation; otherwise the players on the ice stay.
    /// </summary>
    /// <param name="skaters">The skaters this side may have on the ice, not counting an extra attacker.</param>
    /// <param name="opponentSkaters">The skaters the opponent may have on the ice.</param>
    public void SetStrength(int skaters, int opponentSkaters, bool startOfPeriod)
    {
        var situation = SituationFor(skaters, opponentSkaters);
        if (startOfPeriod || situation != _situation)
        {
            _situation = situation;
            _active = situation is { } special ? _specialSituations[special] : _evenStrength;
            foreach (var rotation in _active)
            {
                rotation.Deploy();
            }
        }

        // Penalties may have changed who is available.
        _onIce = null;
        _availableSkaterCount = null;
    }

    public void PullGoalieForDelayedPenalty(bool pulled)
    {
        IsGoaliePulledForDelayedPenalty = pulled;
        _onIce = null;
    }

    public void PullGoalieToTieTheMatch(bool pulled)
    {
        IsGoaliePulledToTieTheMatch = pulled;
        _onIce = null;
    }

    public void AddPowerPlayOpportunity() => PowerPlayOpportunities++;

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

        var onIce = OnIce;
        foreach (var skater in Skaters)
        {
            if (!IsOnIce(skater))
            {
                skater.Rest(seconds);
            }
        }

        for (var index = 0; index < onIce.Count; index++)
        {
            onIce[index].Skater.Skate(seconds, _onIceDrain[index]);
        }

        if (!IsGoaliePulled)
        {
            GoalieTimeOnIceSeconds += seconds;
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

    public bool IsAvailable(SkaterState skater) => !skater.IsEjected && !_penaltyBox.IsServing(skater);

    private static SpecialSituation? SituationFor(int skaters, int opponentSkaters) => (skaters, opponentSkaters) switch
    {
        (5, 5) => null,
        (5, 4) => SpecialSituation.PowerPlay5On4,
        (5, 3) => SpecialSituation.PowerPlay5On3,
        (4, 3) => SpecialSituation.PowerPlay4On3,
        (4, 5) => SpecialSituation.PenaltyKill4On5,
        (3, 5) => SpecialSituation.PenaltyKill3On5,
        (3, 4) => SpecialSituation.PenaltyKill3On4,
        (4, 4) => SpecialSituation.FourOnFour,
        (3, 3) => SpecialSituation.ThreeOnThree,
        _ => throw new ArgumentOutOfRangeException(
            nameof(skaters),
            $"No strength state has {skaters} skaters against {opponentSkaters}."),
    };

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

    /// <summary>
    /// The active groups' skaters, with a substitute for anyone unavailable or already on the ice
    /// in another group, then the extra attacker when the goalie is pulled.
    /// </summary>
    private OnIceSkater[] ComposeOnIce()
    {
        var planned = new List<(OnIceSkater Slot, double Drain)>();
        foreach (var rotation in _active)
        {
            foreach (var slot in rotation.OnIce)
            {
                planned.Add((slot, rotation.DrainMultiplier));
            }
        }

        var onIce = new List<OnIceSkater>(planned.Count + 1);
        var drains = new List<double>(planned.Count + 1);
        foreach (var (slot, drain) in planned)
        {
            var skater = IsAvailable(slot.Skater) && !Contains(onIce, slot.Skater)
                ? slot.Skater
                : Substitute(onIce, planned, slot.Role);
            onIce.Add(new OnIceSkater(skater, slot.Role));
            drains.Add(drain);
        }

        if (IsGoaliePulled)
        {
            var extraAttacker = _extraAttackers.FirstOrDefault(skater => IsAvailable(skater) && !Contains(onIce, skater))
                ?? Substitute(onIce, planned, SkaterRole.Wing);
            onIce.Add(new OnIceSkater(extraAttacker, SkaterRole.Wing));
            drains.Add(1);
        }

        _onIceDrain = drains.ToArray();
        return onIce.ToArray();
    }

    /// <summary>
    /// The most rested available skater who is not on the ice or due on with the group, preferring
    /// defence for a defence slot and forwards otherwise.
    /// </summary>
    private SkaterState Substitute(List<OnIceSkater> onIce, List<(OnIceSkater Slot, double Drain)> planned, SkaterRole role)
    {
        SkaterState? best = null;
        var bestMatchesRole = false;
        foreach (var skater in Skaters)
        {
            if (!IsAvailable(skater) || Contains(onIce, skater) || planned.Exists(entry => entry.Slot.Skater == skater))
            {
                continue;
            }

            var matchesRole = (skater.Player.Position == Position.Defence) == (role == SkaterRole.Defence);
            if (best is null
                || (matchesRole && !bestMatchesRole)
                || (matchesRole == bestMatchesRole && skater.Energy > best.Energy))
            {
                best = skater;
                bestMatchesRole = matchesRole;
            }
        }

        // Penalties are not called on a team short of available skaters, so this cannot happen.
        return best ?? throw new InvalidOperationException("No skater is available to go on the ice.");
    }

    private static bool Contains(List<OnIceSkater> onIce, SkaterState skater) => onIce.Exists(slot => slot.Skater == skater);

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

    private OnIceSkater Slot(Player player, SkaterRole role) => new(_skatersByPlayer[player], role);
}