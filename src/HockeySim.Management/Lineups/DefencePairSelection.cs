using HockeySim.Domain;

namespace HockeySim.Management.Lineups;

public sealed record DefencePairSelection(
    PlayerId LeftDefenceId,
    PlayerId RightDefenceId
);