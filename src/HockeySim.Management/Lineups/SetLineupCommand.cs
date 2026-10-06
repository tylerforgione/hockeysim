using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Management.Lineups;

/// <summary>
/// Replaces the managed team's whole lineup, including every special-situation unit.
/// </summary>
/// <param name="SpecialSituationUnits">
/// Every unit for every special situation; units of one situation are listed first unit first.
/// </param>
/// <param name="ExtraAttackerIds">The two extra attackers, first choice first.</param>
public sealed record SetLineupCommand(
    IReadOnlyList<ForwardLineSelection> ForwardLines,
    IReadOnlyList<DefencePairSelection> DefencePairs,
    PlayerId StartingGoalieId,
    PlayerId BackupGoalieId,
    IReadOnlyList<SpecialSituationUnitSelection> SpecialSituationUnits,
    IReadOnlyList<PlayerId> ExtraAttackerIds
)
{
    /// <summary>
    /// Creates a command that sets <paramref name="lineup"/> exactly as it is, as a starting point
    /// for changing part of it.
    /// </summary>
    public static SetLineupCommand From(LineupSnapshot lineup)
    {
        ArgumentNullException.ThrowIfNull(lineup);

        return new SetLineupCommand(
            lineup.ForwardLines
                .Select(line => new ForwardLineSelection(line.LeftWingId, line.CentreId, line.RightWingId))
                .ToList(),
            lineup.DefencePairs
                .Select(pair => new DefencePairSelection(pair.LeftDefenceId, pair.RightDefenceId))
                .ToList(),
            lineup.StartingGoalieId,
            lineup.BackupGoalieId,
            lineup.SpecialSituationUnits
                .Select(unit => new SpecialSituationUnitSelection(unit.Situation, unit.PlayerIds.ToList()))
                .ToList(),
            lineup.ExtraAttackerIds.ToList());
    }
}