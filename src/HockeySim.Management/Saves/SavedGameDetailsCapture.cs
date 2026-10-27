using HockeySim.Domain;

namespace HockeySim.Management.Saves;

/// <summary>
/// Works out the managed team's position in the season for a save's list entry.
/// </summary>
internal static class SavedGameDetailsCapture
{
    public static SavedGameDetails Create(Season season, TeamId managedTeamId)
    {
        var team = season.League.Teams.Single(candidate => candidate.Id == managedTeamId);
        var division = season.League.Conferences
            .SelectMany(conference => conference.Divisions)
            .Single(candidate => candidate.Teams.Any(member => member.Id == managedTeamId));
        var entry = season.RankStandings(division.Teams.Select(member => member.Id))
            .Single(standing => standing.Record.TeamId == managedTeamId);
        var record = entry.Record;
        var (progress, round) = PlayoffPosition(season.Playoffs, managedTeamId);

        return new SavedGameDetails(
            team.Name,
            team.Colours.Primary.ToString(),
            team.Colours.Secondary.ToString(),
            season.CurrentDate,
            season.Phase,
            record.Wins,
            record.RegulationLosses,
            record.OvertimeLosses + record.ShootoutLosses,
            record.Points,
            division.Name,
            entry.Rank,
            progress,
            round);
    }

    /// <summary>
    /// A team that never played a series, whether the playoffs have not started or it missed
    /// them, is not in the playoffs. Otherwise its latest series says how far it got.
    /// </summary>
    private static (SavedPlayoffProgress Progress, PlayoffRound? Round) PlayoffPosition(Playoffs? playoffs, TeamId teamId)
    {
        var latest = playoffs?.Series.LastOrDefault(series => series.Involves(teamId));
        if (playoffs is null || latest is null)
        {
            return (SavedPlayoffProgress.NotInPlayoffs, null);
        }

        if (playoffs.Champion?.TeamId == teamId)
        {
            return (SavedPlayoffProgress.Champion, latest.Round);
        }

        return latest.Winner is { } winner && winner.TeamId != teamId
            ? (SavedPlayoffProgress.Eliminated, latest.Round)
            : (SavedPlayoffProgress.Alive, latest.Round);
    }
}