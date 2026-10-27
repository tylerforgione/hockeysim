using System.Globalization;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Schedule;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Players;

/// <summary>
/// One match a player appeared in. The player's own figures are <see cref="Cells"/>, one for each of
/// <see cref="PlayerGameLog.SkaterColumns"/> or <see cref="PlayerGameLog.GoalieColumns"/>.
/// </summary>
/// <param name="TeamId">The player's team, whose schedule holds the match.</param>
/// <param name="PhaseLabel">"PRE", "REG", or a playoff game's round and number, such as "R1 G3".</param>
/// <param name="Opponent">"vs" or "@" and the opponent's name.</param>
/// <param name="Result">The result from the player's team's side, such as "W 4–2" or "L 2–3 OT".</param>
public sealed record PlayerGameRowViewModel(
    DateOnly Date,
    TeamId TeamId,
    string PhaseLabel,
    string PhaseDescription,
    string DateLabel,
    string Opponent,
    string Result,
    bool IsWin,
    IReadOnlyList<string> Cells)
{
    public bool IsLoss => !IsWin;
}

/// <summary>Lists the matches a player appeared in, from the box scores kept for the season.</summary>
public static class PlayerGameLog
{
    public static IReadOnlyList<string> SkaterColumns { get; } = ["G", "A", "P", "+/-", "PIM", "SOG", "ixG", "HIT", "BLK", "TOI"];

    public static IReadOnlyList<string> GoalieColumns { get; } = ["SA", "GA", "SV", "SV%", "xGA", "GSAx", "TOI"];

    /// <summary>
    /// Lists every completed match of the season in which the player appeared, in any phase, newest
    /// first. Only a team's starting goalie appears in a match.
    /// </summary>
    public static List<PlayerGameRowViewModel> For(GameSession session, PlayerSnapshot player, TeamSnapshot team)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(team);

        var season = session.Snapshot.Season;
        var rows = new List<PlayerGameRowViewModel>();
        foreach (var result in season.PreseasonResults.Concat(season.Results).Concat(season.Playoffs?.Results ?? []))
        {
            if (!TeamResults.Involves(result, team.Id))
            {
                continue;
            }

            var isHome = result.Home.TeamId == team.Id;
            var (side, opponent) = isHome ? (result.Home, result.Away) : (result.Away, result.Home);
            var cells = player.Position == Position.Goalie ? GoalieCells(side, player.Id) : SkaterCells(side, player.Id);
            if (cells is null)
            {
                continue;
            }

            var (phaseLabel, phaseDescription) = MatchDisplay.PhaseOf(result.Phase, season.Playoffs, result.Date, team.Id);
            rows.Add(new PlayerGameRowViewModel(
                result.Date,
                team.Id,
                phaseLabel,
                phaseDescription,
                MatchDisplay.ShortDate(result.Date),
                $"{(isHome ? "vs" : "@")} {session.GetTeam(opponent.TeamId).Name}",
                MatchDisplay.ResultFor(result, team.Id),
                result.WinnerId == team.Id,
                cells));
        }

        // The phases follow one another and a team plays once a day, so the order played is date order.
        rows.Reverse();
        return rows;
    }

    private static List<string>? SkaterCells(CompletedMatchTeamSnapshot side, PlayerId playerId)
    {
        var box = side.Skaters.FirstOrDefault(skater => skater.PlayerId == playerId);
        return box is null
            ? null
            :
            [
                Count(box.Goals),
                Count(box.Assists),
                Count(box.Points),
                MatchDisplay.PlusMinus(box.PlusMinus),
                Count(box.PenaltyMinutes),
                Count(box.Shots),
                MatchDisplay.ExpectedGoals(box.ExpectedGoals),
                Count(box.Hits),
                Count(box.BlockedShots),
                MatchDisplay.TimeOnIce(box.TimeOnIce),
            ];
    }

    private static List<string>? GoalieCells(CompletedMatchTeamSnapshot side, PlayerId playerId)
    {
        var box = side.Goalie;
        return box.PlayerId != playerId
            ? null
            :
            [
                Count(box.ShotsAgainst),
                Count(box.GoalsAgainst),
                Count(box.Saves),
                MatchDisplay.SavePercentage(box.Saves, box.ShotsAgainst),
                MatchDisplay.ExpectedGoals(box.ExpectedGoalsAgainst),
                MatchDisplay.GoalsSavedAboveExpected(box.ExpectedGoalsAgainst - box.GoalsAgainst),
                MatchDisplay.TimeOnIce(box.TimeOnIce),
            ];
    }

    private static string Count(int value) => value.ToString(CultureInfo.CurrentCulture);
}