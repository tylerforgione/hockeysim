using HockeySim.Domain;

namespace HockeySim.Domain.Tests;

/// <summary>
/// Builds a minimal valid 32-team league with deterministic identities and default lineups.
/// </summary>
internal static class TestLeague
{
    public static League Create()
    {
        var teamNumber = 0;

        Division CreateDivision(string name) =>
            new(name, Enumerable.Range(0, Division.RequiredTeamCount).Select(_ => CreateTeam(++teamNumber)));

        return new League(2026, [
            new Conference("East", [CreateDivision("Atlantic"), CreateDivision("Metro")]),
            new Conference("West", [CreateDivision("Central"), CreateDivision("Pacific")]),
        ]);
    }

    private static Team CreateTeam(int teamNumber)
    {
        var positions = Enumerable.Repeat(Position.Centre, 5)
            .Concat(Enumerable.Repeat(Position.Wing, 8))
            .Concat(Enumerable.Repeat(Position.Defence, 7))
            .Concat(Enumerable.Repeat(Position.Goalie, 3));
        var roster = positions
            .Select((position, index) => CreatePlayer(teamNumber, position, index + 1))
            .ToList();

        var centres = roster.Where(player => player.Position == Position.Centre).ToList();
        var wings = roster.Where(player => player.Position == Position.Wing).ToList();
        var defence = roster.Where(player => player.Position == Position.Defence).ToList();
        var goalies = roster.Where(player => player.Position == Position.Goalie).ToList();
        var lineup = Lineup.CreateWithDefaultUnits(
            Enumerable.Range(0, Lineup.RequiredForwardLineCount)
                .Select(index => new ForwardLine(wings[index * 2], centres[index], wings[(index * 2) + 1])),
            Enumerable.Range(0, Lineup.RequiredDefencePairCount)
                .Select(index => new DefencePair(defence[index * 2], defence[(index * 2) + 1])),
            goalies[0],
            goalies[1]);

        return new Team(
            new TeamId(Guid.Parse($"00000000-0000-0000-{teamNumber:D4}-000000000000")),
            $"Team {teamNumber}",
            roster,
            lineup);
    }

    private static Player CreatePlayer(int teamNumber, Position position, int number) =>
        new(
            new PlayerId(Guid.Parse($"00000000-0000-0000-{teamNumber:D4}-{number:D12}")),
            "Test",
            "Player",
            position,
            24,
            number,
            Enum.GetValues<Rating>().ToDictionary(rating => rating, _ => new RatingScore(50)));
}