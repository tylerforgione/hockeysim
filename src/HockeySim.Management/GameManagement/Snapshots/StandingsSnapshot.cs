using System.Collections.ObjectModel;

using HockeySim.Domain;

namespace HockeySim.Management.GameManagement.Snapshots;

/// <summary>
/// Regular-season standings for the league, each conference, and each division, and each
/// conference's wild-card view. Every table is ranked independently, so head-to-head only
/// considers the teams tied within that table. Every entry carries its team's playoff status.
/// </summary>
public sealed class StandingsSnapshot
{
    private readonly ReadOnlyCollection<StandingsEntrySnapshot> _league;
    private readonly ReadOnlyCollection<ConferenceStandingsSnapshot> _conferences;
    private readonly ReadOnlyCollection<WildCardStandingsSnapshot> _wildCard;

    private StandingsSnapshot(
        IReadOnlyList<StandingsEntrySnapshot> league,
        IReadOnlyList<ConferenceStandingsSnapshot> conferences,
        IReadOnlyList<WildCardStandingsSnapshot> wildCard)
    {
        _league = new ReadOnlyCollection<StandingsEntrySnapshot>(league.ToList());
        _conferences = new ReadOnlyCollection<ConferenceStandingsSnapshot>(conferences.ToList());
        _wildCard = new ReadOnlyCollection<WildCardStandingsSnapshot>(wildCard.ToList());
    }

    /// <summary>Every team, best first.</summary>
    public IReadOnlyList<StandingsEntrySnapshot> League => _league;

    /// <summary>Conference standings in league conference order.</summary>
    public IReadOnlyList<ConferenceStandingsSnapshot> Conferences => _conferences;

    /// <summary>Each conference's wild-card view, in league conference order.</summary>
    public IReadOnlyList<WildCardStandingsSnapshot> WildCard => _wildCard;

    internal static StandingsSnapshot Create(Season season)
    {
        var statuses = season.PlayoffStatuses();
        return new(
            Rank(season.RankStandings(season.League.Teams.Select(team => team.Id)), statuses),
            season.League.Conferences
                .Select(conference => ConferenceStandingsSnapshot.Create(season, conference, statuses))
                .ToList(),
            season.League.Conferences
                .Select(conference => WildCardStandingsSnapshot.Create(season.RankWildCard(conference), statuses))
                .ToList());
    }

    internal static List<StandingsEntrySnapshot> Rank(
        IEnumerable<StandingsEntry> entries,
        IReadOnlyDictionary<TeamId, PlayoffStatus> statuses) =>
        entries
            .Select(entry => StandingsEntrySnapshot.Create(entry, statuses[entry.Record.TeamId]))
            .ToList();
}

public sealed class ConferenceStandingsSnapshot
{
    private readonly ReadOnlyCollection<StandingsEntrySnapshot> _teams;
    private readonly ReadOnlyCollection<DivisionStandingsSnapshot> _divisions;

    private ConferenceStandingsSnapshot(
        string name,
        IReadOnlyList<StandingsEntrySnapshot> teams,
        IReadOnlyList<DivisionStandingsSnapshot> divisions)
    {
        Name = name;
        _teams = new ReadOnlyCollection<StandingsEntrySnapshot>(teams.ToList());
        _divisions = new ReadOnlyCollection<DivisionStandingsSnapshot>(divisions.ToList());
    }

    public string Name { get; }

    /// <summary>The conference's teams, best first.</summary>
    public IReadOnlyList<StandingsEntrySnapshot> Teams => _teams;

    /// <summary>Division standings in conference division order.</summary>
    public IReadOnlyList<DivisionStandingsSnapshot> Divisions => _divisions;

    internal static ConferenceStandingsSnapshot Create(
        Season season,
        Conference conference,
        IReadOnlyDictionary<TeamId, PlayoffStatus> statuses) =>
        new(
            conference.Name,
            StandingsSnapshot.Rank(
                season.RankStandings(conference.Divisions.SelectMany(division => division.Teams).Select(team => team.Id)),
                statuses),
            conference.Divisions
                .Select(division => new DivisionStandingsSnapshot(
                    division.Name,
                    StandingsSnapshot.Rank(season.RankStandings(division.Teams.Select(team => team.Id)), statuses)))
                .ToList());
}

/// <summary>
/// A conference in the wild-card format: each division's top three, then the rest of the
/// conference ranked for the two wild cards.
/// </summary>
public sealed class WildCardStandingsSnapshot
{
    private readonly ReadOnlyCollection<DivisionStandingsSnapshot> _divisionLeaders;
    private readonly ReadOnlyCollection<StandingsEntrySnapshot> _wildCardRace;

    private WildCardStandingsSnapshot(
        string name,
        IReadOnlyList<DivisionStandingsSnapshot> divisionLeaders,
        IReadOnlyList<StandingsEntrySnapshot> wildCardRace)
    {
        Name = name;
        _divisionLeaders = new ReadOnlyCollection<DivisionStandingsSnapshot>(divisionLeaders.ToList());
        _wildCardRace = new ReadOnlyCollection<StandingsEntrySnapshot>(wildCardRace.ToList());
    }

    /// <summary>The conference's name.</summary>
    public string Name { get; }

    /// <summary>Each division's top three with their division ranks, in conference division order.</summary>
    public IReadOnlyList<DivisionStandingsSnapshot> DivisionLeaders => _divisionLeaders;

    /// <summary>
    /// The conference's other teams, best first and ranked among themselves. The first
    /// <see cref="WildCardCount"/> hold the wild cards.
    /// </summary>
    public IReadOnlyList<StandingsEntrySnapshot> WildCardRace => _wildCardRace;

    public int WildCardCount => WildCardStandings.WildCardsPerConference;

    internal static WildCardStandingsSnapshot Create(
        WildCardStandings standings,
        IReadOnlyDictionary<TeamId, PlayoffStatus> statuses) =>
        new(
            standings.Conference.Name,
            standings.DivisionLeaders
                .Select(leaders => new DivisionStandingsSnapshot(
                    leaders.Division.Name,
                    StandingsSnapshot.Rank(leaders.Teams, statuses)))
                .ToList(),
            StandingsSnapshot.Rank(standings.WildCardRace, statuses));
}

public sealed class DivisionStandingsSnapshot
{
    private readonly ReadOnlyCollection<StandingsEntrySnapshot> _teams;

    internal DivisionStandingsSnapshot(string name, IReadOnlyList<StandingsEntrySnapshot> teams)
    {
        Name = name;
        _teams = new ReadOnlyCollection<StandingsEntrySnapshot>(teams.ToList());
    }

    public string Name { get; }

    /// <summary>The division's teams, best first; in the wild-card view, only its leaders.</summary>
    public IReadOnlyList<StandingsEntrySnapshot> Teams => _teams;
}

/// <summary>
/// A team's place in one standings table with its supporting record.
/// </summary>
/// <param name="Rank">
/// One-based position. Teams level on every official criterion share a rank and are listed in
/// league team order.
/// </param>
/// <param name="PlayoffStatus">
/// The team's guaranteed playoff status, the same in every table.
/// </param>
public sealed record StandingsEntrySnapshot(int Rank, TeamRecordSnapshot Record, PlayoffStatus PlayoffStatus)
{
    internal static StandingsEntrySnapshot Create(StandingsEntry entry, PlayoffStatus playoffStatus) =>
        new(entry.Rank, TeamRecordSnapshot.Create(entry.Record), playoffStatus);
}