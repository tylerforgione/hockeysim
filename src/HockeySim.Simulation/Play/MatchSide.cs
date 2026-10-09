using HockeySim.Domain;

namespace HockeySim.Simulation.Play;

/// <summary>
/// One team's transient state during a simulated match. The lineup is captured once at match
/// start, so the eligible players stay fixed for the whole match.
/// </summary>
/// <remarks>
/// Each strength state has its own rotation: the forward lines and defence pairs at five-on-five,
/// and the lineup's units for every special situation. A skater who is serving a penalty, has
/// been ejected, or is out injured cannot go on, so the most rested available skater of the same
/// kind (forward or defence) takes their place in the group. A dressed skater who cannot play on
/// the match date is out from the start and does not appear; if the starting goalie cannot play,
/// the backup starts.
/// </remarks>
internal sealed class MatchSide
{
    private readonly PenaltyBox _penaltyBox;
    private readonly Dictionary<Player, SkaterState> _skatersByPlayer;
    private readonly IReadOnlyList<SkaterState> _extraAttackers;
    private readonly Rotation[] _evenStrength;
    private readonly Dictionary<SpecialSituation, Rotation[]> _specialSituations;
    private Rotation[] _active;
    private SpecialSituation? _situation;
    private OnIceSkater[]? _onIce;
    private double[] _onIceDrain = [];
    private int? _availableSkaterCount;

    public MatchSide(Team team, PenaltyBox penaltyBox, MatchHealth health)
    {
        var lineup = team.Lineup;

        _penaltyBox = penaltyBox;
        TeamId = team.Id;
        Goalie = health.CanPlay(lineup.StartingGoalie.Id) ? lineup.StartingGoalie
            : health.CanPlay(lineup.BackupGoalie.Id) ? lineup.BackupGoalie
            : throw new ArgumentException("A team needs a dressed goalie who can play.", nameof(team));
        RateGoalie(health.For(Goalie.Id).RatingReductionsOn(health.Date));

        Skaters = lineup.ForwardLines
            .SelectMany(line => line.Players)
            .Concat(lineup.DefencePairs.SelectMany(pair => pair.Players))
            .Select(player =>
            {
                var skater = new SkaterState(player, health.For(player.Id).RatingReductionsOn(health.Date));
                if (!health.CanPlay(player.Id))
                {
                    skater.MissMatch();
                }

                return skater;
            })
            .ToList();
        AbleSkaters = team.Roster.Count(player => player.Position != Position.Goalie && health.CanPlay(player.Id));
        AbleGoalies = team.Roster.Count(player => player.Position == Position.Goalie && health.CanPlay(player.Id));
        _skatersByPlayer = Skaters.ToDictionary(skater => skater.Player);
        _extraAttackers = lineup.ExtraAttackers.Select(player => _skatersByPlayer[player]).ToList();

        var forwards = new Rotation(
            lineup.ForwardLines.Select(line => new[]
            {
                Slot(line.LeftWing, SkaterRole.Wing, SkaterSide.Left),
                Slot(line.Centre, SkaterRole.Centre, side: null),
                Slot(line.RightWing, SkaterRole.Wing, SkaterSide.Right),
            }),
            MatchTuning.ForwardLineUsage,
            drainMultiplier: 1);
        var defence = new Rotation(
            lineup.DefencePairs.Select(pair => new[]
            {
                Slot(pair.LeftDefence, SkaterRole.Defence, SkaterSide.Left),
                Slot(pair.RightDefence, SkaterRole.Defence, SkaterSide.Right),
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
                        .Select((player, slot) => Slot(player, unit.Format.Roles[slot], unit.Format.Sides[slot]))
                        .ToArray()),
                    MatchTuning.UnitUsage(format.Situation),
                    drainMultiplier: 1),
            });
        _active = _evenStrength;
    }

    public TeamId TeamId { get; }

    public Player Goalie { get; }

    /// <summary>The goalie's ability to stop shots on goal.</summary>
    public double Saving { get; private set; }

    public double ReboundControl { get; private set; }

    /// <summary>The goalie's overall strength in net, used in the shootout and on penalty shots.</summary>
    public double Goaltending { get; private set; }

    /// <summary>The dressed skaters in lineup order: forward lines, then defence pairs.</summary>
    public IReadOnlyList<SkaterState> Skaters { get; }

    /// <summary>The dressed skaters who could play, so appear in the match, in lineup order.</summary>
    public IEnumerable<SkaterState> AppearingSkaters => Skaters.Where(skater => !skater.MissedMatch);

    /// <summary>
    /// Rostered skaters able to play, scratches included: those not out injured before or during
    /// the match. The injury cap keeps it from falling below <see cref="InjuryCap.MinimumAbleSkaters"/>.
    /// </summary>
    public int AbleSkaters { get; private set; }

    /// <summary>Rostered goalies able to play; see <see cref="AbleSkaters"/>.</summary>
    public int AbleGoalies { get; private set; }

    /// <summary>
    /// The shootout shooters in order: dressed skaters by shootout strength, without anyone ejected,
    /// injured, or still serving a penalty when overtime ends.
    /// </summary>
    /// <remarks>
    /// OrderByDescending is stable, so equally rated shooters keep their lineup order and the
    /// shootout order is deterministic.
    /// </remarks>
    public IReadOnlyList<SkaterState> ShootoutOrder =>
        Skaters.Where(IsAvailable).OrderByDescending(skater => skater.Shootout).ToList();

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

    /// <summary>The mean of a strength over the skaters on the ice, as they perform at their current energy.</summary>
    public double MeanOnIce(Func<OnIceSkater, double> strength)
    {
        var onIce = OnIce;
        var total = 0.0;
        foreach (var slot in onIce)
        {
            total += strength(slot) * slot.Skater.Performance;
        }

        return total / onIce.Count;
    }

    public bool IsAvailable(SkaterState skater) =>
        !skater.IsEjected && !skater.IsInjured && !_penaltyBox.IsServing(skater);

    /// <summary>
    /// A skater hurt in the match leaves it, or plays on at reduced ratings; either way the skaters
    /// on the ice are composed again.
    /// </summary>
    public void Injure(SkaterState skater, InjuryDefinition injury, IReadOnlyDictionary<Rating, int> reductions)
    {
        if (injury.CanPlayThrough)
        {
            skater.PlayThrough(reductions);
        }
        else
        {
            skater.LeaveInjured();
            AbleSkaters--;
        }

        _onIce = null;
        _availableSkaterCount = null;
    }

    /// <summary>The goalie in net plays on through an injury at reduced ratings.</summary>
    public void InjureGoalie(IReadOnlyDictionary<Rating, int> reductions) => RateGoalie(reductions);

    private void RateGoalie(IReadOnlyDictionary<Rating, int> reductions)
    {
        var ratings = new EffectiveRatings(Goalie, reductions);
        Saving = PlayerStrength.Saving(ratings);
        ReboundControl = ratings[Rating.GoalieReboundControl];
        Goaltending = PlayerStrength.Goaltending(ratings);
    }

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
            onIce.Add(new OnIceSkater(skater, slot.Role, slot.Side));
            drains.Add(drain);
        }

        if (IsGoaliePulled)
        {
            var extraAttacker = _extraAttackers.FirstOrDefault(skater => IsAvailable(skater) && !Contains(onIce, skater))
                ?? Substitute(onIce, planned, SkaterRole.Wing);
            onIce.Add(OnIceSkater.ExtraAttacker(extraAttacker));
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

    private OnIceSkater Slot(Player player, SkaterRole role, SkaterSide? side) => new(_skatersByPlayer[player], role, side);
}