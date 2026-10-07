using HockeySim.Domain;

namespace HockeySim.Simulation.Tests;

/// <summary>
/// Builds valid teams whose ratings are controlled per lineup role, so tests can isolate the
/// influence of a single line, pair, or goalie.
/// </summary>
internal static class TestTeams
{
    public const int AverageRating = 65;

    private const int CentreCount = 5;
    private const int WingCount = 8;
    private const int DefenceCount = 7;
    private const int GoalieCount = 3;

    private static int _nextIdentity;

    /// <param name="ratingFor">
    /// Every rating for a player, given their role. Defaults to <see cref="AverageRating"/>.
    /// </param>
    public static Team Create(string name, Func<LineupRole, int>? ratingFor = null)
    {
        ratingFor ??= _ => AverageRating;
        var roster = new List<Player>(Team.RequiredRosterSize);

        List<Player> AddPlayers(Position position, int count, Func<int, LineupRole> roleForIndex)
        {
            var players = Enumerable.Range(0, count)
                .Select(index => CreatePlayer(position, roster.Count + index + 1, ratingFor(roleForIndex(index))))
                .ToList();
            roster.AddRange(players);
            return players;
        }

        var centres = AddPlayers(Position.Centre, CentreCount, ForwardRole);
        var wings = AddPlayers(Position.Wing, WingCount, index => ForwardRole(index / 2));
        var defence = AddPlayers(Position.Defence, DefenceCount, index => DefenceRole(index / 2));
        var goalies = AddPlayers(Position.Goalie, GoalieCount, GoalieRole);

        var forwardLines = Enumerable.Range(0, Lineup.RequiredForwardLineCount)
            .Select(index => new ForwardLine(wings[index * 2], centres[index], wings[(index * 2) + 1]));
        var defencePairs = Enumerable.Range(0, Lineup.RequiredDefencePairCount)
            .Select(index => new DefencePair(defence[index * 2], defence[(index * 2) + 1]));
        var lineup = Lineup.CreateWithDefaultUnits(forwardLines, defencePairs, goalies[0], goalies[1]);

        return new Team(new TeamId(NextGuid()), name, roster, lineup);
    }

    public static Match CreateMatch(Team home, Team away) => new(home, away);

    private static LineupRole ForwardRole(int index) =>
        index < Lineup.RequiredForwardLineCount ? new LineupRole(LineupRoleKind.ForwardLine, index) : LineupRole.Scratch;

    private static LineupRole DefenceRole(int index) =>
        index < Lineup.RequiredDefencePairCount ? new LineupRole(LineupRoleKind.DefencePair, index) : LineupRole.Scratch;

    private static LineupRole GoalieRole(int index) => index switch
    {
        0 => new LineupRole(LineupRoleKind.StartingGoalie, 0),
        1 => new LineupRole(LineupRoleKind.BackupGoalie, 0),
        _ => LineupRole.Scratch,
    };

    private static Player CreatePlayer(Position position, int number, int rating) =>
        new(
            new PlayerId(NextGuid()),
            "Test",
            $"{position}{number}",
            position,
            TestBiography.Create(),
            number,
            Enum.GetValues<Rating>().ToDictionary(value => value, _ => new RatingScore(rating)));

    private static Guid NextGuid()
    {
        var identity = Interlocked.Increment(ref _nextIdentity);
        return new Guid(identity, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]);
    }
}

internal enum LineupRoleKind
{
    ForwardLine,
    DefencePair,
    StartingGoalie,
    BackupGoalie,
    Scratch,
}

/// <param name="Index">Zero-based forward line or defence pair; zero for other roles.</param>
internal readonly record struct LineupRole(LineupRoleKind Kind, int Index)
{
    public static LineupRole Scratch { get; } = new(LineupRoleKind.Scratch, 0);
}