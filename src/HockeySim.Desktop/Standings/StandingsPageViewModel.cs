using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Standings;

/// <summary>
/// Current-season standings for the league, each conference, or each division. Management ranks
/// every table independently; this page only presents them.
/// </summary>
public sealed partial class StandingsPageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLeagueScope), nameof(IsConferenceScope), nameof(IsDivisionScope))]
    private StandingsScope _scope = StandingsScope.Division;

    [ObservableProperty]
    private IReadOnlyList<StandingsTableViewModel> _tables = [];

    public StandingsPageViewModel(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        Refresh();
    }

    public override string Title => "Standings";

    public override string Subtitle
    {
        get
        {
            var snapshot = _session.Snapshot;
            var season = $"{PlayerDisplay.FormatSeason(snapshot.League.SeasonYear)} regular season";
            if (snapshot.Season.IsComplete)
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

    public override void Refresh()
    {
        Tables = CreateTables();
        OnPropertyChanged(nameof(Subtitle));
    }

    [RelayCommand]
    private void SelectScope(StandingsScope scope)
    {
        Scope = scope;
    }

    partial void OnScopeChanged(StandingsScope value)
    {
        Tables = CreateTables();
    }

    private List<StandingsTableViewModel> CreateTables()
    {
        var standings = _session.Snapshot.Season.Standings;
        return Scope switch
        {
            StandingsScope.League =>
            [
                CreateTable("LEAGUE", $"{standings.League.Count} teams", standings.League),
            ],
            StandingsScope.Conference => standings.Conferences
                .Select(conference => CreateTable(conference.Name.ToUpperInvariant(), $"{conference.Teams.Count} teams", conference.Teams))
                .ToList(),
            StandingsScope.Division => standings.Conferences
                .SelectMany(conference => conference.Divisions
                    .Select(division => CreateTable(division.Name.ToUpperInvariant(), conference.Name, division.Teams)))
                .ToList(),
            _ => throw new InvalidOperationException($"Unknown standings scope {Scope}."),
        };
    }

    private StandingsTableViewModel CreateTable(string title, string caption, IReadOnlyList<StandingsEntrySnapshot> entries)
    {
        var managedTeamId = _session.Snapshot.ManagedTeamId;
        return new StandingsTableViewModel(
            title,
            caption,
            entries
                .Select(entry => StandingsRowViewModel.Create(
                    entry,
                    _session.GetTeam(entry.Record.TeamId).Name,
                    entry.Record.TeamId == managedTeamId))
                .ToList());
    }
}

public enum StandingsScope
{
    League,
    Conference,
    Division,
}

/// <summary>One ranked table: the league, a conference, or a division, best first.</summary>
public sealed record StandingsTableViewModel(string Title, string Caption, IReadOnlyList<StandingsRowViewModel> Rows);