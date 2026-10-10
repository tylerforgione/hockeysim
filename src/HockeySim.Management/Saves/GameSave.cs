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
/// Team records, season statistics, and the playoff bracket and its dates are not saved. Loading
/// replays the completed matches through the season, so they are always rebuilt from the history
/// and can never disagree with it.
/// Changing this shape changes the save format, so the storage's format version must change too.
/// </remarks>
/// <param name="PreseasonSchedule">The scheduled preseason matches in schedule order.</param>
/// <param name="Schedule">The scheduled regular-season matches in schedule order.</param>
/// <param name="CurrentDate">The next league day to be played.</param>
/// <param name="PreseasonMatches">Every completed preseason match, each played on a date before the current date.</param>
/// <param name="CompletedMatches">Every completed regular-season match, each played on a date before the current date.</param>
/// <param name="PlayoffMatches">Every completed playoff match, each played on a date before the current date.</param>
/// <param name="Inbox">The inbox messages, newest first.</param>
public sealed record GameSave(
    int SeasonYear,
    TeamId ManagedTeamId,
    RandomState RandomState,
    IReadOnlyList<SavedConference> Conferences,
    IReadOnlyList<SavedScheduledMatch> PreseasonSchedule,
    IReadOnlyList<SavedScheduledMatch> Schedule,
    DateOnly CurrentDate,
    IReadOnlyList<SavedCompletedMatch> PreseasonMatches,
    IReadOnlyList<SavedCompletedMatch> CompletedMatches,
    IReadOnlyList<SavedCompletedMatch> PlayoffMatches,
    IReadOnlyList<SavedInboxMessage> Inbox);

public sealed record SavedConference(string Name, IReadOnlyList<SavedDivision> Divisions);

public sealed record SavedDivision(string Name, IReadOnlyList<SavedTeam> Teams);

/// <param name="PrimaryColour">Written as #RRGGBB, as is <paramref name="SecondaryColour"/>.</param>
/// <param name="Roster">The players in roster order. Undressed players are the scratches.</param>
public sealed record SavedTeam(
    TeamId Id,
    string Name,
    string PrimaryColour,
    string SecondaryColour,
    IReadOnlyList<SavedPlayer> Roster,
    SavedLineup Lineup);

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
/// <param name="Goals">The scoring summary, in the order scored.</param>
/// <param name="Penalties">The penalty summary, in the order called.</param>
/// <param name="Injuries">The injuries suffered, in the order they happened.</param>
/// <param name="Wear">The hidden wear players' body parts took; loading rebuilds health from it.</param>
public sealed record SavedCompletedMatch(
    DateOnly Date,
    MatchDecision Decision,
    SavedMatchSide Home,
    SavedMatchSide Away,
    IReadOnlyList<SavedGoal> Goals,
    IReadOnlyList<SavedPenalty> Penalties,
    IReadOnlyList<SavedInjury> Injuries,
    IReadOnlyList<SavedWearGain> Wear);

/// <param name="TimeInPeriodSeconds">Elapsed time in the period in whole seconds.</param>
public sealed record SavedGoal(
    int Period,
    int TimeInPeriodSeconds,
    TeamId TeamId,
    PlayerId ScorerId,
    PlayerId? PrimaryAssistId,
    PlayerId? SecondaryAssistId,
    GoalSituation Situation,
    bool IsEmptyNet);

/// <param name="TimeInPeriodSeconds">Elapsed time in the period in whole seconds.</param>
public sealed record SavedPenalty(
    int Period,
    int TimeInPeriodSeconds,
    TeamId TeamId,
    PlayerId PlayerId,
    Infraction Infraction,
    PenaltyKind Kind);

/// <param name="TimeInPeriodSeconds">Elapsed time in the period in whole seconds.</param>
public sealed record SavedInjury(
    int Period,
    int TimeInPeriodSeconds,
    TeamId TeamId,
    PlayerId PlayerId,
    InjuryType Type,
    InjuryCause Cause,
    int RecoveryDays);

public sealed record SavedWearGain(PlayerId PlayerId, BodyPart BodyPart, int Points);

/// <param name="ShotTotals">Both teams' shot totals by strength situation, from this side.</param>
public sealed record SavedMatchSide(
    TeamId TeamId,
    int Score,
    int Shots,
    int PowerPlayOpportunities,
    IReadOnlyList<SavedSkaterBoxScore> Skaters,
    SavedGoalieBoxScore Goalie,
    SavedSituationalShotTotals ShotTotals);

public sealed record SavedSituationalShotTotals(
    SavedShotTotals FiveOnFive,
    SavedShotTotals PowerPlay,
    SavedShotTotals PenaltyKill,
    SavedShotTotals Other);

public sealed record SavedShotTotals(
    int AttemptsFor,
    int AttemptsAgainst,
    int UnblockedAttemptsFor,
    int UnblockedAttemptsAgainst,
    int ShotsFor,
    int ShotsAgainst,
    int GoalsFor,
    int GoalsAgainst,
    double ExpectedGoalsFor,
    double ExpectedGoalsAgainst);

/// <param name="TimeOnIceSeconds">Time on ice in whole seconds.</param>
/// <param name="OnIce">Both teams' shot totals while the skater was on the ice.</param>
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
    int ShorthandedAssists,
    int EmptyNetGoals,
    SavedSituationalShotTotals OnIce);

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