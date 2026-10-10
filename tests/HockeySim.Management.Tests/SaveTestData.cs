using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Saves;

namespace HockeySim.Management.Tests;

internal static class SaveTestData
{
    /// <summary>
    /// Describes everything a snapshot shows, one line per fact, so equal descriptions mean the
    /// user sees the same game: the world, lineups, both schedules, every result, the totals and
    /// standings derived from them, the inbox, and the hidden random state.
    /// </summary>
    public static IReadOnlyList<string> Describe(GameSnapshot snapshot)
    {
        var lines = new List<string>
        {
            $"year {snapshot.League.SeasonYear}, date {snapshot.Season.CurrentDate:yyyy-MM-dd} ({snapshot.Season.Phase}), "
            + $"complete {snapshot.Season.IsComplete}, managed {snapshot.ManagedTeamId}, {snapshot.RandomState}, "
            + $"to replace {string.Join(",", snapshot.PlayersToReplace)}",
        };

        foreach (var conference in snapshot.League.Conferences)
        {
            foreach (var division in conference.Divisions)
            {
                lines.Add($"{conference.Name} / {division.Name}: {string.Join(", ", division.Teams.Select(team => team.Id))}");
            }
        }

        foreach (var team in snapshot.League.Teams)
        {
            lines.Add($"team {team.Id} {team.Name} {team.Colours}");
            lines.AddRange(team.Roster.Select(player =>
                $"  {player.Id} {player.FirstName} {player.LastName} {player.Position} age {player.Age} {player.Biography} #{player.Number} "
                + string.Join(",", player.Ratings.OrderBy(rating => rating.Key).Select(rating => $"{rating.Key}={rating.Value}"))
                + $" injuries {string.Join(";", player.Injuries)}"));
            lines.AddRange(team.Lineup.ForwardLines.Select(line => $"  line {line}"));
            lines.AddRange(team.Lineup.DefencePairs.Select(pair => $"  pair {pair}"));
            lines.Add($"  goalies {team.Lineup.StartingGoalieId} / {team.Lineup.BackupGoalieId}");
            lines.AddRange(team.Lineup.SpecialSituationUnits.Select(unit =>
                $"  {unit.Situation} {string.Join(",", unit.PlayerIds)}"));
            lines.Add($"  extra attackers {string.Join(",", team.Lineup.ExtraAttackerIds)}");
            lines.Add($"  scratches {string.Join(",", team.ScratchedPlayerIds)}");
        }

        lines.AddRange(snapshot.Schedule.PreseasonMatches.Select(match => $"preseason {match}"));
        lines.AddRange(snapshot.Schedule.Matches.Select(match => $"scheduled {match}"));
        lines.Add(SeasonAdvancementTests.Fingerprint(snapshot.Season.PreseasonResults));
        lines.Add(SeasonAdvancementTests.Fingerprint(snapshot));
        lines.AddRange(snapshot.Season.TeamRecords.Select(record => record.ToString()));
        lines.AddRange(snapshot.Season.TeamStatistics.Select(statistics => statistics.ToString()));
        lines.Add($"league {Ranking(snapshot.Season.Standings.League)}");
        foreach (var conference in snapshot.Season.Standings.Conferences)
        {
            lines.Add($"{conference.Name} {Ranking(conference.Teams)}");
            lines.AddRange(conference.Divisions.Select(division => $"{division.Name} {Ranking(division.Teams)}"));
        }

        lines.AddRange(snapshot.Season.SkaterStatistics.Select(statistics => statistics.ToString()));
        lines.AddRange(snapshot.Season.GoalieStatistics.Select(statistics => statistics.ToString()));
        lines.AddRange(DescribePlayoffs(snapshot));
        lines.AddRange(snapshot.Inbox.Select(message => message.ToString()));
        return lines;
    }

    /// <summary>The playoff schedule, bracket, results, and totals, if the playoffs have started.</summary>
    private static IEnumerable<string> DescribePlayoffs(GameSnapshot snapshot)
    {
        var playoffs = snapshot.Season.Playoffs;
        if (playoffs is null)
        {
            return ["no playoffs"];
        }

        return snapshot.Schedule.PlayoffMatches.Select(match => $"playoff {match}")
            .Append($"round {playoffs.CurrentRound}, champion {playoffs.ChampionId}")
            .Concat(playoffs.Series.Select(series =>
                $"{series.Round} {series.HigherRanked} {series.HigherRankedWins}-{series.LowerRankedWins} {series.LowerRanked} "
                + $"winner {series.WinnerId} next {series.NextGame}"))
            .Append(SeasonAdvancementTests.Fingerprint(playoffs.Results))
            .Concat(playoffs.TeamRecords.Select(record => record.ToString()))
            .Concat(playoffs.TeamStatistics.Select(statistics => statistics.ToString()))
            .Concat(playoffs.SkaterStatistics.Select(statistics => statistics.ToString()))
            .Concat(playoffs.GoalieStatistics.Select(statistics => statistics.ToString()));
    }

    private static string Ranking(IEnumerable<StandingsEntrySnapshot> entries) =>
        string.Join(", ", entries.Select(entry => $"{entry.Rank}:{entry.Record.TeamId}"));
}

/// <summary>
/// Holds a save in memory, for Management workflows that do not depend on storage. Loading
/// returns whatever it holds, even nothing, as a faulty store might.
/// </summary>
internal sealed class MemorySaveStore(GameSave? save = null) : IGameSaveStore
{
    public GameSave? Saved { get; private set; } = save;

    public void Save(GameSave save) => Saved = save;

    public GameSave Load() => Saved!;
}