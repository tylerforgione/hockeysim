using HockeySim.Domain;
using HockeySim.Simulation.Events;
using HockeySim.Simulation.Play;
using HockeySim.Simulation.Randomness;

using Xunit;

namespace HockeySim.Simulation.Tests;

/// <summary>
/// Checks that skaters out of position or on their off-hand side play below their ratings. The
/// statistical checks compare teams of identical ratings that differ only in where their skaters
/// play, with injuries off so nobody leaves the lineup being compared. The extra attacker is on the
/// ice too briefly for that, so its strengths are checked directly.
/// </summary>
public sealed class LineupFitTests
{
    private const int MatchCount = 400;

    [Fact]
    public void ForwardsOnDefenceAndDefenceAtForwardLoseMoreOften()
    {
        var natural = TestTeams.Create("Natural");
        var swapped = WithForwardsAndDefenceSwapped(TestTeams.Create("Swapped"));

        var naturalWinShare = WinShare(natural, swapped);

        Assert.True(naturalWinShare > 0.53, $"Natural win share: {naturalWinShare}");
    }

    [Fact]
    public void SwappingCentresAndWingsCostsLessThanSwappingForwardsAndDefence()
    {
        var centresAndWings = WithCentresAndWingsSwapped(TestTeams.Create("Centres and wings swapped"));
        var forwardsAndDefence = WithForwardsAndDefenceSwapped(TestTeams.Create("Forwards and defence swapped"));

        var winShare = WinShare(centresAndWings, forwardsAndDefence);

        Assert.True(winShare > 0.53, $"Centres and wings swapped win share: {winShare}");
    }

    [Fact]
    public void WingsPlayingCentreLoseMoreFaceoffs()
    {
        var natural = TestTeams.Create("Natural");
        var wingsAtCentre = WithCentresAndWingsSwapped(TestTeams.Create("Wings at centre"));

        var faceoffs = SimulateMany(natural, wingsAtCentre).SelectMany(result => result.Events.OfType<FaceoffEvent>()).ToList();
        var naturalShare = faceoffs.Count(faceoff => faceoff.WinnerTeamId == natural.Id) / (double)faceoffs.Count;

        Assert.True(naturalShare > 0.52, $"Natural faceoff share: {naturalShare}");
    }

    [Fact]
    public void OffHandWingsAndDefenceAreOutshot()
    {
        var onSide = TestTeams.CreateWithHandedSides("On side", offHand: false);
        var offHand = TestTeams.CreateWithHandedSides("Off hand", offHand: true);

        var results = SimulateMany(onSide, offHand).Concat(SimulateMany(offHand, onSide)).ToList();
        var attempts = results.SelectMany(result => result.Events)
            .Select(matchEvent => matchEvent switch
            {
                ShotAttemptEvent attempt => attempt.TeamId,
                GoalEvent goal => goal.TeamId,
                _ => (TeamId?)null,
            })
            .OfType<TeamId>()
            .ToList();
        var onSideShare = attempts.Count(team => team == onSide.Id) / (double)attempts.Count;

        // The off-hand effects are deliberately small, and one-timers partly offset them.
        Assert.True(onSideShare > 0.502, $"On-side share of attempts: {onSideShare}");
    }

    [Theory]
    [InlineData(Position.Centre)]
    [InlineData(Position.Wing)]
    [InlineData(Position.Defence)]
    public void NoSkaterIsOutOfPositionAsAnExtraAttacker(Position position)
    {
        var player = TestTeams.Create("Team").Roster.First(player => player.Position == position);
        var skater = new SkaterState(player, new Dictionary<Rating, int>());

        var extraAttacker = OnIceSkater.ExtraAttacker(skater);
        var wing = new OnIceSkater(skater, SkaterRole.Wing, side: null);

        Assert.Equal(skater.Offence, extraAttacker.Offence);
        Assert.Equal(skater.Defence, extraAttacker.Defence);
        Assert.Equal(skater.Faceoffs, extraAttacker.Faceoffs);
        Assert.Equal(skater.Finishing, extraAttacker.Finishing);
        Assert.Equal(skater.PuckProtection, extraAttacker.PuckProtection);
        // The same skater in an ordinary wing slot is penalised unless they are a wing.
        Assert.Equal(position != Position.Wing, wing.Offence < skater.Offence);
    }

    /// <summary>
    /// The top three lines' wings and the three pairs trade places, so most skaters on the ice play
    /// the other side of forward and defence.
    /// </summary>
    private static Team WithForwardsAndDefenceSwapped(Team team) =>
        TestTeams.WithLines(team, (lines, pairs) => (
            lines.Select((line, index) => index < pairs.Count
                ? new ForwardLine(pairs[index].LeftDefence, line.Centre, pairs[index].RightDefence)
                : line),
            pairs.Select((_, index) => new DefencePair(lines[index].LeftWing, lines[index].RightWing))));

    /// <summary>Each line's centre and left wing trade places.</summary>
    private static Team WithCentresAndWingsSwapped(Team team) =>
        TestTeams.WithLines(team, (lines, pairs) => (
            lines.Select(line => new ForwardLine(line.Centre, line.LeftWing, line.RightWing)),
            pairs));

    /// <summary>Plays half the matches with each team at home.</summary>
    private static double WinShare(Team team, Team opponent)
    {
        var wins = SimulateMany(team, opponent, MatchCount / 2).Count(result => result.WinnerId == team.Id)
            + SimulateMany(opponent, team, MatchCount / 2).Count(result => result.WinnerId == team.Id);
        return wins / (double)MatchCount;
    }

    private static List<MatchResult> SimulateMany(Team home, Team away, int count = MatchCount)
    {
        var match = TestTeams.CreateMatch(home, away);
        var simulator = new MatchSimulator();
        return Enumerable.Range(0, count)
            .Select(seed => simulator.Simulate(match, OvertimeFormat.RegularSeason, TestMatches.WithoutInjuries, new RandomState((ulong)seed)))
            .ToList();
    }
}