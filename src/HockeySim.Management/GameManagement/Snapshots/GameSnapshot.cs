using System.Collections.ObjectModel;

using HockeySim.Domain;
using HockeySim.Management.Inbox;
using HockeySim.Management.Lineups;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Management.GameManagement.Snapshots;

public sealed class GameSnapshot
{
    private readonly ReadOnlyCollection<InboxMessageSnapshot> _inbox;
    private readonly ReadOnlyCollection<PlayerId> _playersToReplace;

    private GameSnapshot(
        LeagueSnapshot league,
        ScheduleSnapshot schedule,
        SeasonSnapshot season,
        TeamId managedTeamId,
        IReadOnlyList<PlayerId> playersToReplace,
        RandomState randomState,
        IReadOnlyList<InboxMessageSnapshot> inbox)
    {
        League = league;
        Schedule = schedule;
        Season = season;
        ManagedTeamId = managedTeamId;
        _playersToReplace = new ReadOnlyCollection<PlayerId>(playersToReplace.ToList());
        RandomState = randomState;
        _inbox = new ReadOnlyCollection<InboxMessageSnapshot>(inbox.ToList());
    }

    public LeagueSnapshot League { get; }

    public ScheduleSnapshot Schedule { get; }

    public SeasonSnapshot Season { get; }

    public TeamId ManagedTeamId { get; }

    /// <summary>
    /// Gets the managed team's dressed players who cannot play today, in lineup order. The day
    /// cannot be played until they are replaced. Empty when the team does not play today.
    /// </summary>
    public IReadOnlyList<PlayerId> PlayersToReplace => _playersToReplace;

    /// <summary>
    /// Gets the random state to preserve with the game world for deterministic continuation.
    /// </summary>
    public RandomState RandomState { get; }

    /// <summary>
    /// Gets the messages delivered to the user, newest first.
    /// </summary>
    public IReadOnlyList<InboxMessageSnapshot> Inbox => _inbox;

    internal static GameSnapshot Create(
        Season season,
        TeamId managedTeamId,
        IReadOnlyList<PlayerId> playersToReplace,
        RandomState randomState,
        InboxMessages inbox) =>
        new(
            LeagueSnapshot.Create(season, managedTeamId),
            ScheduleSnapshot.Create(season.Schedule),
            SeasonSnapshot.Create(season),
            managedTeamId,
            playersToReplace,
            randomState,
            inbox.Messages.Select(InboxMessageSnapshot.Create).ToList());
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

    /// <summary>
    /// The league on the season's current date, which players' ages and injuries are given on.
    /// </summary>
    internal static LeagueSnapshot Create(Season season, TeamId managedTeamId)
    {
        var league = season.League;
        var teams = league.Teams
            .Select(team => TeamSnapshot.Create(team, season, team.Id == managedTeamId))
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

    private readonly ReadOnlyCollection<PlayerId> _scratchedPlayerIds;

    private TeamSnapshot(
        TeamId id,
        string name,
        TeamColours colours,
        IReadOnlyList<PlayerSnapshot> roster,
        IReadOnlyList<PlayerId> scratchedPlayerIds,
        LineupSnapshot lineup)
    {
        Id = id;
        Name = name;
        Colours = colours;
        _roster = new ReadOnlyCollection<PlayerSnapshot>(roster.ToList());
        Lineup = lineup;
        _scratchedPlayerIds = new ReadOnlyCollection<PlayerId>(
            scratchedPlayerIds.ToList()
        );
    }

    public TeamId Id { get; }

    public string Name { get; }

    public TeamColours Colours { get; }

    public IReadOnlyList<PlayerSnapshot> Roster => _roster;

    public IReadOnlyList<PlayerId> ScratchedPlayerIds => _scratchedPlayerIds;

    /// <summary>
    /// Gets the lineup the team dresses today. The managed team's is the lineup the user set, even
    /// if it holds players who cannot play (see <see cref="GameSnapshot.PlayersToReplace"/>); an AI
    /// team's has them replaced from its healthy scratches.
    /// </summary>
    public LineupSnapshot Lineup { get; }

    internal static TeamSnapshot Create(Team team, Season season, bool isManaged)
    {
        var lineup = isManaged ? team.Lineup : MatchDayLineup.ForAiTeam(team, season);
        var dressedPlayerIds = lineup.DressedPlayers.Select(
            player => player.Id
        ).ToHashSet();

        var scratchedPlayerIds = team.Roster.Where(
            player => !dressedPlayerIds.Contains(player.Id)
        ).Select(player => player.Id).ToList();

        return new(
            team.Id,
            team.Name,
            team.Colours,
            team.Roster.Select(player => PlayerSnapshot.Create(player, season)).ToList(),
            scratchedPlayerIds,
            LineupSnapshot.Create(lineup));
    }
}

/// <summary>
/// A player as the user may see them. Durability is hidden information, so it is absent from
/// <see cref="Ratings"/>; every other rating is present. Wear is hidden too, and so is how long
/// each injury will take: only the staff's <see cref="InjurySnapshot.ExpectedReturn"/> is shown.
/// </summary>
/// <remarks>
/// <see cref="Age"/> and <see cref="Injuries"/> are as of the game's current date when the
/// snapshot was taken.
/// </remarks>
public sealed class PlayerSnapshot
{
    private readonly ReadOnlyDictionary<Rating, int> _ratings;
    private readonly ReadOnlyCollection<InjurySnapshot> _injuries;

