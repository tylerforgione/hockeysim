using System.Collections.ObjectModel;

namespace HockeySim.Domain;

public sealed class Lineup
{
    public const int RequiredForwardLineCount = 4;
    public const int RequiredDefencePairCount = 3;
    public const int RequiredDressedPlayerCount = 20;
    public const int RequiredExtraAttackerCount = 2;

    private readonly ReadOnlyCollection<ForwardLine> _forwardLines;
    private readonly ReadOnlyCollection<DefencePair> _defencePairs;
    private readonly ReadOnlyCollection<Player> _dressedPlayers;
    private readonly ReadOnlyCollection<SpecialSituationUnit> _specialSituationUnits;
    private readonly ReadOnlyCollection<Player> _extraAttackers;

    /// <param name="specialSituationUnits">
    /// Exactly <see cref="SpecialSituationFormat.UnitCount"/> units for every special situation.
    /// Units of the same situation keep their given order: the first is the first unit.
    /// </param>
    /// <param name="extraAttackers">
    /// The skaters sent on when the goalie is pulled, first choice first.
    /// </param>
    public Lineup(
        IEnumerable<ForwardLine> forwardLines,
        IEnumerable<DefencePair> defencePairs,
        Player startingGoalie,
        Player backupGoalie,
        IEnumerable<SpecialSituationUnit> specialSituationUnits,
        IEnumerable<Player> extraAttackers)
    {
        ArgumentNullException.ThrowIfNull(forwardLines);
        ArgumentNullException.ThrowIfNull(defencePairs);
        ArgumentNullException.ThrowIfNull(startingGoalie);
        ArgumentNullException.ThrowIfNull(backupGoalie);
        ArgumentNullException.ThrowIfNull(specialSituationUnits);
        ArgumentNullException.ThrowIfNull(extraAttackers);

        var forwardLineList = forwardLines.ToList();
        if (forwardLineList.Count != RequiredForwardLineCount)
        {
            throw new ArgumentException(
                $"A lineup must contain exactly {RequiredForwardLineCount} forward lines.",
                nameof(forwardLines));
        }

        var defencePairList = defencePairs.ToList();
        if (defencePairList.Count != RequiredDefencePairCount)
        {
            throw new ArgumentException(
                $"A lineup must contain exactly {RequiredDefencePairCount} defence pairs.",
                nameof(defencePairs));
        }

        if (startingGoalie.Position != Position.Goalie || backupGoalie.Position != Position.Goalie)
        {
            throw new ArgumentException("Both lineup goalies must play the goalie position.");
        }

        var dressedSkaters = forwardLineList
            .SelectMany(line => line.Players)
            .Concat(defencePairList.SelectMany(pair => pair.Players))
            .ToList();
        var dressedPlayers = dressedSkaters
            .Append(startingGoalie)
            .Append(backupGoalie)
            .ToList();

        if (dressedPlayers.Count != RequiredDressedPlayerCount
            || dressedPlayers.Select(player => player.Id).Distinct().Count() != dressedPlayers.Count)
        {
            throw new ArgumentException(
                $"A lineup must contain {RequiredDressedPlayerCount} distinct dressed players.");
        }

        var dressedSkaterSet = dressedSkaters.ToHashSet(ReferenceEqualityComparer.Instance);
        var unitList = OrderUnits(specialSituationUnits.ToList());
        if (unitList.Any(unit => !unit.Players.All(dressedSkaterSet.Contains)))
        {
            throw new ArgumentException(
                "Only dressed skaters can play in special-situation units.",
                nameof(specialSituationUnits));
        }

        var extraAttackerList = extraAttackers.ToList();
        if (extraAttackerList.Count != RequiredExtraAttackerCount
            || !extraAttackerList.All(dressedSkaterSet.Contains)
            || extraAttackerList.Distinct().Count() != extraAttackerList.Count)
        {
            throw new ArgumentException(
                $"A lineup must name {RequiredExtraAttackerCount} different dressed skaters as extra attackers.",
                nameof(extraAttackers));
        }

        _forwardLines = forwardLineList.AsReadOnly();
        _defencePairs = defencePairList.AsReadOnly();
        StartingGoalie = startingGoalie;
        BackupGoalie = backupGoalie;
        _dressedPlayers = dressedPlayers.AsReadOnly();
        _specialSituationUnits = unitList.AsReadOnly();
        _extraAttackers = extraAttackerList.AsReadOnly();
    }

    public IReadOnlyList<ForwardLine> ForwardLines => _forwardLines;

    public IReadOnlyList<DefencePair> DefencePairs => _defencePairs;

    public Player StartingGoalie { get; }

    public Player BackupGoalie { get; }

    public IReadOnlyList<Player> DressedPlayers => _dressedPlayers;

    /// <summary>
    /// Gets every special-situation unit, grouped in <see cref="SpecialSituation"/> order and
    /// ordered first unit first within each situation.
    /// </summary>
    public IReadOnlyList<SpecialSituationUnit> SpecialSituationUnits => _specialSituationUnits;

    /// <summary>
    /// Gets the two skaters sent on as an extra attacker when the goalie is pulled, first choice
    /// first. The second choice covers a first choice who is already on the ice.
    /// </summary>
    public IReadOnlyList<Player> ExtraAttackers => _extraAttackers;

    /// <summary>
    /// Gets the units for one situation, first unit first.
    /// </summary>
    public IReadOnlyList<SpecialSituationUnit> UnitsFor(SpecialSituation situation)
    {
        SpecialSituationFormat.For(situation);
        return _specialSituationUnits.Where(unit => unit.Situation == situation).ToList().AsReadOnly();
    }

    /// <summary>
    /// Creates a lineup whose special-situation units and extra attackers are derived from its
    /// lines and pairs, as for a generated team. See <see cref="DefaultSpecialSituationUnits"/>.
    /// </summary>
    public static Lineup CreateWithDefaultUnits(
        IEnumerable<ForwardLine> forwardLines,
        IEnumerable<DefencePair> defencePairs,
        Player startingGoalie,
        Player backupGoalie)
    {
        ArgumentNullException.ThrowIfNull(forwardLines);
        ArgumentNullException.ThrowIfNull(defencePairs);

        var forwardLineList = forwardLines.ToList();
        var defencePairList = defencePairs.ToList();
        if (forwardLineList.Count != RequiredForwardLineCount || defencePairList.Count != RequiredDefencePairCount)
        {
            throw new ArgumentException(
                $"A lineup must contain exactly {RequiredForwardLineCount} forward lines and {RequiredDefencePairCount} defence pairs.");
        }

        return new Lineup(
            forwardLineList,
            defencePairList,
            startingGoalie,
            backupGoalie,
            DefaultSpecialSituationUnits.Create(forwardLineList, defencePairList),
            DefaultSpecialSituationUnits.ExtraAttackers(forwardLineList));
    }

    private static List<SpecialSituationUnit> OrderUnits(List<SpecialSituationUnit> units)
    {
        if (units.Any(unit => unit is null))
        {
            throw new ArgumentException("Special-situation units cannot be missing.", nameof(units));
        }

        var ordered = new List<SpecialSituationUnit>(units.Count);
        foreach (var format in SpecialSituationFormat.All)
        {
            var situationUnits = units.Where(unit => unit.Situation == format.Situation).ToList();
            if (situationUnits.Count != format.UnitCount)
            {
                throw new ArgumentException(
                    $"A lineup must contain exactly {format.UnitCount} {format.Situation} units.",
                    nameof(units));
            }

            ordered.AddRange(situationUnits);
        }

        return ordered;
    }
}