using System.Collections.ObjectModel;

using HockeySim.Domain;

namespace HockeySim.Management.GameManagement.Snapshots;

/// <summary>
/// Regular-season standings for the league, each conference, and each division. Every table is
/// ranked independently, so head-to-head only considers the teams tied within that table.
/// </summary>
public sealed class StandingsSnapshot
{
    private readonly ReadOnlyCollection<StandingsEntrySnapshot> _league;
    private readonly ReadOnlyCollection<ConferenceStandingsSnapshot> _conferences;

    private StandingsSnapshot(
        IReadOnlyList<StandingsEntrySnapshot> league,
        IReadOnlyList<ConferenceStandingsSnapshot> conferences)
    {
        _league = new ReadOnlyCollection<StandingsEntrySnapshot>(league.ToList());
        _conferences = new ReadOnlyCollection<ConferenceStandingsSnapshot>(conferences.ToList());
    }

    /// <summary>Every team, best first.</summary>
    public IReadOnlyList<StandingsEntrySnapshot> League => _league;

    /// <summary>Conference standings in league conference order.</summary>
    public IReadOnlyList<ConferenceStandingsSnapshot> Conferences => _conferences;

    internal static StandingsSnapshot Create(Season season) =>
        new(
            Rank(season, season.League.Teams),
            season.League.Conferences
                .Select(conference => ConferenceStandingsSnapshot.Create(season, conference))
                .ToList());

    internal static List<StandingsEntrySnapshot> Rank(Season season, IEnumerable<Team> teams) =>
        season.RankStandings(teams.Select(team => team.Id))
            .Select(StandingsEntrySnapshot.Create)
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

    internal static ConferenceStandingsSnapshot Create(Season season, Conference conference) =>
        new(
            conference.Name,
            StandingsSnapshot.Rank(season, conference.Divisions.SelectMany(division => division.Teams)),
            conference.Divisions.Select(division => DivisionStandingsSnapshot.Create(season, division)).ToList());
}

public sealed class DivisionStandingsSnapshot
{
    private readonly ReadOnlyCollection<StandingsEntrySnapshot> _teams;

    private DivisionStandingsSnapshot(string name, IReadOnlyList<StandingsEntrySnapshot> teams)
    {
        Name = name;
        _teams = new ReadOnlyCollection<StandingsEntrySnapshot>(teams.ToList());
    }

    public string Name { get; }

    /// <summary>The division's teams, best first.</summary>
    public IReadOnlyList<StandingsEntrySnapshot> Teams => _teams;

    internal static DivisionStandingsSnapshot Create(Season season, Division division) =>
        new(division.Name, StandingsSnapshot.Rank(season, division.Teams));
}

/// <summary>
/// A team's place in one standings table with its supporting record.
/// </summary>
/// <param name="Rank">
/// One-based position. Teams level on every official criterion share a rank and are listed in
/// league team order.
/// </param>
public sealed record StandingsEntrySnapshot(int Rank, TeamRecordSnapshot Record)
{
    internal static StandingsEntrySnapshot Create(StandingsEntry entry) =>
        new(entry.Rank, TeamRecordSnapshot.Create(entry.Record));
}