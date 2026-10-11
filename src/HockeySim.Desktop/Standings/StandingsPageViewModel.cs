using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Standings;

/// <summary>
/// Current-season standings for the league, each conference, each division, or each conference's
/// wild-card view, with every team's clinch or elimination marker and recent form. Management ranks
/// every table independently and decides the markers; this page only presents them, with the two
/// conferences side by side.
/// </summary>
public sealed partial class StandingsPageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLeagueScope), nameof(IsConferenceScope), nameof(IsDivisionScope), nameof(IsWildCardScope))]
    private StandingsScope _scope = StandingsScope.Division;

    private readonly Action<TeamId>? _openTeam;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Tables))]
    private IReadOnlyList<StandingsColumnViewModel> _columns = [];

    /// <param name="openTeam">Opens a team from its name; without it, names link nowhere.</param>
    public StandingsPageViewModel(GameSession session, Action<TeamId>? openTeam = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _openTeam = openTeam;
        Refresh();
    }

    /// <summary>Every table, column by column.</summary>
    public IReadOnlyList<StandingsTableViewModel> Tables => Columns.SelectMany(column => column.Tables).ToList();

    public override string Subtitle
    {
        get
        {
            var snapshot = _session.Snapshot;
            var season = $"{PlayerDisplay.FormatSeason(snapshot.League.SeasonYear)} regular season";
            if (snapshot.Season.IsRegularSeasonComplete)
            {
                return $"{season} · final standings";
            }

            var played = snapshot.Season.Results.Count;
            return played == 0
                ? $"{season} · no matches played yet"
                : $"{season} · {played:N0} of {snapshot.Schedule.Matches.Count:N0} matches played";
        }
    }

    public bool IsLeagueScope => Scope == StandingsScope.League;

    public bool IsConferenceScope => Scope == StandingsScope.Conference;

    public bool IsDivisionScope => Scope == StandingsScope.Division;

    public bool IsWildCardScope => Scope == StandingsScope.WildCard;

    public override void Refresh()
    {
        Columns = CreateColumns();
        OnPropertyChanged(nameof(Subtitle));
    }

    [RelayCommand]
    private void SelectScope(StandingsScope scope)
    {
        Scope = scope;
    }

    partial void OnScopeChanged(StandingsScope value)
    {
        Columns = CreateColumns();
    }

    /// <summary>
    /// One column per conference, side by side, in every view built from conferences; the league
    /// view is a single column.
    /// </summary>
    private List<StandingsColumnViewModel> CreateColumns()
    {
        var standings = _session.Snapshot.Season.Standings;
        var results = ResultsByTeam();
        return Scope switch
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
            _ => throw new InvalidOperationException($"Unknown standings scope {Scope}."),
        };
    }

    /// <summary>Each team's regular-season results, oldest first, for its recent form.</summary>
    private Dictionary<TeamId, List<CompletedMatchSnapshot>> ResultsByTeam()
    {
        var results = _session.Snapshot.League.Teams.ToDictionary(team => team.Id, _ => new List<CompletedMatchSnapshot>());
        foreach (var result in _session.Snapshot.Season.Results)
        {
            results[result.Home.TeamId].Add(result);
            results[result.Away.TeamId].Add(result);
        }

        return results;
    }

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

/// <summary>The tables stacked in one column of the page: a conference's, or the league's.</summary>
public sealed record StandingsColumnViewModel(IReadOnlyList<StandingsTableViewModel> Tables);

public enum StandingsScope
{
    League,
    Conference,
    Division,
    WildCard,
}

/// <summary>One ranked table: the league, a conference, or a division, best first.</summary>
public sealed record StandingsTableViewModel(string Title, string Caption, IReadOnlyList<StandingsRowViewModel> Rows);