    private PlayerSnapshot(
        PlayerId id,
        string firstName,
        string lastName,
        Position position,
        PlayerBiography biography,
        int age,
        int number,
        int overall,
        IReadOnlyDictionary<Rating, int> ratings,
        IReadOnlyList<InjurySnapshot> injuries)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Position = position;
        Biography = biography;
        Age = age;
        Number = number;
        Overall = overall;
        _ratings = new ReadOnlyDictionary<Rating, int>(new Dictionary<Rating, int>(ratings));
        _injuries = new ReadOnlyCollection<InjurySnapshot>(injuries.ToList());
    }

    public PlayerId Id { get; }

    public string FirstName { get; }

    public string LastName { get; }

    public Position Position { get; }

    /// <summary>Birth date, birthplace, nationality, handedness, height, and weight.</summary>
    public PlayerBiography Biography { get; }

    public int Age { get; }

    public int Number { get; }

    /// <summary>The player's overall rating for their position.</summary>
    public int Overall { get; }

    /// <summary>Every rating visible to the user, which excludes <see cref="Rating.Durability"/>.</summary>
    public IReadOnlyDictionary<Rating, int> Ratings => _ratings;

    /// <summary>The injuries not yet healed, in the order suffered.</summary>
    public IReadOnlyList<InjurySnapshot> Injuries => _injuries;

    /// <summary>Whether every injury the player has is one they can play through.</summary>
    public bool CanPlay => _injuries.All(injury => injury.CanPlayThrough);

    internal static PlayerSnapshot Create(Player player, Season season)
    {
        var currentDate = season.CurrentDate;
        return new(
            player.Id,
            player.FirstName,
            player.LastName,
            player.Position,
            player.Biography,
            player.AgeOn(currentDate),
            player.Number,
            player.Overall.Value,
            player.Ratings
                .Where(pair => pair.Key != Rating.Durability)
                .ToDictionary(pair => pair.Key, pair => pair.Value.Value),
            season.HealthOf(player.Id).InjuriesOn(currentDate).Select(InjurySnapshot.Create).ToList());
    }
}

/// <summary>An injury a player has not yet recovered from.</summary>
/// <param name="Date">The date of the match in which it happened.</param>
/// <param name="ExpectedReturn">
/// The staff's estimate of when the player recovers; the actual recovery time is hidden.
/// </param>
public sealed record InjurySnapshot(
    InjuryType Type,
    BodyPart BodyPart,
    bool CanPlayThrough,
    DateOnly Date,
    ExpectedReturn ExpectedReturn)
{
    internal static InjurySnapshot Create(Injury injury) =>
        new(
            injury.Type,
            injury.Definition.BodyPart,
            injury.Definition.CanPlayThrough,
            injury.Date,
            injury.ExpectedReturn);
}

