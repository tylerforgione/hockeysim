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
    SavedBiography Biography,
    int Number,
    IReadOnlyDictionary<Rating, int> Ratings);

/// <param name="Handedness">The hand a skater shoots or a goalie catches with.</param>
public sealed record SavedBiography(
    DateOnly BirthDate,
    SavedBirthplace Birthplace,
    Country Nationality,
    Handedness Handedness,
    int HeightInches,
    int WeightPounds);

/// <param name="Region">The state or province, present only where the country uses them.</param>
public sealed record SavedBirthplace(string City, string? Region, Country Country);

/// <param name="SpecialSituationUnits">Every unit, grouped by situation and first unit first.</param>
/// <param name="ExtraAttackerIds">The extra attackers, first choice first.</param>
public sealed record SavedLineup(
    IReadOnlyList<SavedForwardLine> ForwardLines,
    IReadOnlyList<SavedDefencePair> DefencePairs,
    PlayerId StartingGoalieId,
    PlayerId BackupGoalieId,
    IReadOnlyList<SavedSpecialSituationUnit> SpecialSituationUnits,
    IReadOnlyList<PlayerId> ExtraAttackerIds);

public sealed record SavedForwardLine(PlayerId LeftWingId, PlayerId CentreId, PlayerId RightWingId);

public sealed record SavedDefencePair(PlayerId LeftDefenceId, PlayerId RightDefenceId);

/// <param name="PlayerIds">The skaters in the slot order of the situation's format.</param>
public sealed record SavedSpecialSituationUnit(SpecialSituation Situation, IReadOnlyList<PlayerId> PlayerIds);

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
    int PowerPlayOpportunities,
    IReadOnlyList<SavedSkaterBoxScore> Skaters,
    SavedGoalieBoxScore Goalie);

/// <param name="TimeOnIceSeconds">Time on ice in whole seconds.</param>
public sealed record SavedSkaterBoxScore(
    PlayerId PlayerId,
    int Goals,
    int Assists,
    int PlusMinus,
    int TimeOnIceSeconds,
    int Shots,
    int ShotAttempts,
    int Hits,
    int BlockedShots,
    int FaceoffsWon,
    int FaceoffsLost,
    int Takeaways,
    int Giveaways,
    double ExpectedGoals,
    int PenaltyMinutes,
    int PowerPlayGoals,
    int PowerPlayAssists,
    int ShorthandedGoals,
    int ShorthandedAssists);

/// <param name="TimeOnIceSeconds">Time in net in whole seconds.</param>
public sealed record SavedGoalieBoxScore(
    PlayerId PlayerId,
    int ShotsAgainst,
    int GoalsAgainst,
    double ExpectedGoalsAgainst,
    int TimeOnIceSeconds);

public sealed record SavedInboxMessage(
    InboxMessageId Id,
    InboxSenderRole SenderRole,
    string SenderName,
    string Subject,
    string Body,
    bool IsRead);