using HockeySim.Domain;

namespace HockeySim.Management.Lineups;

/// <summary>
/// The lineup a team dresses on the season's current date. A player who cannot play is never
/// dressed: the managed team's lineup must already leave them out, and an AI team replaces them
/// from its healthy scratches.
/// </summary>
/// <remarks>
/// An AI team's own <see cref="Team.Lineup"/> stays the lineup it prefers. Its match-day lineup is
/// worked out afresh each day from that lineup and the players' health, so the preferred lineup
/// comes back by itself as players return, and nothing extra needs saving.
/// </remarks>
internal static class MatchDayLineup
{
    /// <summary>The team's dressed players who cannot play on the current date, in lineup order.</summary>
    public static IReadOnlyList<Player> UnavailablePlayers(Team team, Season season) =>
        team.Lineup.DressedPlayers
            .Where(player => !season.HealthOf(player.Id).CanPlayOn(season.CurrentDate))
            .ToList()
            .AsReadOnly();

    /// <summary>
    /// The lineup an AI team dresses: its own lineup with each player who cannot play replaced by
    /// the scratch who best suits their place, who also takes over their unit and extra-attacker
    /// slots. Players playing through an injury stay dressed.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The team has too few players able to play, which the injury cap prevents.
    /// </exception>
    public static Lineup ForAiTeam(Team team, Season season)
    {
        var lineup = team.Lineup;
        var unavailable = UnavailablePlayers(team, season);
        if (unavailable.Count == 0)
        {
            return lineup;
        }

        var dressed = lineup.DressedPlayers.ToHashSet();
        var scratches = team.Roster
            .Where(player => !dressed.Contains(player) && season.HealthOf(player.Id).CanPlayOn(season.CurrentDate))
            .ToList();
        var replacements = new Dictionary<Player, Player>();
        foreach (var player in unavailable)
        {
            var replacement = BestReplacement(player, RoleOf(lineup, player), scratches)
                ?? throw new InvalidOperationException(
                    $"The {team.Name} have no healthy scratch to replace {player.FirstName} {player.LastName}.");
            scratches.Remove(replacement);
            replacements[player] = replacement;
        }

        Player Replace(Player player) => replacements.GetValueOrDefault(player, player);

        return new Lineup(
            lineup.ForwardLines.Select(line => new ForwardLine(
                Replace(line.LeftWing),
                Replace(line.Centre),
                Replace(line.RightWing))),
            lineup.DefencePairs.Select(pair => new DefencePair(
                Replace(pair.LeftDefence),
                Replace(pair.RightDefence))),
            Replace(lineup.StartingGoalie),
            Replace(lineup.BackupGoalie),
            lineup.SpecialSituationUnits.Select(unit => new SpecialSituationUnit(
                unit.Situation,
                unit.Players.Select(Replace))),
            lineup.ExtraAttackers.Select(Replace));
    }

    /// <summary>
    /// The scratch who best fills the role: a goalie for a goalie; for a skater, the best position
    /// fit, then the highest overall rating, then roster order.
    /// </summary>
    private static Player? BestReplacement(Player player, SkaterRole? role, List<Player> scratches)
    {
        if (role is not { } skaterRole)
        {
            return scratches
                .Where(scratch => scratch.Position == Position.Goalie)
                .OrderByDescending(scratch => scratch.Overall.Value)
                .FirstOrDefault();
        }

        return scratches
            .Where(scratch => scratch.Position != Position.Goalie)
            .OrderBy(scratch => SkaterFit.For(scratch.Position, skaterRole))
            .ThenByDescending(scratch => scratch.Overall.Value)
            .FirstOrDefault();
    }

    /// <summary>The role a dressed player fills on their line or pair; none for a goalie.</summary>
    private static SkaterRole? RoleOf(Lineup lineup, Player player)
    {
        if (lineup.DefencePairs.Any(pair => pair.Players.Contains(player)))
        {
            return SkaterRole.Defence;
        }

        if (lineup.ForwardLines.Any(line => line.Centre == player))
        {
            return SkaterRole.Centre;
        }

        return lineup.ForwardLines.Any(line => line.Players.Contains(player)) ? SkaterRole.Wing : null;
    }
}