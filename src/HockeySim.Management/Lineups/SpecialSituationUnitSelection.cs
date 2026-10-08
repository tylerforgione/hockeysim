using HockeySim.Domain;

namespace HockeySim.Management.Lineups;

/// <param name="PlayerIds">The skaters in the slot order of the situation's format.</param>
public sealed record SpecialSituationUnitSelection(
    SpecialSituation Situation,
    IReadOnlyList<PlayerId> PlayerIds
);