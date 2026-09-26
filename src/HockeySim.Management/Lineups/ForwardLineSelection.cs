using HockeySim.Domain;

namespace HockeySim.Management.Lineups;

public sealed record ForwardLineSelection(
    PlayerId LeftWingId,
    PlayerId CentreId,
    PlayerId RightWingId
);