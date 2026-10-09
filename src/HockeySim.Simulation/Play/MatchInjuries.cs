using HockeySim.Domain;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Simulation.Play;

/// <summary>
/// Decides the injuries a match's play causes and keeps the hidden wear it adds. Each contact
/// strikes a body part, adds wear there, and may injure it; durability lowers the chance and the
/// part's wear, from earlier matches and this one, raises it. An injury that cannot be played
/// through does not happen when it would break the <see cref="InjuryCap"/>, or to the goalie in
/// net, who plays the whole match.
/// </summary>
internal sealed class MatchInjuries(MatchHealth health, ControlledRandom random)
{
    private readonly Dictionary<(PlayerId Player, BodyPart Part), int> _wear = [];
    private readonly List<(PlayerId Player, BodyPart Part)> _wearOrder = [];
    private readonly Dictionary<PlayerId, Dictionary<Rating, int>> _reductions = [];

    public bool InjuriesPossible => health.InjuriesPossible;

    /// <summary>The wear each player's body parts took in the match, in the order first struck.</summary>
    public IEnumerable<WearGain> Wear =>
        _wearOrder.Select(key => new WearGain(key.Player, key.Part, _wear[key]));

    /// <summary>
    /// Plays a contact or strain on a player. Returns the injury it causes, if any.
    /// </summary>
    /// <param name="riskFactor">A multiplier on the chance, such as fatigue for a strain.</param>
    /// <param name="canLeave">Whether the player may suffer an injury that takes them out of the match.</param>
    /// <param name="ablePlayers">The team's rostered players at the player's position still able to play.</param>
    public DrawnInjury? TryInjure(Player player, InjuryCause cause, double riskFactor, bool canLeave, int ablePlayers)
    {
        if (!health.InjuriesPossible)
        {
            return null;
        }

        var parts = InjuryTuning.PartsStruck(cause);
        var struck = parts[random.NextWeightedIndex(parts.Select(part => part.Weight).ToArray())];
        if (cause != InjuryCause.Strain)
        {
            AddWear(player.Id, struck.BodyPart, InjuryTuning.ContactWear(cause));
        }

        var wear = health.For(player.Id).Wear(struck.BodyPart) + _wear.GetValueOrDefault((player.Id, struck.BodyPart));
        var durability = player.GetRating(Rating.Durability).Value;
        var chance = InjuryTuning.BaseChance(cause)
            * struck.Chance
            * riskFactor
            * Math.Exp(-InjuryTuning.DurabilitySensitivity * (durability - MatchTuning.ReferenceRating))
            * (1 + (InjuryTuning.WearRiskPerPoint * wear));
        if (!random.Chance(chance))
        {
            return null;
        }

        var type = struck.Injuries[random.NextWeightedIndex(struck.Injuries.Select(injury => injury.Chance).ToArray())].Type;
        var definition = InjuryCatalogue.For(type);
        if (!definition.CanPlayThrough && (!canLeave || !InjuryCap.AllowsLosing(player.Position, ablePlayers)))
        {
            return null;
        }

        // The shortest of a few draws, so most injuries heal toward the short end of their range
        // and only some take the longest.
        var recoveryDays = definition.MaximumRecoveryDays;
        for (var draw = 0; draw < InjuryTuning.RecoveryDayDraws; draw++)
        {
            recoveryDays = Math.Min(recoveryDays, random.NextInt(definition.MinimumRecoveryDays, definition.MaximumRecoveryDays + 1));
        }
        AddWear(player.Id, definition.BodyPart, InjuryTuning.InjuryWear + (recoveryDays / InjuryTuning.RecoveryDaysPerInjuryWearPoint));

        var reductions = ReductionsFor(player.Id);
        if (definition.CanPlayThrough)
        {
            foreach (var (rating, points) in definition.RatingReductions)
            {
                reductions[rating] = reductions.GetValueOrDefault(rating) + points;
            }
        }

        return new DrawnInjury(definition, cause, recoveryDays, reductions.AsReadOnly());
    }

    private Dictionary<Rating, int> ReductionsFor(PlayerId playerId)
    {
        if (!_reductions.TryGetValue(playerId, out var reductions))
        {
            reductions = new Dictionary<Rating, int>(health.For(playerId).RatingReductionsOn(health.Date));
            _reductions[playerId] = reductions;
        }

        return reductions;
    }

    private void AddWear(PlayerId playerId, BodyPart part, int points)
    {
        var key = (playerId, part);
        if (!_wear.ContainsKey(key))
        {
            _wearOrder.Add(key);
        }

        _wear[key] = _wear.GetValueOrDefault(key) + points;
    }
}

/// <summary>An injury the play caused, and the player's rating reductions from every injury they are now playing through.</summary>
internal sealed record DrawnInjury(InjuryDefinition Definition, InjuryCause Cause, int RecoveryDays, IReadOnlyDictionary<Rating, int> Reductions);