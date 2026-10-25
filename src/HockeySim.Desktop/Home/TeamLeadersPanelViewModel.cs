using System.Globalization;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Schedule;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Home;

/// <summary>
/// The managed team's leaders in points, goals, assists, five-on-five expected-goal share, save
/// percentage, and goals saved above expected, from the statistics set the home page shows. The
/// rate statistics only count players who have played a chosen share of the team's games, so a
/// player with one good game does not lead; the choice holds across refreshes and days.
/// </summary>
public sealed partial class TeamLeadersPanelViewModel : ObservableObject
{
    private readonly GameSession _session;
    private readonly Action<PlayerId> _openPlayer;

    [ObservableProperty]
    private IReadOnlyList<LeaderTileViewModel> _tiles = [];

    [ObservableProperty]
    private MinimumGamesOption _minimumGames = MinimumGamesOption.Default;

    public TeamLeadersPanelViewModel(GameSession session, Action<PlayerId> openPlayer)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(openPlayer);

        _session = session;
        _openPlayer = openPlayer;
        Refresh();
    }

    public IReadOnlyList<MinimumGamesOption> MinimumGamesOptions => MinimumGamesOption.All;

    public void Refresh()
    {
        var snapshot = _session.Snapshot;
        var season = snapshot.Season;
        var teamId = snapshot.ManagedTeamId;
        var statistics = HomeStatistics.For(snapshot, teamId);
        var roster = _session.ManagedTeam.Roster.ToDictionary(player => player.Id);
        var teamGames = HomeStatistics.Records(season, statistics).SingleOrDefault(record => record.TeamId == teamId)?.GamesPlayed ?? 0;
        var required = MinimumGames.RequiredGames(teamGames);
        var skaters = HomeStatistics.Skaters(season, statistics, teamId).ToList();
        var goalies = HomeStatistics.Goalies(season, statistics, teamId).ToList();

        Tiles =
        [
            Count("Points", skaters, skater => skater.Points),
            Count("Goals", skaters, skater => skater.Goals),
            Count("Assists", skaters, skater => skater.Assists),
            Leader(
                "5v5 xGF%",
                skaters.Where(skater => skater.GamesPlayed >= required),
                skater => skater.OnIce.FiveOnFive.ExpectedGoalsPercentage,
                skater => (skater.PlayerId, skater.GamesPlayed),
                MatchDisplay.Percentage),
            Leader(
                "Save %",
                goalies.Where(goalie => goalie.GamesPlayed >= required),
                goalie => goalie.SavePercentage,
                goalie => (goalie.PlayerId, goalie.GamesPlayed),
                value => MatchDisplay.SavePercentage(value)),
            Leader(
                "GSAx",
                goalies.Where(goalie => goalie.GamesPlayed > 0),
                goalie => goalie.GoalsSavedAboveExpected,
                goalie => (goalie.PlayerId, goalie.GamesPlayed),
                value => MatchDisplay.GoalsSavedAboveExpected(value!.Value)),
        ];

        LeaderTileViewModel Count(string label, IEnumerable<SkaterSeasonStatisticsSnapshot> candidates, Func<SkaterSeasonStatisticsSnapshot, int> count) =>
            Leader(
                label,
                candidates.Where(skater => count(skater) > 0),
                skater => count(skater),
                skater => (skater.PlayerId, skater.GamesPlayed),
                value => value!.Value.ToString("0", CultureInfo.CurrentCulture));

        // Best value first, then the player with more games, then by last name; a candidate
        // without a value (a rate not yet defined) is passed over.
        LeaderTileViewModel Leader<T>(
            string label,
            IEnumerable<T> candidates,
            Func<T, double?> value,
            Func<T, (PlayerId PlayerId, int GamesPlayed)> player,
            Func<double?, string> format)
        {
            var leaders = candidates
                .Where(candidate => value(candidate) is not null)
                .OrderByDescending(candidate => value(candidate))
                .ThenByDescending(candidate => player(candidate).GamesPlayed)
                .ThenBy(candidate => roster[player(candidate).PlayerId].LastName, StringComparer.CurrentCulture)
                .Take(1)
                .ToList();
            if (leaders.Count == 0)
            {
                return new LeaderTileViewModel(label, string.Empty, MatchDisplay.Undefined, null);
            }

            var leader = leaders[0];
            var playerId = player(leader).PlayerId;
            return new LeaderTileViewModel(
                label,
                PlayerDisplay.ShortName(roster[playerId]),
                format(value(leader)),
                new RelayCommand(() => _openPlayer(playerId)));
        }
    }

    partial void OnMinimumGamesChanged(MinimumGamesOption value)
    {
        Refresh();
    }
}

/// <summary>One leader: the statistic, the player leading it, and the value; a dash and no player when nobody leads.</summary>
public sealed record LeaderTileViewModel(string Label, string PlayerName, string Value, ICommand? OpenPlayerCommand);

/// <summary>
/// A share of the team's games a player must have played to lead a rate statistic.
/// </summary>
/// <param name="Share">The share from zero to one; zero means any appearance.</param>
public sealed record MinimumGamesOption(string Label, double Share)
{
    public static IReadOnlyList<MinimumGamesOption> All { get; } =
    [
        new("Any", 0),
        new("25%", 0.25),
        new("50%", 0.5),
        new("75%", 0.75),
    ];

    public static MinimumGamesOption Default => All[2];

    /// <summary>The games a player needs, the share of the team's games rounded up; at least one.</summary>
    public int RequiredGames(int teamGamesPlayed) => Math.Max(1, (int)Math.Ceiling(Share * teamGamesPlayed));

    public override string ToString() => Label;
}