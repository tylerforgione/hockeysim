using System.Collections.ObjectModel;

namespace HockeySim.Domain;

/// <summary>
/// A best-of-seven series: the first team to <see cref="WinsRequired"/> wins advances. The
/// higher-ranked team has home ice in a 2-2-1-1-1 pattern: it hosts games one, two, five, and
/// seven. Games are scheduled one at a time, so a decided series schedules no more.
/// </summary>
public sealed class PlayoffSeries
{
    public const int WinsRequired = 4;
    public const int MaximumGames = (2 * WinsRequired) - 1;

    // Game by game, whether the higher-ranked team is at home: 2-2-1-1-1.
    private static readonly bool[] HigherRankedTeamHosts = [true, true, false, false, true, false, true];

    private readonly List<CompletedMatch> _games = [];
    private readonly ReadOnlyCollection<CompletedMatch> _gamesView;

    internal PlayoffSeries(PlayoffRound round, PlayoffSeed higherRanked, PlayoffSeed lowerRanked)
    {
        Round = round;
        HigherRanked = higherRanked;
        LowerRanked = lowerRanked;
        _gamesView = _games.AsReadOnly();
    }

    public PlayoffRound Round { get; }

    /// <summary>The team with home ice.</summary>
    public PlayoffSeed HigherRanked { get; }

    public PlayoffSeed LowerRanked { get; }

    /// <summary>The games played so far, in order.</summary>
    public IReadOnlyList<CompletedMatch> Games => _gamesView;

    /// <summary>The scheduled game not yet played, if any; none once the series is decided.</summary>
    public ScheduledMatch? NextGame { get; private set; }

    public int HigherRankedWins => WinsOf(HigherRanked.TeamId);

    public int LowerRankedWins => WinsOf(LowerRanked.TeamId);

    public bool IsDecided => Winner is not null;

    /// <summary>The team that reached <see cref="WinsRequired"/> wins, once one has.</summary>
    public PlayoffSeed? Winner =>
        HigherRankedWins == WinsRequired ? HigherRanked
        : LowerRankedWins == WinsRequired ? LowerRanked
        : null;

    public int WinsOf(TeamId teamId) =>
        _games.Count(game => (game.Home.Score > game.Away.Score ? game.Home.TeamId : game.Away.TeamId) == teamId);

    public bool Involves(TeamId teamId) => HigherRanked.TeamId == teamId || LowerRanked.TeamId == teamId;

    /// <summary>Schedules the series' next game on <paramref name="date"/>, hosted by the 2-2-1-1-1 pattern.</summary>
    internal ScheduledMatch ScheduleNextGame(DateOnly date)
    {
        if (IsDecided || NextGame is not null)
        {
            throw new InvalidOperationException("A series schedules its next game only while undecided and after the previous one.");
        }

        var (home, away) = HigherRankedTeamHosts[_games.Count]
            ? (HigherRanked, LowerRanked)
            : (LowerRanked, HigherRanked);
        NextGame = new ScheduledMatch(date, home.TeamId, away.TeamId);
        return NextGame;
    }

    internal void Record(CompletedMatch game)
    {
        if (!ReferenceEquals(game.ScheduledMatch, NextGame))
        {
            throw new ArgumentException("A series records only its scheduled next game.", nameof(game));
        }

        _games.Add(game);
        NextGame = null;
    }
}