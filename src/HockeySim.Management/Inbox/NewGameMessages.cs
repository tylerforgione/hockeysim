using HockeySim.Domain;

namespace HockeySim.Management.Inbox;

/// <summary>
/// Writes the messages that greet the user when a new game begins. Every statement is derived
/// from the generated team, so the messages never describe state that does not exist.
/// </summary>
internal static class NewGameMessages
{
    private const int ScoutingHighlightCount = 3;

    // Messages are delivered newest-first, so the owner's welcome is delivered last to appear on top.
    public static void Deliver(InboxMessages inbox, Team managedTeam, int seasonYear)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(managedTeam);

        DeliverCaptainMessage(inbox, managedTeam);
        DeliverScoutingReport(inbox, managedTeam);
        DeliverLineupNote(inbox, managedTeam);
        DeliverOwnerWelcome(inbox, managedTeam, seasonYear);
    }

    private static void DeliverOwnerWelcome(InboxMessages inbox, Team team, int seasonYear)
    {
        inbox.Deliver(
            InboxSenderRole.Owner,
            "Team Owner",
            $"Welcome to the {team.Name}",
            $"""
            Welcome aboard. The board has placed the {team.Name} in your hands for the {FormatSeason(seasonYear)} season.

            Your staff have prepared a roster evaluation and an opening lineup. Review them, make the changes you see fit, and build a team this city can be proud of.

            My door is always open.
            """);
    }

    private static void DeliverLineupNote(InboxMessages inbox, Team team)
    {
        var scratchedPlayers = team.Roster
            .Where(player => !team.Lineup.DressedPlayers.Contains(player))
            .Select(player => $"  • {FormatPlayer(player)}");

        inbox.Deliver(
            InboxSenderRole.AssistantGeneralManager,
            "Assistant GM",
            "Opening lineup prepared",
            $"""
            I have set a provisional lineup of four forward lines, three defence pairs, and two goalies so the team is ready to play.

            Starting goalie: {FormatPlayer(team.Lineup.StartingGoalie)}
            Backup goalie: {FormatPlayer(team.Lineup.BackupGoalie)}

            Healthy scratches:
            {string.Join("\n", scratchedPlayers)}

            You can change any assignment from the Lines screen.
            """);
    }

    private static void DeliverScoutingReport(InboxMessages inbox, Team team)
    {
        var skaters = team.Roster.Where(player => player.Position != Position.Goalie).ToList();
        var goalies = team.Roster.Where(player => player.Position == Position.Goalie).ToList();

        inbox.Deliver(
            InboxSenderRole.HeadScout,
            "Head Scout",
            "Roster evaluation",
            $"""
            Here is my first look at the players under contract.

            Best skaters:
            {DescribeLeaders(skaters, Rating.Skating)}

            Most accurate shooters:
            {DescribeLeaders(skaters, Rating.ShotAccuracy)}

            Strongest defensive minds:
            {DescribeLeaders(skaters, Rating.DefensiveAwareness)}

            Sharpest goalie reflexes:
            {DescribeLeaders(goalies, Rating.GoalieReflex)}
            """);
    }

    private static void DeliverCaptainMessage(InboxMessages inbox, Team team)
    {
        // The most experienced skater speaks for the room until captaincy becomes a managed decision.
        var veteran = team.Roster
            .Where(player => player.Position != Position.Goalie)
            .OrderByDescending(player => player.Age)
            .First();

        inbox.Deliver(
            InboxSenderRole.Captain,
            $"{veteran.FirstName} {veteran.LastName}",
            "From the dressing room",
            $"""
            Coach,

            On behalf of the guys, welcome to the {team.Name}. We are ready to work and we will follow your lead.

            Let me know if there is anything you need from the veterans.

            {veteran.FirstName} {veteran.LastName} (#{veteran.Number})
            """);
    }

    private static string DescribeLeaders(IEnumerable<Player> players, Rating rating) =>
        string.Join(
            "\n",
            players
                .OrderByDescending(player => player.GetRating(rating).Value)
                .Take(ScoutingHighlightCount)
                .Select(player => $"  • {FormatPlayer(player)}: {player.GetRating(rating).Value}"));

    private static string FormatPlayer(Player player) =>
        $"{player.FirstName} {player.LastName} (#{player.Number}, {player.Position})";

    private static string FormatSeason(int seasonYear) => $"{seasonYear}–{(seasonYear + 1) % 100:00}";
}