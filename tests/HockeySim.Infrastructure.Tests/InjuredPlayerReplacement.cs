using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Lineups;

namespace HockeySim.Infrastructure.Tests;

/// <summary>
/// Plays days as a user would once injuries happen: before the managed team plays, each dressed
/// player who cannot play is swapped for a healthy scratch.
/// </summary>
internal static class InjuredPlayerReplacement
{
    public static GameSnapshot AdvanceDayReplacingInjured(this GameManager manager)
    {
        var snapshot = manager.GetSnapshot();
        if (snapshot.PlayersToReplace.Count > 0)
        {
            manager.SetLineup(ReplacingInjured(snapshot));
        }

        return manager.AdvanceDay();
    }

    /// <summary>
    /// The managed lineup with each player who cannot play swapped, everywhere they appear, for
    /// the first healthy scratch of the same kind (skater or goalie).
    /// </summary>
    public static SetLineupCommand ReplacingInjured(GameSnapshot snapshot)
    {
        var team = snapshot.League.Teams.Single(team => team.Id == snapshot.ManagedTeamId);
        var players = team.Roster.ToDictionary(player => player.Id);
        var scratches = team.ScratchedPlayerIds.Where(id => players[id].CanPlay).ToList();
        var replacements = new Dictionary<PlayerId, PlayerId>();
        foreach (var id in snapshot.PlayersToReplace)
        {
            var isGoalie = players[id].Position == Position.Goalie;
            var replacement = scratches.First(scratch => (players[scratch].Position == Position.Goalie) == isGoalie);
            scratches.Remove(replacement);
            replacements[id] = replacement;
        }

        PlayerId Replace(PlayerId id) => replacements.GetValueOrDefault(id, id);

        var command = SetLineupCommand.From(team.Lineup);
        return command with
        {
            ForwardLines = command.ForwardLines
                .Select(line => new ForwardLineSelection(Replace(line.LeftWingId), Replace(line.CentreId), Replace(line.RightWingId)))
                .ToList(),
            DefencePairs = command.DefencePairs
                .Select(pair => new DefencePairSelection(Replace(pair.LeftDefenceId), Replace(pair.RightDefenceId)))
                .ToList(),
            StartingGoalieId = Replace(command.StartingGoalieId),
            BackupGoalieId = Replace(command.BackupGoalieId),
            SpecialSituationUnits = command.SpecialSituationUnits
                .Select(unit => unit with { PlayerIds = unit.PlayerIds.Select(Replace).ToList() })
                .ToList(),
            ExtraAttackerIds = command.ExtraAttackerIds.Select(Replace).ToList(),
        };
    }
}