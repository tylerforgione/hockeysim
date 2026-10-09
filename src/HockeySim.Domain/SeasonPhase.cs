namespace HockeySim.Domain;

/// <summary>The part of the season a league day belongs to.</summary>
public enum SeasonPhase
{
    /// <summary>
    /// Exhibition matches before opening day. They are played and kept, but count toward no
    /// record, statistic, or player's health.
    /// </summary>
    Preseason,

    /// <summary>From opening day, the matches that count.</summary>
    RegularSeason,
}