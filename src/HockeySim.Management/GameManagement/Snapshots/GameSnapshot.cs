using System.Collections.ObjectModel;

using HockeySim.Domain;
using HockeySim.Management.NewGame;

namespace HockeySim.Management.GameManagement.Snapshots;

public sealed class GameSnapshot
{
    private GameSnapshot(LeagueSnapshot league, TeamId managedTeamId, RandomState randomState)
    {
        League = league;
        ManagedTeamId = managedTeamId;
        RandomState = randomState;
    }

    public LeagueSnapshot League { get; }

    public TeamId ManagedTeamId { get; }

    /// <summary>
    /// Gets the random state to preserve with the game world for deterministic continuation.
    /// </summary>
    public RandomState RandomState { get; }

    internal static GameSnapshot Create(League league, TeamId managedTeamId, RandomState randomState) =>
        new(LeagueSnapshot.Create(league), managedTeamId, randomState);
}

public sealed class LeagueSnapshot
{
    private readonly ReadOnlyCollection<ConferenceSnapshot> _conferences;
    private readonly ReadOnlyCollection<TeamSnapshot> _teams;

    private LeagueSnapshot(
        int seasonYear,
        IReadOnlyList<ConferenceSnapshot> conferences,
        IReadOnlyList<TeamSnapshot> teams)
    {
        SeasonYear = seasonYear;
        _conferences = new ReadOnlyCollection<ConferenceSnapshot>(conferences.ToList());
        _teams = new ReadOnlyCollection<TeamSnapshot>(teams.ToList());
    }

    public int SeasonYear { get; }

    public IReadOnlyList<ConferenceSnapshot> Conferences => _conferences;

    public IReadOnlyList<TeamSnapshot> Teams => _teams;

    internal static LeagueSnapshot Create(League league)
    {
        var teams = league.Teams
            .Select(TeamSnapshot.Create)
            .ToDictionary(team => team.Id);
        var conferences = league.Conferences
            .Select(conference => ConferenceSnapshot.Create(conference, teams))
            .ToList();

        var orderedTeams = league.Teams.Select(team => teams[team.Id]).ToList();
        return new LeagueSnapshot(league.SeasonYear, conferences, orderedTeams);
    }
}

public sealed class ConferenceSnapshot
{
    private readonly ReadOnlyCollection<DivisionSnapshot> _divisions;

    private ConferenceSnapshot(string name, IReadOnlyList<DivisionSnapshot> divisions)
    {
        Name = name;
        _divisions = new ReadOnlyCollection<DivisionSnapshot>(divisions.ToList());
    }

    public string Name { get; }

    public IReadOnlyList<DivisionSnapshot> Divisions => _divisions;

    internal static ConferenceSnapshot Create(
        Conference conference,
        IReadOnlyDictionary<TeamId, TeamSnapshot> teams) =>
        new(
            conference.Name,
            conference.Divisions.Select(division => DivisionSnapshot.Create(division, teams)).ToList());
}

public sealed class DivisionSnapshot
{
    private readonly ReadOnlyCollection<TeamSnapshot> _teams;

    private DivisionSnapshot(string name, IReadOnlyList<TeamSnapshot> teams)
    {
        Name = name;
        _teams = new ReadOnlyCollection<TeamSnapshot>(teams.ToList());
    }

    public string Name { get; }

    public IReadOnlyList<TeamSnapshot> Teams => _teams;

    internal static DivisionSnapshot Create(
        Division division,
        IReadOnlyDictionary<TeamId, TeamSnapshot> teams) =>
        new(division.Name, division.Teams.Select(team => teams[team.Id]).ToList());
}

public sealed class TeamSnapshot
{
    private readonly ReadOnlyCollection<PlayerSnapshot> _roster;

    public IReadOnlyList<PlayerId> _scratchedPlayerIds;

    private TeamSnapshot(
        TeamId id,
        string name,
        IReadOnlyList<PlayerSnapshot> roster,
        IReadOnlyList<PlayerId> scratchedPlayerIds,
        LineupSnapshot lineup)
    {
        Id = id;
        Name = name;
        _roster = new ReadOnlyCollection<PlayerSnapshot>(roster.ToList());
        Lineup = lineup;
        _scratchedPlayerIds = new ReadOnlyCollection<PlayerId>(
            scratchedPlayerIds.ToList()
        );
    }

