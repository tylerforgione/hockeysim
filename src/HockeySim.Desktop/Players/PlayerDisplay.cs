using System.Globalization;

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
        Rating.Faceoffs => "Faceoffs",
        Rating.Discipline => "Discipline",
        Rating.Stamina => "Stamina",
        Rating.Toughness => "Toughness",
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

    /// <summary>The three-letter code hockey uses for a nation, such as CAN or SUI.</summary>
    public static string CountryCode(Country country) => country switch
    {
        Country.Canada => "CAN",
        Country.UnitedStates => "USA",
        Country.Sweden => "SWE",
        Country.Finland => "FIN",
        Country.Russia => "RUS",
        Country.Czechia => "CZE",
        Country.Switzerland => "SUI",
        Country.Germany => "GER",
        Country.Slovakia => "SVK",
        Country.Denmark => "DEN",
        Country.Latvia => "LAT",
        Country.Austria => "AUT",
        Country.Norway => "NOR",
        _ => throw new ArgumentOutOfRangeException(nameof(country), country, "Unknown country."),
    };

    public static string CountryName(Country country) => country switch
    {
        Country.Canada => "Canada",
        Country.UnitedStates => "United States",
        Country.Sweden => "Sweden",
        Country.Finland => "Finland",
        Country.Russia => "Russia",
        Country.Czechia => "Czechia",
        Country.Switzerland => "Switzerland",
        Country.Germany => "Germany",
        Country.Slovakia => "Slovakia",
        Country.Denmark => "Denmark",
        Country.Latvia => "Latvia",
        Country.Austria => "Austria",
        Country.Norway => "Norway",
        _ => throw new ArgumentOutOfRangeException(nameof(country), country, "Unknown country."),
    };

    public static string HandednessAbbreviation(Handedness handedness) => handedness switch
    {
        Handedness.Left => "L",
        Handedness.Right => "R",
        _ => throw new ArgumentOutOfRangeException(nameof(handedness), handedness, "Unknown handedness."),
    };

    public static string HandednessName(Handedness handedness) => handedness switch
    {
        Handedness.Left => "Left",
        Handedness.Right => "Right",
        _ => throw new ArgumentOutOfRangeException(nameof(handedness), handedness, "Unknown handedness."),
    };

    /// <summary>
    /// "Shoots" for a skater or "Catches" for a goalie: what <see cref="PlayerBiography.Handedness"/>
    /// describes for the position.
    /// </summary>
    public static string HandednessLabel(Position position) => position == Position.Goalie ? "Catches" : "Shoots";

    /// <summary>Height in feet and inches, such as 6' 1". A unit setting may add metric later.</summary>
    public static string FormatHeight(Height height) => $"{height.Inches / 12}' {height.Inches % 12}\"";

    /// <summary>Weight in pounds, such as 195 lb.</summary>
    public static string FormatWeight(Weight weight) => $"{weight.Pounds} lb";

    public static string FormatBirthDate(DateOnly birthDate) =>
        birthDate.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);

    /// <summary>City, then state or province where the country has them, then country.</summary>
    public static string FormatBirthplace(Birthplace birthplace) =>
        string.Join(", ", new[] { birthplace.City, birthplace.Region, CountryName(birthplace.Country) }.OfType<string>());

    public static string FormatSeason(int seasonYear) => $"{seasonYear}–{(seasonYear + 1) % 100:00}";
}