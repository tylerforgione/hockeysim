using HockeySim.Domain;
using HockeySim.Management.Lineups;

namespace HockeySim.Management.Tests;

internal static class LineupCommands
{
    /// <summary>
    /// Gives <paramref name="replacementId"/> every unit and extra-attacker slot held by
    /// <paramref name="replacedId"/>, as the Lines page does when a scratched player is dressed in
    /// another's place.
    /// </summary>
    public static SetLineupCommand ReplaceInUnits(this SetLineupCommand command, PlayerId replacedId, PlayerId replacementId)
    {
        PlayerId Replace(PlayerId id) => id == replacedId ? replacementId : id;

        return command with
        {
            SpecialSituationUnits = command.SpecialSituationUnits
                .Select(unit => unit with { PlayerIds = unit.PlayerIds.Select(Replace).ToList() })
                .ToList(),
            ExtraAttackerIds = command.ExtraAttackerIds.Select(Replace).ToList(),
        };
    }
}