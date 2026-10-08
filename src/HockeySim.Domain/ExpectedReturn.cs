namespace HockeySim.Domain;

/// <summary>
/// The dates between which the team's staff expect an injured player back. The range always holds
/// the injury's actual <see cref="Injury.ReturnDate"/>, which stays hidden.
/// </summary>
public sealed record ExpectedReturn(DateOnly Earliest, DateOnly Latest);