    public TeamId Id { get; }

    public string Name { get; }

    public IReadOnlyList<PlayerSnapshot> Roster => _roster;

    public LineupSnapshot Lineup { get; }

    internal static TeamSnapshot Create(Team team)
    {
        var dressedPlayerIds = team.Lineup.DressedPlayers.Select(
            player => player.Id
        ).ToHashSet();

        var scratchedPlayerIds = team.Roster.Where(
            player => !dressedPlayerIds.Contains(player.Id)
        ).Select(player => player.Id).ToList();

        return new(
            team.Id,
            team.Name,
            team.Roster.Select(PlayerSnapshot.Create).ToList(),
            scratchedPlayerIds,
            LineupSnapshot.Create(team.Lineup));
    }
}

public sealed class PlayerSnapshot
{
    private readonly ReadOnlyDictionary<Rating, int> _ratings;

    private PlayerSnapshot(
        PlayerId id,
        string firstName,
        string lastName,
        Position position,
        int age,
        int number,
        IReadOnlyDictionary<Rating, int> ratings)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Position = position;
        Age = age;
        Number = number;
        _ratings = new ReadOnlyDictionary<Rating, int>(new Dictionary<Rating, int>(ratings));
    }

    public PlayerId Id { get; }

    public string FirstName { get; }

    public string LastName { get; }

    public Position Position { get; }

    public int Age { get; }

    public int Number { get; }

    public IReadOnlyDictionary<Rating, int> Ratings => _ratings;

    internal static PlayerSnapshot Create(Player player) =>
        new(
            player.Id,
            player.FirstName,
            player.LastName,
            player.Position,
            player.Age,
            player.Number,
            player.Ratings.ToDictionary(pair => pair.Key, pair => pair.Value.Value));
}

public sealed class LineupSnapshot
{
    private readonly ReadOnlyCollection<ForwardLineSnapshot> _forwardLines;
    private readonly ReadOnlyCollection<DefencePairSnapshot> _defencePairs;
    private readonly ReadOnlyCollection<PlayerId> _dressedPlayerIds;

    private LineupSnapshot(
        IReadOnlyList<ForwardLineSnapshot> forwardLines,
        IReadOnlyList<DefencePairSnapshot> defencePairs,
        PlayerId startingGoalieId,
        PlayerId backupGoalieId,
        IReadOnlyList<PlayerId> dressedPlayerIds)
    {
        _forwardLines = new ReadOnlyCollection<ForwardLineSnapshot>(forwardLines.ToList());
        _defencePairs = new ReadOnlyCollection<DefencePairSnapshot>(defencePairs.ToList());
        StartingGoalieId = startingGoalieId;
        BackupGoalieId = backupGoalieId;
        _dressedPlayerIds = new ReadOnlyCollection<PlayerId>(dressedPlayerIds.ToList());
    }

    public IReadOnlyList<ForwardLineSnapshot> ForwardLines => _forwardLines;

    public IReadOnlyList<DefencePairSnapshot> DefencePairs => _defencePairs;

    public PlayerId StartingGoalieId { get; }

    public PlayerId BackupGoalieId { get; }

    public IReadOnlyList<PlayerId> DressedPlayerIds => _dressedPlayerIds;

    internal static LineupSnapshot Create(Lineup lineup) =>
        new(
            lineup.ForwardLines.Select(ForwardLineSnapshot.Create).ToList(),
            lineup.DefencePairs.Select(DefencePairSnapshot.Create).ToList(),
            lineup.StartingGoalie.Id,
            lineup.BackupGoalie.Id,
            lineup.DressedPlayers.Select(player => player.Id).ToList());
}

public sealed record ForwardLineSnapshot(
    PlayerId LeftWingId,
    PlayerId CentreId,
    PlayerId RightWingId)
{
    internal static ForwardLineSnapshot Create(ForwardLine line) =>
        new(line.LeftWing.Id, line.Centre.Id, line.RightWing.Id);
}

public sealed record DefencePairSnapshot(PlayerId LeftDefenceId, PlayerId RightDefenceId)
{
    internal static DefencePairSnapshot Create(DefencePair pair) =>
        new(pair.LeftDefence.Id, pair.RightDefence.Id);
}