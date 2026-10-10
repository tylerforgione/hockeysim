using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// One conference's playoff picture in the NHL wild-card format: the top
/// <see cref="DivisionQualifiers"/> teams of each division, then the conference's other teams
/// ranked against each other for its <see cref="WildCardsPerConference"/> wild cards. As the
/// season stands, those teams are the conference's playoff qualifiers.
/// </summary>
public sealed class WildCardStandings
{
    public const int DivisionQualifiers = 3;
    public const int WildCardsPerConference = 2;

    private readonly ReadOnlyCollection<DivisionLeaders> _divisionLeaders;
    private readonly ReadOnlyCollection<StandingsEntry> _wildCardRace;

    internal WildCardStandings(
        Conference conference,
        IEnumerable<DivisionLeaders> divisionLeaders,
        IEnumerable<StandingsEntry> wildCardRace)
    {
        Conference = conference;
        _divisionLeaders = divisionLeaders.ToList().AsReadOnly();
        _wildCardRace = wildCardRace.ToList().AsReadOnly();
    }

    public Conference Conference { get; }

    /// <summary>Each division's leaders, in conference division order.</summary>
    public IReadOnlyList<DivisionLeaders> DivisionLeaders => _divisionLeaders;

    /// <summary>
    /// The conference's teams outside the division leaders, best first and ranked among
    /// themselves, so head-to-head considers only teams tied within the race. The first
    /// <see cref="WildCardsPerConference"/> hold the wild cards.
    /// </summary>
    public IReadOnlyList<StandingsEntry> WildCardRace => _wildCardRace;

    /// <summary>The division leaders followed by the wild cards.</summary>
    public IEnumerable<TeamId> Qualifiers =>
        DivisionLeaders.SelectMany(leaders => leaders.Teams)
            .Concat(WildCardRace.Take(WildCardsPerConference))
            .Select(entry => entry.Record.TeamId);
}

/// <summary>
/// A division's top <see cref="WildCardStandings.DivisionQualifiers"/> teams with their division
/// ranks. A tie at the cut-off is taken in division ranking order, so league team order decides
/// between teams level on every criterion.
/// </summary>
public sealed record DivisionLeaders(Division Division, IReadOnlyList<StandingsEntry> Teams);