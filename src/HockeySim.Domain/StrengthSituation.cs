namespace HockeySim.Domain;

/// <summary>
/// The manpower situation from one team's side, decided by the skaters the penalties being served
/// allow rather than by who is on the ice, so an extra attacker for a pulled goalie does not make
/// a power play. Shot totals are kept separately for each, because five-on-five play is the usual
/// measure of a team or skater and special teams would distort it.
/// </summary>
public enum StrengthSituation
{
    /// <summary>Five skaters a side with both goalies in net.</summary>
    FiveOnFive,

    /// <summary>The team has more skaters because the opponent is serving penalties.</summary>
    PowerPlay,

    /// <summary>The team has fewer skaters because it is serving penalties.</summary>
    PenaltyKill,

    /// <summary>
    /// Equal manpower other than five-on-five: four-on-four, three-on-three, or a goalie pulled for
    /// an extra attacker.
    /// </summary>
    Other,
}