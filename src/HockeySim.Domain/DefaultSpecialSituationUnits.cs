namespace HockeySim.Domain;

/// <summary>
/// Derives special-situation units from a lineup's forward lines and defence pairs, as a sensible
/// starting point for teams whose units nobody has chosen, such as generated teams.
/// </summary>
/// <remarks>
/// Higher lines and pairs are assumed to be stronger. Power plays, four-on-four, and three-on-three
/// draw on the top lines and pairs; penalty kills draw on the second to fourth lines' centres and
/// left wings with the pairs in order. The first two lines' centres are the extra attackers.
/// </remarks>
public static class DefaultSpecialSituationUnits
{
    /// <param name="forwardLines">Exactly four forward lines, top line first.</param>
    /// <param name="defencePairs">Exactly three defence pairs, top pair first.</param>
    public static IReadOnlyList<SpecialSituationUnit> Create(
        IReadOnlyList<ForwardLine> forwardLines,
        IReadOnlyList<DefencePair> defencePairs)
    {
        ArgumentNullException.ThrowIfNull(forwardLines);
        ArgumentNullException.ThrowIfNull(defencePairs);

        return SpecialSituationFormat.All
            .SelectMany(format => Enumerable.Range(0, format.UnitCount)
                .Select(index => new SpecialSituationUnit(
                    format.Situation,
                    Skaters(format.Situation, index, forwardLines, defencePairs))))
            .ToList();
    }

    /// <param name="forwardLines">The forward lines, top line first.</param>
    public static IReadOnlyList<Player> ExtraAttackers(IReadOnlyList<ForwardLine> forwardLines)
    {
        ArgumentNullException.ThrowIfNull(forwardLines);

        return [forwardLines[0].Centre, forwardLines[1].Centre];
    }

    private static IEnumerable<Player> Skaters(
        SpecialSituation situation,
        int unit,
        IReadOnlyList<ForwardLine> lines,
        IReadOnlyList<DefencePair> pairs)
    {
        var line = lines[unit];
        var pair = pairs[unit];
        var killLine = lines[unit + 1];

        return situation switch
        {
            SpecialSituation.PowerPlay5On4 or SpecialSituation.PowerPlay5On3 =>
                [line.LeftWing, line.Centre, line.RightWing, pair.LeftDefence, pair.RightDefence],
            SpecialSituation.PowerPlay4On3 or SpecialSituation.FourOnFour =>
                [line.Centre, line.LeftWing, pair.LeftDefence, pair.RightDefence],
            SpecialSituation.PenaltyKill4On5 =>
                [killLine.Centre, killLine.LeftWing, pair.LeftDefence, pair.RightDefence],
            SpecialSituation.PenaltyKill3On5 or SpecialSituation.PenaltyKill3On4 =>
                [killLine.Centre, pair.LeftDefence, pair.RightDefence],
            SpecialSituation.ThreeOnThree =>
                [line.Centre, line.LeftWing, pair.LeftDefence],
            _ => throw new ArgumentOutOfRangeException(nameof(situation), "The special situation must be defined."),
        };
    }
}