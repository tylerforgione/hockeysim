using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// How many units a lineup holds for a special situation and the role of each slot in a unit.
/// </summary>
/// <remarks>
/// Slots list forwards before defence. Every format has exactly one centre, so faceoffs always
/// have a defined player.
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

    private SpecialSituationFormat(SpecialSituation situation, int unitCount, SkaterRole[] roles)
    {
        Situation = situation;
        UnitCount = unitCount;
        _roles = Array.AsReadOnly(roles);
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

    public static SpecialSituationFormat For(SpecialSituation situation) =>
        Formats.SingleOrDefault(format => format.Situation == situation)
        ?? throw new ArgumentOutOfRangeException(nameof(situation), "The special situation must be defined.");
}