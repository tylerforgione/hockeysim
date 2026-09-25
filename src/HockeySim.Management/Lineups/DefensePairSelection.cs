using HockeySim.Domain;

namespace HockeySim.Management.Lineups;

public sealed record DefensePairSelection(
    PlayerId LeftDefenseId,
    PlayerId RightDefenseId
);