using System.Globalization;

using HockeySim.Domain;

namespace HockeySim.Management.Inbox;

/// <summary>
/// Writes the head trainer's reports on the managed team's health after each league day: one for
/// every injury suffered in the day's match, with the staff's expected return, and one for every
/// injury that has healed by the next day.
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

        var match = season.CompletedMatches.SingleOrDefault(match =>
            match.Date == playedDate && (match.Home.TeamId == managedTeamId || match.Away.TeamId == managedTeamId));
        if (match is not null)
        {
            var opponentId = match.Home.TeamId == managedTeamId ? match.Away.TeamId : match.Home.TeamId;
            var opponent = season.League.Teams.Single(team => team.Id == opponentId);
            foreach (var injury in match.Health.Injuries.Where(injury => injury.TeamId == managedTeamId))
            {
                DeliverInjury(inbox, rosterById[injury.PlayerId], injury, opponent, playedDate);
            }
        }

        // Injuries heal on days without matches too, so returns are reported every day.
        foreach (var player in team.Roster)
        {
            var health = season.HealthOf(player.Id);
            foreach (var injury in health.Injuries.Where(injury => injury.ReturnDate == season.CurrentDate))
            {
                DeliverReturn(inbox, player, injury, health.CanPlayOn(season.CurrentDate));
            }
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
        var availability = injury.Definition.CanPlayThrough
            ? $"{player.LastName} can play through it, at reduced ratings until it heals."
            : $"{player.LastName} cannot play until it heals. Replace {player.LastName} in the lineup before our next match.";

        inbox.Deliver(
            InboxSenderRole.HeadTrainer,
            SenderName,
            $"Injury: {player.FirstName} {player.LastName}",
            $"""
            {FormatPlayer(player)} suffered {WithArticle(injury.Type)} against the {opponent.Name} on {FormatDate(date)}.

            {availability}

            Expected return: {FormatDate(expected.Earliest)} to {FormatDate(expected.Latest)}.
            """);
    }

    private static void DeliverReturn(InboxMessages inbox, Player player, Injury injury, bool canPlay)
    {
        var availability = canPlay
            ? $"{player.LastName} is cleared to play."
            : $"{player.LastName} is still out with another injury.";

        inbox.Deliver(
            InboxSenderRole.HeadTrainer,
            SenderName,
            $"Recovered: {player.FirstName} {player.LastName}",
            $"""
            {FormatPlayer(player)} has recovered from the {InjuryName(injury.Type)}.

            {availability}
            """);
    }

    private static string InjuryName(InjuryType type) => type switch
    {
        InjuryType.Concussion => "concussion",
        InjuryType.BrokenNose => "broken nose",
        InjuryType.SeparatedShoulder => "separated shoulder",
        InjuryType.BruisedShoulder => "bruised shoulder",
        InjuryType.BrokenHand => "broken hand",
        InjuryType.BrokenFinger => "broken finger",
        InjuryType.BruisedRibs => "bruised ribs",
        InjuryType.BackSpasms => "back spasms",
        InjuryType.GroinStrain => "groin strain",
        InjuryType.TightGroin => "tight groin",
        InjuryType.SprainedKnee => "sprained knee",
        InjuryType.BruisedKnee => "bruised knee",
        InjuryType.HighAnkleSprain => "high ankle sprain",
        InjuryType.SprainedAnkle => "sprained ankle",
        InjuryType.BrokenFoot => "broken foot",
        InjuryType.BruisedFoot => "bruised foot",
        _ => throw new ArgumentOutOfRangeException(nameof(type), "The injury type is not recognised."),
    };

    // Plural injuries read without an article: "suffered bruised ribs".
    private static string WithArticle(InjuryType type) => type switch
    {
        InjuryType.BruisedRibs or InjuryType.BackSpasms => InjuryName(type),
        _ => $"a {InjuryName(type)}",
    };

    private static string FormatPlayer(Player player) =>
        $"{player.FirstName} {player.LastName} (#{player.Number}, {player.Position})";

    private static string FormatDate(DateOnly date) => date.ToString("MMMM d", CultureInfo.InvariantCulture);
}