public sealed class LineupSnapshot
{
    private readonly ReadOnlyCollection<ForwardLineSnapshot> _forwardLines;
    private readonly ReadOnlyCollection<DefencePairSnapshot> _defencePairs;
    private readonly ReadOnlyCollection<PlayerId> _dressedPlayerIds;
    private readonly ReadOnlyCollection<SpecialSituationUnitSnapshot> _specialSituationUnits;
    private readonly ReadOnlyCollection<PlayerId> _extraAttackerIds;

    private LineupSnapshot(
        IReadOnlyList<ForwardLineSnapshot> forwardLines,
        IReadOnlyList<DefencePairSnapshot> defencePairs,
        PlayerId startingGoalieId,
        PlayerId backupGoalieId,
        IReadOnlyList<PlayerId> dressedPlayerIds,
        IReadOnlyList<SpecialSituationUnitSnapshot> specialSituationUnits,
        IReadOnlyList<PlayerId> extraAttackerIds)
    {
        _forwardLines = new ReadOnlyCollection<ForwardLineSnapshot>(forwardLines.ToList());
        _defencePairs = new ReadOnlyCollection<DefencePairSnapshot>(defencePairs.ToList());
        StartingGoalieId = startingGoalieId;
        BackupGoalieId = backupGoalieId;
        _dressedPlayerIds = new ReadOnlyCollection<PlayerId>(dressedPlayerIds.ToList());
        _specialSituationUnits = new ReadOnlyCollection<SpecialSituationUnitSnapshot>(specialSituationUnits.ToList());
        _extraAttackerIds = new ReadOnlyCollection<PlayerId>(extraAttackerIds.ToList());
    }

    public IReadOnlyList<ForwardLineSnapshot> ForwardLines => _forwardLines;

    public IReadOnlyList<DefencePairSnapshot> DefencePairs => _defencePairs;

    public PlayerId StartingGoalieId { get; }

    public PlayerId BackupGoalieId { get; }

    public IReadOnlyList<PlayerId> DressedPlayerIds => _dressedPlayerIds;

    /// <summary>
    /// Gets every special-situation unit, grouped in <see cref="SpecialSituation"/> order and
    /// ordered first unit first within each situation.
    /// </summary>
    public IReadOnlyList<SpecialSituationUnitSnapshot> SpecialSituationUnits => _specialSituationUnits;

    /// <summary>
    /// Gets the two extra attackers, first choice first.
    /// </summary>
    public IReadOnlyList<PlayerId> ExtraAttackerIds => _extraAttackerIds;

    public IReadOnlyList<SpecialSituationUnitSnapshot> UnitsFor(SpecialSituation situation) =>
        _specialSituationUnits.Where(unit => unit.Situation == situation).ToList().AsReadOnly();

    internal static LineupSnapshot Create(Lineup lineup) =>
        new(
            lineup.ForwardLines.Select(ForwardLineSnapshot.Create).ToList(),
            lineup.DefencePairs.Select(DefencePairSnapshot.Create).ToList(),
            lineup.StartingGoalie.Id,
            lineup.BackupGoalie.Id,
            lineup.DressedPlayers.Select(player => player.Id).ToList(),
            lineup.SpecialSituationUnits.Select(SpecialSituationUnitSnapshot.Create).ToList(),
            lineup.ExtraAttackers.Select(player => player.Id).ToList());
}

public sealed class SpecialSituationUnitSnapshot
{
    private readonly ReadOnlyCollection<PlayerId> _playerIds;

    private SpecialSituationUnitSnapshot(SpecialSituation situation, IReadOnlyList<PlayerId> playerIds)
    {
        Situation = situation;
        _playerIds = new ReadOnlyCollection<PlayerId>(playerIds.ToList());
    }

    public SpecialSituation Situation { get; }

    /// <summary>
    /// Gets the skaters in slot order, matching the roles of
    /// <see cref="SpecialSituationFormat.For(SpecialSituation)"/>.
    /// </summary>
    public IReadOnlyList<PlayerId> PlayerIds => _playerIds;

    internal static SpecialSituationUnitSnapshot Create(SpecialSituationUnit unit) =>
        new(unit.Situation, unit.Players.Select(player => player.Id).ToList());
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