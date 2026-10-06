using System.Globalization;

using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Players;

/// <summary>
/// Read-only profile of one player: identity, age, position, lineup role, overall rating,
/// current-season totals, and the ratings that matter to their position.
/// </summary>
/// <remarks>
/// A skater's goaltending ratings and a goalie's skater ratings are generated low and play no
/// part in their game, so the profile leaves them out. Durability is hidden and never reaches
/// Desktop.
/// </remarks>
public sealed class PlayerDetailViewModel
{
    private static readonly (string Name, Rating[] Ratings)[] SkaterGroups =
    [
        ("OFFENCE", [Rating.Skating, Rating.PuckControl, Rating.Passing, Rating.ShotPower, Rating.ShotAccuracy, Rating.OffensiveAwareness]),
        ("DEFENCE", [Rating.DefensiveAwareness, Rating.Checking, Rating.ShotBlocking, Rating.StickChecking]),
        ("GAME", [Rating.Faceoffs, Rating.Stamina, Rating.Toughness, Rating.Discipline]),
    ];

    private static readonly (string Name, Rating[] Ratings)[] GoalieGroups =
    [
        ("GOALTENDING", [Rating.GoalieReflex, Rating.GoaliePositioning, Rating.GoalieReboundControl]),
        ("GAME", [Rating.Stamina, Rating.Discipline]),
    ];

    public PlayerDetailViewModel(PlayerSnapshot player, TeamSnapshot team, PlayerSeasonTotals seasonTotals, int seasonYear)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(seasonTotals);

        Id = player.Id;
        FullName = PlayerDisplay.FullName(player);
        Number = $"#{player.Number}";
        Position = PlayerDisplay.PositionName(player.Position);
        Age = player.Age;
        TeamName = team.Name;
        LineupRole = PlayerDisplay.LineupRole(player.Id, team.Lineup);
        Overall = player.Overall;

        var isGoalie = player.Position == Domain.Position.Goalie;
        SeasonTitle = $"{PlayerDisplay.FormatSeason(seasonYear)} REGULAR SEASON";
        SeasonStatistics = isGoalie
            ?
            [
                new("GP", seasonTotals.GamesPlayed.ToString(CultureInfo.CurrentCulture), "Games played"),
                new("SA", seasonTotals.ShotsAgainst.ToString(CultureInfo.CurrentCulture), "Shots against"),
                new("SV", seasonTotals.Saves.ToString(CultureInfo.CurrentCulture), "Saves"),
                new("GA", seasonTotals.GoalsAgainst.ToString(CultureInfo.CurrentCulture), "Goals against"),
                new("SV%", seasonTotals.SavePercentage, "Save percentage"),
            ]
            :
            [
                new("GP", seasonTotals.GamesPlayed.ToString(CultureInfo.CurrentCulture), "Games played"),
                new("G", seasonTotals.Goals.ToString(CultureInfo.CurrentCulture), "Goals"),
                new("A", seasonTotals.Assists.ToString(CultureInfo.CurrentCulture), "Assists"),
                new("P", seasonTotals.Points.ToString(CultureInfo.CurrentCulture), "Points"),
            ];

        // Only the starting goalie appears in a match, so a goalie's games are starts.
        SeasonCaption = seasonTotals.GamesPlayed > 0
            ? string.Empty
            : isGoalie ? "No starts yet this season." : "No appearances yet this season.";

        var groups = isGoalie ? GoalieGroups : SkaterGroups;
        RatingGroups = groups
            .Select(group => new RatingGroupViewModel(
                group.Name,
                group.Ratings
                    .Select(rating => new RatingLineViewModel(PlayerDisplay.RatingName(rating), player.Ratings[rating]))
                    .ToList()))
            .ToList();
    }

    public PlayerId Id { get; }

    public string FullName { get; }

    public string Number { get; }

    public string Position { get; }

    public int Age { get; }

    public string TeamName { get; }

    public string LineupRole { get; }

    /// <summary>The player's overall rating for their position.</summary>
    public int Overall { get; }

    public string SeasonTitle { get; }

    /// <summary>
    /// Skater GP/G/A/P or goalie GP/SA/SV/GA/SV%. Totals are zero before a first appearance, and a
    /// save percentage is a dash until a shot has been faced.
    /// </summary>
    public IReadOnlyList<SeasonStatViewModel> SeasonStatistics { get; }

    /// <summary>Explains zero totals for a player who has not appeared; otherwise empty.</summary>
    public string SeasonCaption { get; }

    public bool HasSeasonCaption => SeasonCaption.Length > 0;

    public IReadOnlyList<RatingGroupViewModel> RatingGroups { get; }
}

public sealed record SeasonStatViewModel(string Label, string Value, string Description);

public sealed record RatingGroupViewModel(string Name, IReadOnlyList<RatingLineViewModel> Ratings);

public sealed record RatingLineViewModel(string Name, int Value);