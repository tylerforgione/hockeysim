using System.Globalization;

using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Players;

/// <summary>
/// Read-only profile of one player: identity, age, position, lineup role, overall rating,
/// biographical details, injuries, the full current-season statistic line, and the ratings that
/// matter to their position.
/// </summary>
/// <remarks>
/// A skater's goaltending ratings and a goalie's skater ratings are generated low and play no
/// part in their game, so the profile leaves them out. Durability and wear are hidden and never
/// reach Desktop.
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
        Height = PlayerDisplay.FormatHeight(player.Biography.Height);
        Weight = PlayerDisplay.FormatWeight(player.Biography.Weight);
        HandednessLabel = PlayerDisplay.HandednessLabel(player.Position).ToUpperInvariant();
        Handedness = PlayerDisplay.HandednessName(player.Biography.Handedness);
        BirthDate = PlayerDisplay.FormatBirthDate(player.Biography.BirthDate);
        Birthplace = PlayerDisplay.FormatBirthplace(player.Biography.Birthplace);
        Nationality = PlayerDisplay.CountryName(player.Biography.Nationality);
        TeamName = team.Name;
        LineupRole = PlayerDisplay.LineupRole(player.Id, team.Lineup);
        Overall = player.Overall;
        Health = player.Injuries.Count == 0 ? "Healthy" : player.CanPlay ? "Playing through injury" : "Out injured";
        IsOut = !player.CanPlay;
        Injuries = player.Injuries.Select(injury => new PlayerInjuryViewModel(injury)).ToList();

        var isGoalie = player.Position == Domain.Position.Goalie;
        SeasonTitle = $"{PlayerDisplay.FormatSeason(seasonYear)} REGULAR SEASON";
        SeasonStatisticGroups = seasonTotals.ProfileGroups(isGoalie);

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

    /// <summary>Feet and inches, such as 6' 1".</summary>
    public string Height { get; }

    /// <summary>Pounds, such as 195 lb.</summary>
    public string Weight { get; }

    /// <summary>SHOOTS for a skater, CATCHES for a goalie.</summary>
    public string HandednessLabel { get; }

    public string Handedness { get; }

    public string BirthDate { get; }

    public string Birthplace { get; }

    public string Nationality { get; }

    public string TeamName { get; }

    public string LineupRole { get; }

    /// <summary>The player's overall rating for their position.</summary>
    public int Overall { get; }

    /// <summary>Healthy, playing through injury, or out injured.</summary>
    public string Health { get; }

    public bool IsOut { get; }

    public bool IsPlayingHurt => HasInjuries && !IsOut;

    /// <summary>The injuries that have not healed, in the order suffered.</summary>
    public IReadOnlyList<PlayerInjuryViewModel> Injuries { get; }

    public bool HasInjuries => Injuries.Count > 0;

    public string SeasonTitle { get; }

    /// <summary>
    /// The full current-season statistic line, grouped. Counts are zero before a first appearance,
    /// and a percentage, average, or rate is a dash until it is defined.
    /// </summary>
    public IReadOnlyList<SeasonStatGroupViewModel> SeasonStatisticGroups { get; }

    /// <summary>Explains zero totals for a player who has not appeared; otherwise empty.</summary>
    public string SeasonCaption { get; }

    public bool HasSeasonCaption => SeasonCaption.Length > 0;

    public IReadOnlyList<RatingGroupViewModel> RatingGroups { get; }
}

/// <summary>One injury in a player's profile.</summary>
public sealed class PlayerInjuryViewModel
{
    public PlayerInjuryViewModel(InjurySnapshot injury)
    {
        ArgumentNullException.ThrowIfNull(injury);

        Name = InjuryDisplay.Name(injury.Type);
        Status = InjuryDisplay.Status(injury);
        IsOut = !injury.CanPlayThrough;
        ExpectedReturn = $"Expected back {InjuryDisplay.ExpectedReturn(injury.ExpectedReturn)}";

        // The catalogue's reductions are rules of the game, not hidden state, so the user may see
        // what playing through costs.
        Effect = injury.CanPlayThrough
            ? "Plays at " + string.Join(
                ", ",
                InjuryCatalogue.For(injury.Type).RatingReductions
                    .OrderBy(reduction => reduction.Key)
                    .Select(reduction => $"−{reduction.Value} {PlayerDisplay.RatingName(reduction.Key)}"))
            : "Cannot play until healed";
    }

    public string Name { get; }

    /// <summary>Out, or playing through.</summary>
    public string Status { get; }

    public bool IsOut { get; }

    public string ExpectedReturn { get; }

    /// <summary>What the injury does: the rating points it costs, or that the player cannot play.</summary>
    public string Effect { get; }
}

public sealed record SeasonStatViewModel(string Label, string Value, string Description);

public sealed record SeasonStatGroupViewModel(string Name, IReadOnlyList<SeasonStatViewModel> Statistics);

public sealed record RatingGroupViewModel(string Name, IReadOnlyList<RatingLineViewModel> Ratings);

public sealed record RatingLineViewModel(string Name, int Value);