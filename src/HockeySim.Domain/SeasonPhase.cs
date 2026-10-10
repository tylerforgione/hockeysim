namespace HockeySim.Domain;

/// <summary>The part of the season a league day belongs to.</summary>
public enum SeasonPhase
{
    /// <summary>
    /// Exhibition matches before opening day. They are played and kept, but count toward no
    /// record, statistic, or player's health.
    /// </summary>
    Preseason,

    /// <summary>From opening day until every regular-season match is played.</summary>
    RegularSeason,

    /// <summary>
    /// From the end of the regular season: the qualifiers' best-of-seven series, until the final
    /// crowns a champion. Records and statistics are kept apart from the regular season's.
    /// </summary>
    Playoffs,
}