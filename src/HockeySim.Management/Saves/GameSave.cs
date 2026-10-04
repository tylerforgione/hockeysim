using HockeySim.Domain;
using HockeySim.Management.Inbox;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Management.Saves;

/// <summary>
/// Everything needed to resume a game exactly: the world as generated and managed, the season's
/// progress and history, the inbox, and the hidden random state. Storage implementations persist
/// this shape; they do not interpret it.
/// </summary>
/// <remarks>
/// Team records and season statistics are not saved. Loading replays the completed matches
/// through the season, so they are always rebuilt from the history and can never disagree with it.
/// Changing this shape changes the save format, so the storage's format version must change too.
/// </remarks>
/// <param name="Schedule">The scheduled matches in schedule order.</param>
/// <param name="CurrentDate">The next league day to be played.</param>
/// <param name="CompletedMatches">Every completed match, each played on a date before the current date.</param>
/// <param name="Inbox">The inbox messages, newest first.</param>
public sealed record GameSave(
    int SeasonYear,
    TeamId ManagedTeamId,
    RandomState RandomState,
    IReadOnlyList<SavedConference> Conferences,
    IReadOnlyList<SavedScheduledMatch> Schedule,
    DateOnly CurrentDate,
    IReadOnlyList<SavedCompletedMatch> CompletedMatches,
    IReadOnlyList<SavedInboxMessage> Inbox);

public sealed record SavedConference(string Name, IReadOnlyList<SavedDivision> Divisions);

public sealed record SavedDivision(string Name, IReadOnlyList<SavedTeam> Teams);

/// <param name="Roster">The players in roster order. Undressed players are the scratches.</param>
public sealed record SavedTeam(TeamId Id, string Name, IReadOnlyList<SavedPlayer> Roster, SavedLineup Lineup);

public sealed record SavedPlayer(
    PlayerId Id,
    string FirstName,
    string LastName,
    Position Position,
    int Age,
    int Number,
    IReadOnlyDictionary<Rating, int> Ratings);

public sealed record SavedLineup(
    IReadOnlyList<SavedForwardLine> ForwardLines,
    IReadOnlyList<SavedDefencePair> DefencePairs,
    PlayerId StartingGoalieId,
    PlayerId BackupGoalieId);

public sealed record SavedForwardLine(PlayerId LeftWingId, PlayerId CentreId, PlayerId RightWingId);

public sealed record SavedDefencePair(PlayerId LeftDefenceId, PlayerId RightDefenceId);

public sealed record SavedScheduledMatch(DateOnly Date, TeamId HomeTeamId, TeamId AwayTeamId);

/// <summary>
/// A completed match, identified by its date and home team, since a team plays at most once on
/// any date.
/// </summary>
public sealed record SavedCompletedMatch(
    DateOnly Date,
    MatchDecision Decision,
    SavedMatchSide Home,
    SavedMatchSide Away);

public sealed record SavedMatchSide(
    TeamId TeamId,
    int Score,
    int Shots,
    IReadOnlyList<SavedSkaterBoxScore> Skaters,
    SavedGoalieBoxScore Goalie);

public sealed record SavedSkaterBoxScore(PlayerId PlayerId, int Goals, int Assists);

public sealed record SavedGoalieBoxScore(PlayerId PlayerId, int ShotsAgainst, int GoalsAgainst);

public sealed record SavedInboxMessage(
    InboxMessageId Id,
    InboxSenderRole SenderRole,
    string SenderName,
    string Subject,
    string Body,
    bool IsRead);