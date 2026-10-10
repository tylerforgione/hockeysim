using HockeySim.Domain;

using static HockeySim.Management.Inbox.InjuryWording;

namespace HockeySim.Management.Inbox;

/// <summary>
/// Writes the head trainer's reports on the managed team's health after each league day. An injury
/// that keeps a player out gets a report of its own, with the staff's expected return, and so does
/// its recovery. Knocks a player can play through are too common for that: they and their
/// recoveries go into a weekly health report instead, delivered after each Sunday and when the
/// season ends.
/// </summary>
internal static class InjuryMessages
{
    private const string SenderName = "Head Trainer";

    /// <param name="playedDate">The day just played; the season has moved on to the next day.</param>
    public static void Deliver(InboxMessages inbox, Season season, TeamId managedTeamId, DateOnly playedDate)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(season);

        var team = season.League.Teams.Single(team => team.Id == managedTeamId);
        var rosterById = team.Roster.ToDictionary(player => player.Id);

        var match = ManagedMatchOn(season, managedTeamId, playedDate);
        if (match is not null)
        {
            var opponent = Opponent(season, match, managedTeamId);
            foreach (var injury in match.Health.Injuries.Where(injury =>
                injury.TeamId == managedTeamId && !injury.Definition.CanPlayThrough))
            {
                DeliverInjury(inbox, rosterById[injury.PlayerId], injury, opponent, playedDate);
            }
        }

        // Injuries heal on days without matches too, so returns are reported every day.
        foreach (var player in team.Roster)
        {
            var health = season.HealthOf(player.Id);
            foreach (var injury in health.Injuries.Where(injury =>
                injury.ReturnDate == season.CurrentDate && !injury.Definition.CanPlayThrough))
            {
                DeliverReturn(inbox, player, injury, health.CanPlayOn(season.CurrentDate));
            }
        }

        if (season.CurrentDate.DayOfWeek == DayOfWeek.Monday || season.IsComplete)
        {
            DeliverWeeklyReport(inbox, season, team, playedDate);
        }
    }

    private static void DeliverInjury(
        InboxMessages inbox,
        Player player,
        MatchInjury injury,
        Team opponent,
        DateOnly date)
    {
        var expected = new Injury(injury.Type, date, injury.RecoveryDays).ExpectedReturn;
        var details = string.Join(
            " ",
            new[] { AboutTheInjury(player, injury.Type, date), HowSerious(player, injury, date) }.OfType<string>());

        inbox.Deliver(
            InboxSenderRole.HeadTrainer,
            SenderName,
            $"Injury: {player.FirstName} {player.LastName}",
            $"""
            {HowItHappened(player, injury, opponent, date)} {details}

            {player.LastName} cannot play until it heals. Replace {player.LastName} in the lineup before our next match.

            Expected return: {FormatDate(expected.Earliest)} to {FormatDate(expected.Latest)}.
            """);
    }

    private static void DeliverReturn(InboxMessages inbox, Player player, Injury injury, bool canPlay) =>
        inbox.Deliver(
            InboxSenderRole.HeadTrainer,
            SenderName,
            $"Recovered: {player.FirstName} {player.LastName}",
            $"""
            {Recovered(player, injury)}

            {Availability(player, injury, canPlay)}
            """);

    /// <summary>
    /// Reports, for the week from the Monday on or before <paramref name="playedDate"/>, the
    /// players playing through knocks (with how they got any suffered this week), the players out
    /// with every injury they have, and the knocks that have healed. Nothing is sent in a week with none of these.
    /// </summary>
    private static void DeliverWeeklyReport(InboxMessages inbox, Season season, Team team, DateOnly playedDate)
    {
        var weekStart = playedDate.AddDays(-(((int)playedDate.DayOfWeek + 6) % 7));
        var today = season.CurrentDate;
        var causes = WeekInjuryCauses(season, team.Id, weekStart, playedDate);

        var playingHurt = new List<string>();
        var missing = new List<string>();
        var recovered = new List<string>();
        foreach (var player in team.Roster)
        {
            var health = season.HealthOf(player.Id);
            var section = health.CanPlayOn(today) ? playingHurt : missing;
            foreach (var injury in health.InjuriesOn(today))
            {
                var expected = injury.ExpectedReturn;
                var line = $"  • {FormatPlayer(player)}: {InjuryName(injury.Type)}"
                    + (causes.TryGetValue((player.Id, injury.Type, injury.Date), out var cause)
                        ? $" {HowSuffered(cause, player.Position)} on {FormatDate(injury.Date)}"
                        : string.Empty)
                    + $". Expected return {FormatDate(expected.Earliest)} to {FormatDate(expected.Latest)}.";
                section.Add(line);
            }

            recovered.AddRange(health.Injuries
                .Where(injury => injury.Definition.CanPlayThrough && injury.ReturnDate > weekStart && injury.ReturnDate <= today)
                .Select(injury => $"  • {FormatPlayer(player)}: {InjuryName(injury.Type)}."));
        }

        if (playingHurt.Count == 0 && missing.Count == 0 && recovered.Count == 0)
        {
            return;
        }

        var sections = new List<string> { WeeklyOpening(weekStart) };
        AddSection(sections, "Playing hurt:", playingHurt);
        AddSection(sections, "Out:", missing);
        AddSection(sections, "Recovered:", recovered);

        inbox.Deliver(
            InboxSenderRole.HeadTrainer,
            SenderName,
            $"Health report: week of {FormatDate(weekStart)}",
            string.Join("\n\n", sections));
    }

    private static void AddSection(List<string> sections, string heading, List<string> lines)
    {
        if (lines.Count > 0)
        {
            sections.Add($"{heading}\n{string.Join("\n", lines)}");
        }
    }

    /// <summary>The cause of each managed-team injury suffered in a match between the two dates.</summary>
    private static Dictionary<(PlayerId Player, InjuryType Type, DateOnly Date), InjuryCause> WeekInjuryCauses(
        Season season,
        TeamId teamId,
        DateOnly from,
        DateOnly to)
    {
        var causes = new Dictionary<(PlayerId, InjuryType, DateOnly), InjuryCause>();
        foreach (var match in MatchesThatCount(season).Where(match =>
            match.Date >= from && match.Date <= to && (match.Home.TeamId == teamId || match.Away.TeamId == teamId)))
        {
            foreach (var injury in match.Health.Injuries.Where(injury => injury.TeamId == teamId))
            {
                causes.TryAdd((injury.PlayerId, injury.Type, match.Date), injury.Cause);
            }
        }

        return causes;
    }

    /// <summary>The regular-season and playoff matches: the ones that can injure players.</summary>
    private static IEnumerable<CompletedMatch> MatchesThatCount(Season season) =>
        season.CompletedMatches.Concat(season.Playoffs?.CompletedMatches ?? []);

    private static CompletedMatch? ManagedMatchOn(Season season, TeamId managedTeamId, DateOnly date) =>
        MatchesThatCount(season).SingleOrDefault(match =>
            match.Date == date && (match.Home.TeamId == managedTeamId || match.Away.TeamId == managedTeamId));

    private static Team Opponent(Season season, CompletedMatch match, TeamId managedTeamId)
    {
        var opponentId = match.Home.TeamId == managedTeamId ? match.Away.TeamId : match.Home.TeamId;
        return season.League.Teams.Single(team => team.Id == opponentId);
    }
}