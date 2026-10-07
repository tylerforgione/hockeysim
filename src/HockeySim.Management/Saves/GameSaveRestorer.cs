using HockeySim.Domain;
using HockeySim.Management.Inbox;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Management.Saves;

/// <summary>
/// A game rebuilt from a save, ready to replace the active game.
/// </summary>
internal sealed record RestoredGame(Season Season, TeamId ManagedTeamId, RandomState RandomState, InboxMessages Inbox);

/// <summary>
/// Rebuilds a game from save data through the same Domain operations that built it, so a loaded
/// world is held to every invariant a played one is.
/// </summary>
/// <remarks>
/// The season is not restored from saved totals. Each saved league day is replayed through
/// <see cref="Season.CompleteDay"/>, which revalidates the results and rebuilds team records and
/// season statistics from them. Save data comes from outside the game, so any part of it may be
/// missing; every rejection becomes an <see cref="InvalidGameSaveException"/>.
/// </remarks>
internal static class GameSaveRestorer
{
    public static RestoredGame Restore(GameSave save)
    {
        ArgumentNullException.ThrowIfNull(save);

        try
        {
            var league = RestoreLeague(save);
            if (league.Teams.All(team => team.Id != save.ManagedTeamId))
            {
                throw Invalid("The managed team is not in the saved league.");
            }

            var schedule = new SeasonSchedule(Items(save.Schedule, "schedule")
                .Select(match => new ScheduledMatch(match.Date, match.HomeTeamId, match.AwayTeamId)));
            var season = new Season(league, schedule);
            ReplayLeagueDays(season, save);

            return new RestoredGame(season, save.ManagedTeamId, save.RandomState, RestoreInbox(save));
        }
        catch (ArgumentException exception)
        {
            throw Invalid($"The save does not describe a valid game. {exception.Message}", exception);
        }
    }

    private static League RestoreLeague(GameSave save) =>
        new(
            save.SeasonYear,
            Items(save.Conferences, "conferences").Select(conference => new Conference(
                conference.Name,
                Items(conference.Divisions, "divisions").Select(division => new Division(
                    division.Name,
                    Items(division.Teams, "teams").Select(RestoreTeam))))));

    private static Team RestoreTeam(SavedTeam saved)
    {
        var roster = Items(saved.Roster, "roster players").Select(RestorePlayer).ToList();
        var rosterById = new Dictionary<PlayerId, Player>();
        foreach (var player in roster)
        {
            if (!rosterById.TryAdd(player.Id, player))
            {
                throw Invalid($"Player '{player.Id}' appears more than once on the {saved.Name} roster.");
            }
        }

        Player Dressed(PlayerId id) =>
            rosterById.GetValueOrDefault(id)
            ?? throw Invalid($"Dressed player '{id}' is not on the {saved.Name} roster.");

        var lineup = Required(saved.Lineup, "lineup");
        return new Team(
            saved.Id,
            saved.Name,
            roster,
            new Lineup(
                Items(lineup.ForwardLines, "forward lines")
                    .Select(line => new ForwardLine(Dressed(line.LeftWingId), Dressed(line.CentreId), Dressed(line.RightWingId))),
                Items(lineup.DefencePairs, "defence pairs")
                    .Select(pair => new DefencePair(Dressed(pair.LeftDefenceId), Dressed(pair.RightDefenceId))),
                Dressed(lineup.StartingGoalieId),
                Dressed(lineup.BackupGoalieId),
                Items(lineup.SpecialSituationUnits, "special-situation units")
                    .Select(unit => new SpecialSituationUnit(
                        unit.Situation,
                        Values(unit.PlayerIds, "unit skaters").Select(Dressed))),
                Values(lineup.ExtraAttackerIds, "extra attackers").Select(Dressed)));
    }

    private static Player RestorePlayer(SavedPlayer saved) =>
        new(
            saved.Id,
            saved.FirstName,
            saved.LastName,
            saved.Position,
            RestoreBiography(Required(saved.Biography, "player biography")),
            saved.Number,
            Required(saved.Ratings, "player ratings").ToDictionary(rating => rating.Key, rating => new RatingScore(rating.Value)));

