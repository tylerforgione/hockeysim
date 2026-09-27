using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Players;

/// <summary>
/// Read-only profile of one player: identity, age, position, lineup role, and every rating.
/// </summary>
public sealed class PlayerDetailViewModel
{
    private static readonly (string Name, Rating[] Ratings)[] SkaterGroups =
    [
        ("OFFENCE", [Rating.Skating, Rating.PuckControl, Rating.Passing, Rating.ShotPower, Rating.ShotAccuracy, Rating.OffensiveAwareness]),
        ("DEFENCE", [Rating.DefensiveAwareness, Rating.Checking, Rating.ShotBlocking, Rating.StickChecking]),
        ("GOALTENDING", [Rating.GoalieReflex, Rating.GoaliePositioning, Rating.GoalieReboundControl]),
    ];

    private static readonly (string Name, Rating[] Ratings)[] GoalieGroups =
    [
        ("GOALTENDING", [Rating.GoalieReflex, Rating.GoaliePositioning, Rating.GoalieReboundControl]),
        ("SKATING AND PUCK", [Rating.Skating, Rating.PuckControl, Rating.Passing]),
        ("OTHER", [Rating.ShotPower, Rating.ShotAccuracy, Rating.OffensiveAwareness, Rating.DefensiveAwareness, Rating.Checking, Rating.ShotBlocking, Rating.StickChecking]),
    ];

    public PlayerDetailViewModel(PlayerSnapshot player, TeamSnapshot team)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(team);

        Id = player.Id;
        FullName = PlayerDisplay.FullName(player);
        Number = $"#{player.Number}";
        Position = PlayerDisplay.PositionName(player.Position);
        Age = player.Age;
        TeamName = team.Name;
        LineupRole = PlayerDisplay.LineupRole(player.Id, team.Lineup);

        var groups = player.Position == Domain.Position.Goalie ? GoalieGroups : SkaterGroups;
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

    public IReadOnlyList<RatingGroupViewModel> RatingGroups { get; }
}

public sealed record RatingGroupViewModel(string Name, IReadOnlyList<RatingLineViewModel> Ratings);

public sealed record RatingLineViewModel(string Name, int Value);