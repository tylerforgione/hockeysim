using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// How many units a lineup holds for a special situation and the role of each slot in a unit.
/// </summary>
/// <remarks>
/// Slots list forwards before defence. Every format has exactly one centre, so faceoffs always
/// have a defined player. Where a unit has two wings or two defence players, the first plays the
/// left side and the second the right.
/// </remarks>
public sealed class SpecialSituationFormat
{
    private static readonly SkaterRole[] FiveSkaterPowerPlay =
        [SkaterRole.Wing, SkaterRole.Centre, SkaterRole.Wing, SkaterRole.Defence, SkaterRole.Defence];

    private static readonly SkaterRole[] FourSkaters =
        [SkaterRole.Centre, SkaterRole.Wing, SkaterRole.Defence, SkaterRole.Defence];

    private static readonly SkaterRole[] ThreeSkaterPenaltyKill =
        [SkaterRole.Centre, SkaterRole.Defence, SkaterRole.Defence];

    private static readonly SkaterRole[] ThreeOnThreeSkaters =
        [SkaterRole.Centre, SkaterRole.Wing, SkaterRole.Defence];

    private static readonly ReadOnlyCollection<SpecialSituationFormat> Formats = Array.AsReadOnly(
    [
        new SpecialSituationFormat(SpecialSituation.PowerPlay5On4, 2, FiveSkaterPowerPlay),
        new SpecialSituationFormat(SpecialSituation.PowerPlay5On3, 2, FiveSkaterPowerPlay),
        new SpecialSituationFormat(SpecialSituation.PowerPlay4On3, 2, FourSkaters),
        new SpecialSituationFormat(SpecialSituation.PenaltyKill4On5, 3, FourSkaters),
        new SpecialSituationFormat(SpecialSituation.PenaltyKill3On5, 2, ThreeSkaterPenaltyKill),
        new SpecialSituationFormat(SpecialSituation.PenaltyKill3On4, 2, ThreeSkaterPenaltyKill),
        new SpecialSituationFormat(SpecialSituation.FourOnFour, 2, FourSkaters),
        new SpecialSituationFormat(SpecialSituation.ThreeOnThree, 3, ThreeOnThreeSkaters),
    ]);

    private readonly ReadOnlyCollection<SkaterRole> _roles;
    private readonly ReadOnlyCollection<SkaterSide?> _sides;

    private SpecialSituationFormat(SpecialSituation situation, int unitCount, SkaterRole[] roles)
    {
        Situation = situation;
        UnitCount = unitCount;
        _roles = Array.AsReadOnly(roles);
        _sides = Array.AsReadOnly(SidesOf(roles));
    }

    /// <summary>
    /// Gets the format of every special situation, in <see cref="SpecialSituation"/> order.
    /// </summary>
    public static IReadOnlyList<SpecialSituationFormat> All => Formats;

    public SpecialSituation Situation { get; }

    /// <summary>
    /// Gets the number of units a lineup holds for the situation.
    /// </summary>
    public int UnitCount { get; }

    /// <summary>
    /// Gets the role of each slot in a unit, in slot order.
    /// </summary>
    public IReadOnlyList<SkaterRole> Roles => _roles;

    /// <summary>
    /// Gets the side of each slot, in slot order: left then right for a pair of wings or defence,
    /// and none for the centre or a lone wing or defence player, who play across the ice.
    /// </summary>
    public IReadOnlyList<SkaterSide?> Sides => _sides;

    public static SpecialSituationFormat For(SpecialSituation situation) =>
        Formats.SingleOrDefault(format => format.Situation == situation)
        ?? throw new ArgumentOutOfRangeException(nameof(situation), "The special situation must be defined.");

    private static SkaterSide?[] SidesOf(SkaterRole[] roles)
    {
        var sides = new SkaterSide?[roles.Length];
        foreach (var role in new[] { SkaterRole.Wing, SkaterRole.Defence })
        {
            var slots = Enumerable.Range(0, roles.Length).Where(slot => roles[slot] == role).ToList();
            if (slots.Count == 2)
            {
                sides[slots[0]] = SkaterSide.Left;
                sides[slots[1]] = SkaterSide.Right;
            }
        }

        return sides;
    }
}