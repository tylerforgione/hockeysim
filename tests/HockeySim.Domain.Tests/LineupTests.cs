using HockeySim.Domain;

using Xunit;

namespace HockeySim.Domain.Tests;

public sealed class LineupTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void LineupRequiresExactlyFourForwardLines(int lineCount)
    {
        var roster = CreateRoster();
        var validLineup = CreateLineup(roster);
        var forwardLines = Enumerable.Range(0, lineCount)
            .Select(index => validLineup.ForwardLines[index % validLineup.ForwardLines.Count])
            .ToList();

        Assert.Throws<ArgumentException>(() => new Lineup(
            forwardLines,
            validLineup.DefencePairs,
            validLineup.StartingGoalie,
            validLineup.BackupGoalie));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void LineupRequiresExactlyThreeDefencePairs(int pairCount)
    {
        var roster = CreateRoster();
        var validLineup = CreateLineup(roster);
        var defencePairs = Enumerable.Range(0, pairCount)
            .Select(index => validLineup.DefencePairs[index % validLineup.DefencePairs.Count])
            .ToList();

        Assert.Throws<ArgumentException>(() => new Lineup(
            validLineup.ForwardLines,
            defencePairs,
            validLineup.StartingGoalie,
            validLineup.BackupGoalie));
    }

    [Fact]
    public void DefencePairRejectsAPlayerInTheWrongPosition()
    {
        var roster = CreateRoster();
        var centre = roster.First(player => player.Position == Position.Centre);
        var defence = roster.First(player => player.Position == Position.Defence);

        Assert.Throws<ArgumentException>(() => new DefencePair(centre, defence));
    }

    [Fact]
    public void LineupRejectsASkaterInAGoalieRole()
    {
        var roster = CreateRoster();
        var validLineup = CreateLineup(roster);
        var centre = roster.Last(player => player.Position == Position.Centre);

        Assert.Throws<ArgumentException>(() => new Lineup(
            validLineup.ForwardLines,
            validLineup.DefencePairs,
            centre,
            validLineup.BackupGoalie));
    }

    [Fact]
    public void LineupRejectsAPlayerAssignedMoreThanOnce()
    {
        var roster = CreateRoster();
        var centres = roster.Where(player => player.Position == Position.Centre).ToList();
        var wings = roster.Where(player => player.Position == Position.Wing).ToList();
        var validLineup = CreateLineup(roster);
        var forwardLines = new List<ForwardLine>
        {
            new(wings[0], centres[0], wings[1]),
            new(wings[0], centres[1], wings[3]),
            new(wings[4], centres[2], wings[5]),
            new(wings[6], centres[3], wings[7]),
        };

        Assert.Throws<ArgumentException>(() => new Lineup(
            forwardLines,
            validLineup.DefencePairs,
            validLineup.StartingGoalie,
            validLineup.BackupGoalie));
    }

    [Fact]
    public void TeamRejectsALineupContainingAPlayerOutsideItsRoster()
    {
        var roster = CreateRoster();
        var validLineup = CreateLineup(roster);
        var foreignWing = CreatePlayer(Position.Wing, 24);
        var forwardLines = validLineup.ForwardLines.ToList();
        forwardLines[0] = new ForwardLine(
            foreignWing,
            forwardLines[0].Centre,
            forwardLines[0].RightWing);
        var foreignLineup = new Lineup(
            forwardLines,
            validLineup.DefencePairs,
            validLineup.StartingGoalie,
            validLineup.BackupGoalie);

        Assert.Throws<ArgumentException>(() => new Team(
            new TeamId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            "Test Team",
            roster,
            foreignLineup));
    }

    private static IReadOnlyList<Player> CreateRoster()
    {
        var positions = Enumerable.Repeat(Position.Centre, 5)
            .Concat(Enumerable.Repeat(Position.Wing, 8))
            .Concat(Enumerable.Repeat(Position.Defence, 7))
            .Concat(Enumerable.Repeat(Position.Goalie, 3));

        return positions.Select((position, index) => CreatePlayer(position, index + 1)).ToList();
    }

    private static Lineup CreateLineup(IReadOnlyList<Player> roster)
    {
        var centres = roster.Where(player => player.Position == Position.Centre).ToList();
        var wings = roster.Where(player => player.Position == Position.Wing).ToList();
        var defence = roster.Where(player => player.Position == Position.Defence).ToList();
        var goalies = roster.Where(player => player.Position == Position.Goalie).ToList();
        var forwardLines = Enumerable.Range(0, Lineup.RequiredForwardLineCount)
            .Select(index => new ForwardLine(wings[index * 2], centres[index], wings[(index * 2) + 1]))
            .ToList();
        var defencePairs = Enumerable.Range(0, Lineup.RequiredDefencePairCount)
            .Select(index => new DefencePair(defence[index * 2], defence[(index * 2) + 1]))
            .ToList();

        return new Lineup(forwardLines, defencePairs, goalies[0], goalies[1]);
    }

    private static Player CreatePlayer(Position position, int number) =>
        new(
            new PlayerId(Guid.Parse($"00000000-0000-0000-0000-{number:D12}")),
            "Test",
            "Player",
            position,
            24,
            number,
            Enum.GetValues<Rating>().ToDictionary(rating => rating, _ => new RatingScore(50)));
}