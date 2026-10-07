using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;

using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Lineups;
using HockeySim.Management.NewGame;
using HockeySim.Management.Saves;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Infrastructure.Tests;

internal static class SaveTestGames
{
    public static GameManager StartGame(ulong seed = 12345)
    {
        var manager = new GameManager();
        manager.StartNewGame(new NewGameCommand(2026, new RandomState(seed), "Halifax Mariners"));
        return manager;
    }

    public static GameSnapshot Advance(GameManager manager, int days)
    {
        var snapshot = manager.GetSnapshot();
        for (var day = 0; day < days && !snapshot.Season.IsComplete; day++)
        {
            snapshot = manager.AdvanceDay();
        }

        return snapshot;
    }

    /// <summary>
    /// The management actions repeated after a reload: a goalie change, then more league days.
    /// </summary>
    public static GameSnapshot ContinueWithLineupChange(GameManager manager)
    {
        var team = manager.GetSnapshot().League.Teams.Single(team => team.Id == manager.GetSnapshot().ManagedTeamId);
        var lineup = team.Lineup;
        manager.SetLineup(SetLineupCommand.From(lineup) with
        {
            StartingGoalieId = lineup.BackupGoalieId,
            BackupGoalieId = lineup.StartingGoalieId,
        });
        return Advance(manager, 6);
    }

    /// <summary>
    /// Describes the game as the user sees it, so equal descriptions mean the same game: rosters
    /// and lineups, every result and box score, totals, standings, inbox, and random state.
    /// </summary>
    public static IReadOnlyList<string> Describe(GameSnapshot snapshot)
    {
        var lines = new List<string>
        {
            $"date {snapshot.Season.CurrentDate:yyyy-MM-dd}, complete {snapshot.Season.IsComplete}, "
            + $"managed {snapshot.ManagedTeamId}, {snapshot.RandomState}",
        };

        foreach (var team in snapshot.League.Teams)
        {
            lines.Add($"team {team.Id} {team.Name} lineup {string.Join(",", team.Lineup.DressedPlayerIds)} "
                + $"scratches {string.Join(",", team.ScratchedPlayerIds)} "
                + $"units {string.Join(";", team.Lineup.SpecialSituationUnits.Select(unit => $"{unit.Situation}:{string.Join(",", unit.PlayerIds)}"))} "
                + $"extra attackers {string.Join(",", team.Lineup.ExtraAttackerIds)}");
            lines.AddRange(team.Roster.Select(player =>
                $"  {player.Id} {player.FirstName} {player.LastName} {player.Position} {player.Age} {player.Biography} #{player.Number} "
                + string.Join(",", player.Ratings.OrderBy(rating => rating.Key).Select(rating => rating.Value))));
        }

        lines.AddRange(snapshot.Schedule.Matches.Select(match => match.ToString()));
        lines.AddRange(snapshot.Season.Results.Select(result =>
            $"{result.Date:yyyy-MM-dd} {result.Decision} {Side(result.Home)} @ {Side(result.Away)}"));
        lines.AddRange(snapshot.Season.TeamRecords.Select(record => record.ToString()));
        lines.Add(string.Join(",", snapshot.Season.Standings.League.Select(entry => $"{entry.Rank}:{entry.Record.TeamId}")));
        lines.AddRange(snapshot.Season.SkaterStatistics.Select(statistics => statistics.ToString()));
        lines.AddRange(snapshot.Season.GoalieStatistics.Select(statistics => statistics.ToString()));
        lines.AddRange(snapshot.Inbox.Select(message => message.ToString()));
        return lines;
    }

    private static string Side(CompletedMatchTeamSnapshot side) =>
        $"{side.TeamId}:{side.Score}/{side.Shots} "
        + $"[{string.Join(",", side.Skaters.Select(skater => $"{skater.PlayerId}:{skater.Goals}+{skater.Assists}"))}] "
        + $"G {side.Goalie.PlayerId}:{side.Goalie.GoalsAgainst}/{side.Goalie.ShotsAgainst}";
}

/// <summary>
/// Reads and writes save files' contents directly, to build saves the game itself would never
/// write.
/// </summary>
internal static class SaveFileContents
{
    public static JsonObject Read(string path)
    {
        using var compressed = new BrotliStream(File.OpenRead(path), CompressionMode.Decompress);
        return JsonNode.Parse(compressed)!.AsObject();
    }

    public static void Write(string path, JsonNode document) => WriteCompressed(path, document.ToJsonString());

    public static void WriteCompressed(string path, string text)
    {
        using var compressed = new BrotliStream(File.Create(path), CompressionLevel.Fastest);
        compressed.Write(Encoding.UTF8.GetBytes(text));
    }
}

/// <summary>
/// Captures a game's save data in memory, to write saves that the game would never produce.
/// </summary>
internal sealed class CapturingStore : IGameSaveStore
{
    public GameSave? Saved { get; private set; }

    public void Save(GameSave save) => Saved = save;

    public GameSave Load() => throw new NotSupportedException();
}