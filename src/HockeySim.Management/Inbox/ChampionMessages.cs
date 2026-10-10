using HockeySim.Domain;

namespace HockeySim.Management.Inbox;

/// <summary>
/// Announces the champion once the final is decided. The owner congratulates the user when the
/// managed team wins; otherwise the assistant GM reports the final and how the team's season ended.
/// </summary>
internal static class ChampionMessages
{
    public static void Deliver(InboxMessages inbox, Season season, TeamId managedTeamId)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(season);

        var playoffs = season.Playoffs;
        if (playoffs?.Champion is not { } champion)
        {
            return;
        }

        var final = playoffs.Series[^1];
        var runnerUpId = final.HigherRanked.TeamId == champion.TeamId ? final.LowerRanked.TeamId : final.HigherRanked.TeamId;
        var championName = TeamName(season, champion.TeamId);
        var runnerUpName = TeamName(season, runnerUpId);
        var score = $"{final.WinsOf(champion.TeamId)}–{final.WinsOf(runnerUpId)}";
        var seasonName = FormatSeason(season.League.SeasonYear);

        if (champion.TeamId == managedTeamId)
        {
            inbox.Deliver(
                InboxSenderRole.Owner,
                "Team Owner",
                $"Champions: the {championName}",
                $"""
                The {championName} are the {seasonName} champions. Beating the {runnerUpName} {score} in the final caps a season this city will not forget.

                Congratulations to you, the staff, and every player in that dressing room.
                """);
            return;
        }

        inbox.Deliver(
            InboxSenderRole.AssistantGeneralManager,
            "Assistant GM",
            $"Champions: the {championName}",
            $"""
            The {championName} beat the {runnerUpName} {score} in the final to win the {seasonName} championship.

            {HowOurSeasonEnded(season, playoffs, managedTeamId)}
            """);
    }

    private static string HowOurSeasonEnded(Season season, Playoffs playoffs, TeamId managedTeamId)
    {
        var lastSeries = playoffs.Series.LastOrDefault(series => series.Involves(managedTeamId));
        if (lastSeries is null)
        {
            return "We missed the playoffs this season.";
        }

        var opponentId = lastSeries.HigherRanked.TeamId == managedTeamId ? lastSeries.LowerRanked.TeamId : lastSeries.HigherRanked.TeamId;
        return $"Our run ended in the {RoundName(lastSeries.Round)}, "
            + $"{lastSeries.WinsOf(managedTeamId)}–{lastSeries.WinsOf(opponentId)} against the {TeamName(season, opponentId)}.";
    }

    private static string RoundName(PlayoffRound round) => round switch
    {
        PlayoffRound.FirstRound => "first round",
        PlayoffRound.SecondRound => "second round",
        PlayoffRound.ConferenceFinal => "conference final",
        PlayoffRound.Final => "final",
        _ => throw new ArgumentOutOfRangeException(nameof(round), round, "Unknown playoff round."),
    };

    private static string TeamName(Season season, TeamId teamId) =>
        season.League.Teams.Single(team => team.Id == teamId).Name;

    private static string FormatSeason(int seasonYear) => $"{seasonYear}–{(seasonYear + 1) % 100:00}";
}