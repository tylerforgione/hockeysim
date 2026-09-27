using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Players;

/// <summary>
/// Formats player facts consistently across tables, lineups, and detail panels.
/// </summary>
public static class PlayerDisplay
{
    public static string FullName(PlayerSnapshot player) => $"{player.FirstName} {player.LastName}";

    public static string ShortName(PlayerSnapshot player) => $"{player.FirstName[0]}. {player.LastName}";

    public static string PositionAbbreviation(Position position) => position switch
    {
        Position.Centre => "C",
        Position.Wing => "W",
        Position.Defence => "D",
        Position.Goalie => "G",
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, "Unknown position."),
    };

    public static string PositionName(Position position) => position switch
    {
        Position.Centre => "Centre",
        Position.Wing => "Wing",
        Position.Defence => "Defence",
        Position.Goalie => "Goalie",
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, "Unknown position."),
    };

    public static string RatingName(Rating rating) => rating switch
    {
        Rating.Skating => "Skating",
        Rating.ShotPower => "Shot Power",
        Rating.ShotAccuracy => "Shot Accuracy",
        Rating.PuckControl => "Puck Control",
        Rating.Passing => "Passing",
        Rating.OffensiveAwareness => "Offensive Awareness",
        Rating.DefensiveAwareness => "Defensive Awareness",
        Rating.Checking => "Checking",
        Rating.ShotBlocking => "Shot Blocking",
        Rating.StickChecking => "Stick Checking",
        Rating.GoalieReflex => "Reflexes",
        Rating.GoaliePositioning => "Positioning",
        Rating.GoalieReboundControl => "Rebound Control",
        _ => throw new ArgumentOutOfRangeException(nameof(rating), rating, "Unknown rating."),
    };

    /// <summary>
    /// Describes where a player is dressed in a lineup, such as "F1 LW" or "D2 RD", or "Scratch".
    /// </summary>
    public static string LineupRole(PlayerId playerId, LineupSnapshot lineup)
    {
        for (var index = 0; index < lineup.ForwardLines.Count; index++)
        {
            var line = lineup.ForwardLines[index];
            var slot = playerId == line.LeftWingId ? "LW"
                : playerId == line.CentreId ? "C"
                : playerId == line.RightWingId ? "RW"
                : null;
            if (slot is not null)
            {
                return $"F{index + 1} {slot}";
            }
        }

        for (var index = 0; index < lineup.DefencePairs.Count; index++)
        {
            var pair = lineup.DefencePairs[index];
            var slot = playerId == pair.LeftDefenceId ? "LD"
                : playerId == pair.RightDefenceId ? "RD"
                : null;
            if (slot is not null)
            {
                return $"D{index + 1} {slot}";
            }
        }

        if (playerId == lineup.StartingGoalieId)
        {
            return "Starter";
        }

        return playerId == lineup.BackupGoalieId ? "Backup" : "Scratch";
    }

    public static string TeamInitials(string teamName)
    {
        var words = teamName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length switch
        {
            0 => string.Empty,
            1 => words[0][..Math.Min(3, words[0].Length)].ToUpperInvariant(),
            _ => string.Concat(words.Select(word => char.ToUpperInvariant(word[0]))),
        };
    }

    public static string FormatSeason(int seasonYear) => $"{seasonYear}–{(seasonYear + 1) % 100:00}";
}