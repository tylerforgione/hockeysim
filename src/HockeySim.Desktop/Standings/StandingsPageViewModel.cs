using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Domain;

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
    private readonly StandingsTableBuilder _tables;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLeagueScope), nameof(IsConferenceScope), nameof(IsDivisionScope), nameof(IsWildCardScope))]
    private StandingsScope _scope = StandingsScope.Division;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Tables))]
    private IReadOnlyList<StandingsColumnViewModel> _columns = [];

    /// <param name="openTeam">Opens a team from its name; without it, names link nowhere.</param>
    public StandingsPageViewModel(GameSession session, Action<TeamId>? openTeam = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _tables = new StandingsTableBuilder(session, openTeam);
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

    private List<StandingsColumnViewModel> CreateColumns() => _tables.Columns(Scope);
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