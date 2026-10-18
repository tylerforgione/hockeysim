using HockeySim.Desktop.Players;
using HockeySim.Domain;

namespace HockeySim.Desktop.Roster;

/// <summary>
/// The selected player's summary strip under the roster tables: identity, biography, injury, and
/// the ratings that matter to the player's position. Wear, durability, and exact recovery time are
/// hidden and never reach it.
/// </summary>
public sealed class PlayerStripViewModel
{
    public PlayerStripViewModel(PlayerRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var player = row.Player;
        Number = $"#{player.Number}";
        Name = row.Name;
        Position = PlayerDisplay.PositionName(player.Position);
        InjuryMarker = row.InjuryMarker;
        IsOut = row.IsOut;
        IsPlayingHurt = row.IsPlayingHurt;
        InjurySummary = row.InjurySummary;
        Facts =
        [
            new(PlayerDisplay.HandednessLabel(player.Position).ToUpperInvariant(), PlayerDisplay.HandednessName(player.Biography.Handedness)),
            new("AGE", row.Age.ToString(System.Globalization.CultureInfo.CurrentCulture)),
            new("HEIGHT", row.Height),
            new("WEIGHT", row.Weight),
            new("NATIONALITY", row.NationalityName),
        ];

        var ratings = new List<StripRatingViewModel> { new("OVR", "Overall", row.Overall) };
        ratings.AddRange(player.Position == Domain.Position.Goalie
            ? new StripRatingViewModel[]
            {
                new("REF", PlayerDisplay.RatingName(Rating.GoalieReflex), row.GoalieReflex),
                new("POS", PlayerDisplay.RatingName(Rating.GoaliePositioning), row.GoaliePositioning),
                new("REB", PlayerDisplay.RatingName(Rating.GoalieReboundControl), row.GoalieReboundControl),
            }
            :
            [
                new("SKA", PlayerDisplay.RatingName(Rating.Skating), row.Skating),
                new("SHP", PlayerDisplay.RatingName(Rating.ShotPower), row.ShotPower),
                new("SHA", PlayerDisplay.RatingName(Rating.ShotAccuracy), row.ShotAccuracy),
                new("PUC", PlayerDisplay.RatingName(Rating.PuckControl), row.PuckControl),
                new("PAS", PlayerDisplay.RatingName(Rating.Passing), row.Passing),
                new("OFF", PlayerDisplay.RatingName(Rating.OffensiveAwareness), row.OffensiveAwareness),
                new("DEF", PlayerDisplay.RatingName(Rating.DefensiveAwareness), row.DefensiveAwareness),
                new("CHK", PlayerDisplay.RatingName(Rating.Checking), row.Checking),
                new("BLK", PlayerDisplay.RatingName(Rating.ShotBlocking), row.ShotBlocking),
                new("STK", PlayerDisplay.RatingName(Rating.StickChecking), row.StickChecking),
            ]);
        Ratings = ratings;
    }

    /// <summary>The jersey number with its hash, such as #19.</summary>
    public string Number { get; }

    public string Name { get; }

    /// <summary>The natural position's name, such as Centre.</summary>
    public string Position { get; }

    /// <summary>OUT, INJ, or nothing for a healthy player.</summary>
    public string? InjuryMarker { get; }

    public bool HasInjury => InjuryMarker is not null;

    public bool IsOut { get; }

    public bool IsPlayingHurt { get; }

    /// <summary>Each injury's status, name, and expected return, one per line.</summary>
    public string InjurySummary { get; }

    /// <summary>Handedness, age, height, weight, and nationality as label and value pairs.</summary>
    public IReadOnlyList<StripFactViewModel> Facts { get; }

    /// <summary>Overall, then the player's rating set: skater or goalie ratings.</summary>
    public IReadOnlyList<StripRatingViewModel> Ratings { get; }
}

public sealed record StripFactViewModel(string Label, string Value);

/// <param name="Label">The abbreviation the roster tables use.</param>
/// <param name="Name">The full name, for the tooltip.</param>
public sealed record StripRatingViewModel(string Label, string Name, int Value);