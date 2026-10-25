using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Standings;
using HockeySim.Domain;

namespace HockeySim.Desktop.Home;

/// <summary>
/// A compact standings table around the managed team: its division, its conference's wild-card
/// view, its conference, or the league. The scope holds across refreshes and days.
/// </summary>
public sealed partial class StandingsPanelViewModel : ObservableObject
{
    private readonly Action<ShellPage> _navigate;
    private readonly StandingsTableBuilder _builder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDivisionScope), nameof(IsWildCardScope), nameof(IsConferenceScope), nameof(IsLeagueScope))]
    private StandingsScope _scope = StandingsScope.Division;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<StandingsPanelTableViewModel> _tables = [];

    public StandingsPanelViewModel(GameSession session, Action<ShellPage> navigate, Action<TeamId> openTeam)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(navigate);
        ArgumentNullException.ThrowIfNull(openTeam);

        _navigate = navigate;
        _builder = new StandingsTableBuilder(session, openTeam);
        Refresh();
    }

    public bool IsDivisionScope => Scope == StandingsScope.Division;

    public bool IsWildCardScope => Scope == StandingsScope.WildCard;

    public bool IsConferenceScope => Scope == StandingsScope.Conference;

    public bool IsLeagueScope => Scope == StandingsScope.League;

    public void Refresh()
    {
        var tables = _builder.ManagedTeamTables(Scope);

        // The wild-card view stacks several tables; the panel is named for the last, the race, and
        // the others carry their own titles.
        Title = tables[^1].Title;
        Tables = tables.Select(table => new StandingsPanelTableViewModel(table, table.Title != Title)).ToList();
    }

    [RelayCommand]
    private void SelectScope(StandingsScope scope)
    {
        Scope = scope;
    }

    [RelayCommand]
    private void OpenStandings()
    {
        _navigate(ShellPage.Standings);
    }

    partial void OnScopeChanged(StandingsScope value)
    {
        Refresh();
    }
}

/// <param name="ShowTitle">Whether the table is headed with its title, because the panel's own names another.</param>
public sealed record StandingsPanelTableViewModel(StandingsTableViewModel Table, bool ShowTitle)
{
    public string Title => Table.Title;

    public IReadOnlyList<StandingsRowViewModel> Rows => Table.Rows;
}