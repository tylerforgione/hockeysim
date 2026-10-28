using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Schedule;
using HockeySim.Desktop.Tables;
using HockeySim.Domain;

namespace HockeySim.Desktop.Teams;

/// <summary>
/// A team's matches in every phase with their results, as the Schedule tab of its team page.
/// Choosing a completed match opens its box score on the schedule page.
/// </summary>
public sealed partial class TeamScheduleViewModel : ObservableObject
{
    private readonly GameSession _session;
    private readonly TeamId _teamId;
    private readonly Action<DateOnly, TeamId> _openMatch;
    private readonly Action<TeamId>? _openTeam;

    [ObservableProperty]
    private IReadOnlyList<ScheduleMatchRowViewModel> _matches = [];

    /// <summary>The row chosen; choosing a completed one opens its box score, after which nothing is chosen again.</summary>
    [ObservableProperty]
    private ScheduleMatchRowViewModel? _selectedMatch;

    /// <summary>The table's sort, or none for date order.</summary>
    [ObservableProperty]
    private TableSort? _matchesSort;

    /// <param name="openMatch">Opens a match's box score in the given team's schedule.</param>
    /// <param name="openTeam">Opens an opponent's team page.</param>
    public TeamScheduleViewModel(GameSession session, TeamId teamId, Action<DateOnly, TeamId> openMatch, Action<TeamId>? openTeam = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(openMatch);

        _session = session;
        _teamId = teamId;
        _openMatch = openMatch;
        _openTeam = openTeam;
        Refresh();
    }

    public void Refresh()
    {
        Matches = ScheduleRows.Create(_session, _teamId, MatchesSort, _openTeam);
    }

    [RelayCommand]
    private void SortMatches(string column)
    {
        MatchesSort = TableSorting.Click(ScheduleTableColumns.Matches, MatchesSort, column);
        Matches = ScheduleRows.Sorted(Matches, MatchesSort);
    }

    partial void OnSelectedMatchChanged(ScheduleMatchRowViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedMatch = null;
        if (value.IsCompleted)
        {
            _openMatch(value.Date, _teamId);
        }
    }
}