    private static PlayerBiography RestoreBiography(SavedBiography saved)
    {
        var birthplace = Required(saved.Birthplace, "player birthplace");
        return new PlayerBiography(
            saved.BirthDate,
            new Birthplace(birthplace.City, birthplace.Region, birthplace.Country),
            saved.Nationality,
            saved.Handedness,
            new Height(saved.HeightInches),
            new Weight(saved.WeightPounds));
    }

    /// <summary>
    /// Plays back every saved league day from opening day up to the saved current date. Each day
    /// must have a result for every match scheduled on it, and every saved result must be used.
    /// </summary>
    private static void ReplayLeagueDays(Season season, GameSave save)
    {
        var savedResults = new Dictionary<(DateOnly Date, TeamId HomeTeamId), SavedCompletedMatch>();
        foreach (var result in Items(save.CompletedMatches, "completed matches"))
        {
            if (!savedResults.TryAdd((result.Date, Required(result.Home, "home side").TeamId), result))
            {
                throw Invalid($"More than one result is saved for the home team's match on {result.Date:yyyy-MM-dd}.");
            }
        }

        if (save.CurrentDate < season.CurrentDate)
        {
            throw Invalid("The saved current date is before opening day.");
        }

        while (season.CurrentDate < save.CurrentDate)
        {
            if (season.IsComplete)
            {
                throw Invalid("The saved current date is after the end of the season.");
            }

            var day = season.CurrentDateMatches
                .Select(match => savedResults.Remove((match.Date, match.HomeTeamId), out var result)
                    ? RestoreCompletedMatch(match, result)
                    : throw Invalid($"A match on {match.Date:yyyy-MM-dd}, before the saved current date, has no result."))
                .ToList();
            season.CompleteDay(day);
        }

        if (savedResults.Count > 0)
        {
            throw Invalid("The save has results for matches that are not scheduled before its current date.");
        }
    }

    private static CompletedMatch RestoreCompletedMatch(ScheduledMatch scheduledMatch, SavedCompletedMatch saved) =>
        new(
            scheduledMatch,
            RestoreSide(Required(saved.Home, "home side")),
            RestoreSide(Required(saved.Away, "away side")),
            saved.Decision);

    private static CompletedMatchTeam RestoreSide(SavedMatchSide saved)
    {
        var goalie = Required(saved.Goalie, "goalie box score");
        return new CompletedMatchTeam(
            saved.TeamId,
            saved.Score,
            saved.Shots,
            Items(saved.Skaters, "skater box scores").Select(RestoreSkater),
            new GoalieBoxScore(
                goalie.PlayerId,
                goalie.ShotsAgainst,
                goalie.GoalsAgainst,
                goalie.ExpectedGoalsAgainst,
                TimeSpan.FromSeconds(goalie.TimeOnIceSeconds)));
    }

    private static SkaterBoxScore RestoreSkater(SavedSkaterBoxScore skater) =>
        new(
            skater.PlayerId,
            skater.Goals,
            skater.Assists,
            skater.PlusMinus,
            TimeSpan.FromSeconds(skater.TimeOnIceSeconds),
            skater.Shots,
            skater.ShotAttempts,
            skater.Hits,
            skater.BlockedShots,
            skater.FaceoffsWon,
            skater.FaceoffsLost,
            skater.Takeaways,
            skater.Giveaways,
            skater.ExpectedGoals);

    private static InboxMessages RestoreInbox(GameSave save) =>
        InboxMessages.Restore(Items(save.Inbox, "inbox messages").Select(saved =>
        {
            var message = new InboxMessage(saved.Id, saved.SenderRole, saved.SenderName, saved.Subject, saved.Body);
            if (saved.IsRead)
            {
                message.MarkRead();
            }

            return message;
        }));

    private static IReadOnlyList<T> Items<T>(IReadOnlyList<T>? items, string description)
        where T : class =>
        items is null || items.Any(item => item is null)
            ? throw Invalid($"The save's {description} are missing or incomplete.")
            : items;

    private static IReadOnlyList<T> Values<T>(IReadOnlyList<T>? values, string description)
        where T : struct =>
        values ?? throw Invalid($"The save's {description} are missing.");

    private static T Required<T>(T? value, string description)
        where T : class =>
        value ?? throw Invalid($"The save is missing a {description}.");

    private static InvalidGameSaveException Invalid(string message, Exception? innerException = null) =>
        new(message, innerException);
}