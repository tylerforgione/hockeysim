using HockeySim.Domain;

namespace HockeySim.Management.Lineups;

public sealed record SetLineupCommand(
    IReadOnlyList<ForwardLineSelection> ForwardLines,
    IReadOnlyList<DefensePairSelection> DefensePairs,
    PlayerId StartingGoalieId,
    PlayerId BackupGoalieId
);