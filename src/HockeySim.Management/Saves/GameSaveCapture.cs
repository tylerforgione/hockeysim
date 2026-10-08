using HockeySim.Domain;
using HockeySim.Management.Inbox;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Management.Saves;

/// <summary>
/// Copies the game world into save data that holds no references back into it.
/// </summary>
internal static class GameSaveCapture
{
    public static GameSave Create(Season season, TeamId managedTeamId, RandomState randomState, InboxMessages inbox) =>
        new(
            season.League.SeasonYear,
            managedTeamId,
            randomState,
            season.League.Conferences.Select(CaptureConference).ToList(),
            season.Schedule.Matches
                .Select(match => new SavedScheduledMatch(match.Date, match.HomeTeamId, match.AwayTeamId))
                .ToList(),
            season.CurrentDate,
            season.CompletedMatches.Select(CaptureCompletedMatch).ToList(),
            inbox.Messages.Select(CaptureInboxMessage).ToList());

    private static SavedConference CaptureConference(Conference conference) =>
        new(
            conference.Name,
            conference.Divisions
                .Select(division => new SavedDivision(division.Name, division.Teams.Select(CaptureTeam).ToList()))
                .ToList());

    private static SavedTeam CaptureTeam(Team team) =>
        new(team.Id, team.Name, team.Roster.Select(CapturePlayer).ToList(), CaptureLineup(team.Lineup));

    private static SavedPlayer CapturePlayer(Player player) =>
        new(
            player.Id,
            player.FirstName,
            player.LastName,
            player.Position,
            CaptureBiography(player.Biography),
            player.Number,
            player.Ratings.ToDictionary(rating => rating.Key, rating => rating.Value.Value));

    private static SavedBiography CaptureBiography(PlayerBiography biography) =>
        new(
            biography.BirthDate,
            new SavedBirthplace(biography.Birthplace.City, biography.Birthplace.Region, biography.Birthplace.Country),
            biography.Nationality,
            biography.Handedness,
            biography.Height.Inches,
            biography.Weight.Pounds);

    private static SavedLineup CaptureLineup(Lineup lineup) =>
        new(
            lineup.ForwardLines
                .Select(line => new SavedForwardLine(line.LeftWing.Id, line.Centre.Id, line.RightWing.Id))
                .ToList(),
            lineup.DefencePairs
                .Select(pair => new SavedDefencePair(pair.LeftDefence.Id, pair.RightDefence.Id))
                .ToList(),
            lineup.StartingGoalie.Id,
            lineup.BackupGoalie.Id,
            lineup.SpecialSituationUnits
                .Select(unit => new SavedSpecialSituationUnit(
                    unit.Situation,
                    unit.Players.Select(player => player.Id).ToList()))
                .ToList(),
            lineup.ExtraAttackers.Select(player => player.Id).ToList());

    private static SavedCompletedMatch CaptureCompletedMatch(CompletedMatch match) =>
        new(match.Date, match.Decision, CaptureSide(match.Home), CaptureSide(match.Away));

    private static SavedMatchSide CaptureSide(CompletedMatchTeam side) =>
        new(
            side.TeamId,
            side.Score,
            side.Shots,
            side.PowerPlayOpportunities,
            side.Skaters.Select(CaptureSkater).ToList(),
            new SavedGoalieBoxScore(
                side.Goalie.PlayerId,
                side.Goalie.ShotsAgainst,
                side.Goalie.GoalsAgainst,
                side.Goalie.ExpectedGoalsAgainst,
                WholeSeconds(side.Goalie.TimeOnIce)));

    private static SavedSkaterBoxScore CaptureSkater(SkaterBoxScore skater) =>
        new(
            skater.PlayerId,
            skater.Goals,
            skater.Assists,
            skater.PlusMinus,
            WholeSeconds(skater.TimeOnIce),
            skater.Shots,
            skater.ShotAttempts,
            skater.Hits,
            skater.BlockedShots,
            skater.FaceoffsWon,
            skater.FaceoffsLost,
            skater.Takeaways,
            skater.Giveaways,
            skater.ExpectedGoals,
            skater.PenaltyMinutes,
            skater.PowerPlayGoals,
            skater.PowerPlayAssists,
            skater.ShorthandedGoals,
            skater.ShorthandedAssists,
            skater.EmptyNetGoals);

    // Box-score times are whole seconds, so this is exact.
    private static int WholeSeconds(TimeSpan time) => (int)(time.Ticks / TimeSpan.TicksPerSecond);

    private static SavedInboxMessage CaptureInboxMessage(InboxMessage message) =>
        new(message.Id, message.SenderRole, message.SenderName, message.Subject, message.Body, message.IsRead);
}