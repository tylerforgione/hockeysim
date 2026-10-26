using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Schedule;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Standings;

/// <summary>
/// Builds the standings tables from the session's latest snapshot, for the standings page and the
/// home page's panel. Management ranks every table independently; this only presents them, in
/// Management's order.
/// </summary>
public sealed class StandingsTableBuilder
{
    private readonly GameSession _session;
    private readonly Action<TeamId>? _openTeam;

    /// <param name="openTeam">Opens a team from its name; without it, names link nowhere.</param>
    public StandingsTableBuilder(GameSession session, Action<TeamId>? openTeam = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _openTeam = openTeam;
    }

    /// <summary>
    /// One column per conference, side by side, in every view built from conferences; the league
    /// view is a single column.
    /// </summary>
    public List<StandingsColumnViewModel> Columns(StandingsScope scope)
    {
        var standings = _session.Snapshot.Season.Standings;
        var results = ResultsByTeam();
        return scope switch
        {
            StandingsScope.League =>
            [
                new([CreateTable("LEAGUE", $"{standings.League.Count} teams", standings.League, results)]),
            ],
            StandingsScope.Conference => standings.Conferences
                .Select(conference => new StandingsColumnViewModel(
                    [CreateTable(conference.Name.ToUpperInvariant(), $"{conference.Teams.Count} teams", conference.Teams, results)]))
                .ToList(),
            StandingsScope.Division => standings.Conferences
                .Select(conference => new StandingsColumnViewModel(conference.Divisions
                    .Select(division => CreateTable(division.Name.ToUpperInvariant(), conference.Name, division.Teams, results))
                    .ToList()))
                .ToList(),
            StandingsScope.WildCard => standings.WildCard
                .Select(conference => new StandingsColumnViewModel(CreateWildCardTables(conference, results).ToList()))
                .ToList(),
            _ => throw new InvalidOperationException($"Unknown standings scope {scope}."),
        };
    }

    /// <summary>
    /// The tables of the managed team's own division, conference, or the league, or of its
    /// conference's wild-card view (each division's top three, then the race).
    /// </summary>
    public List<StandingsTableViewModel> ManagedTeamTables(StandingsScope scope)
    {
        var standings = _session.Snapshot.Season.Standings;
        var results = ResultsByTeam();
        var (conference, division) = _session.FindDivision(_session.ManagedTeam);
        return scope switch
        {
            StandingsScope.League => [CreateTable("LEAGUE", $"{standings.League.Count} teams", standings.League, results)],
            StandingsScope.Conference => standings.Conferences.Where(entry => entry.Name == conference.Name)
                .Select(entry => CreateTable(entry.Name.ToUpperInvariant(), $"{entry.Teams.Count} teams", entry.Teams, results))
                .ToList(),
            StandingsScope.Division => standings.Conferences.Single(entry => entry.Name == conference.Name).Divisions
                .Where(entry => entry.Name == division.Name)
                .Select(entry => CreateTable(entry.Name.ToUpperInvariant(), conference.Name, entry.Teams, results))
                .ToList(),
            StandingsScope.WildCard => CreateWildCardTables(standings.WildCard.Single(entry => entry.Name == conference.Name), results).ToList(),
            _ => throw new InvalidOperationException($"Unknown standings scope {scope}."),
        };
    }

    /// <summary>Each team's regular-season results, oldest first, for its recent form.</summary>
    private Dictionary<TeamId, List<CompletedMatchSnapshot>> ResultsByTeam() =>
        TeamResults.ByTeam(_session.Snapshot.League.Teams.Select(team => team.Id), _session.Snapshot.Season.Results);

    /// <summary>
    /// Each division's leaders, then the conference's wild-card race with a line under the last
    /// wild card.
    /// </summary>
    private IEnumerable<StandingsTableViewModel> CreateWildCardTables(
        WildCardStandingsSnapshot conference,
        Dictionary<TeamId, List<CompletedMatchSnapshot>> results)
    {
        foreach (var division in conference.DivisionLeaders)
        {
            yield return CreateTable(division.Name.ToUpperInvariant(), conference.Name, division.Teams, results);
        }

        yield return CreateTable(
            $"{conference.Name.ToUpperInvariant()} WILD CARD",
            $"Top {conference.WildCardCount} qualify",
            conference.WildCardRace,
            results,
            lastQualifierIndex: conference.WildCardCount - 1);
    }

    private StandingsTableViewModel CreateTable(
        string title,
        string caption,
        IReadOnlyList<StandingsEntrySnapshot> entries,
        Dictionary<TeamId, List<CompletedMatchSnapshot>> results,
        int? lastQualifierIndex = null)
    {
        var managedTeamId = _session.Snapshot.ManagedTeamId;
        return new StandingsTableViewModel(
            title,
            caption,
            entries
                .Select((entry, index) =>
                {
                    var teamId = entry.Record.TeamId;
                    return StandingsRowViewModel.Create(entry, _session.GetTeam(teamId), teamId == managedTeamId, results[teamId]) with
                    {
                        IsLastQualifier = index == lastQualifierIndex,
                        OpenTeamCommand = _openTeam is null ? null : new RelayCommand(() => _openTeam(teamId)),
                    };
                })
                .ToList());
    }
}