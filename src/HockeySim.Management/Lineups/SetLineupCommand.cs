using HockeySim.Domain;

namespace HockeySim.Management.Lineups;

public sealed record SetLineupCommand(
    IReadOnlyList<ForwardLineSelection> ForwardLines,
    IReadOnlyList<DefencePairSelection> DefencePairs,
    PlayerId StartingGoalieId,
    PlayerId BackupGoalieId
);