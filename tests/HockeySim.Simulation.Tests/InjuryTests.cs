using HockeySim.Domain;
using HockeySim.Simulation.Events;
using HockeySim.Simulation.Randomness;

using Xunit;

namespace HockeySim.Simulation.Tests;

/// <summary>
/// Injuries and hidden wear from match events, checked across many seeds. The test rosters have
/// twenty skaters and three goalies, so two skaters and one goalie can be out before the injury
/// cap applies.
/// </summary>
public sealed class InjuryTests
{
    private const int SeedCount = 200;
    private static readonly DateOnly MatchDate = new(2026, 11, 1);

    [Fact]
    public void MatchesInjurePlayersOfBothKindsAndAddWear()
    {
        var match = TestMatches.EvenMatch();
        var results = TestMatches.SimulateMany(match, SeedCount);
        var injuries = results.SelectMany(result => result.Injuries).ToList();

        Assert.Contains(injuries, injury => InjuryCatalogue.For(injury.Type).CanPlayThrough);
        Assert.Contains(injuries, injury => !InjuryCatalogue.For(injury.Type).CanPlayThrough);
        Assert.All(injuries, injury => Assert.True(InjuryCatalogue.For(injury.Type).AllowsRecoveryDays(injury.RecoveryDays)));
        Assert.All(results, result => Assert.NotEmpty(result.Wear));
        Assert.All(results.SelectMany(result => result.Wear), gain => Assert.True(gain.Points > 0));
    }

    [Fact]
    public void OnlyAppearingPlayersAreInjuredOrTakeWear()
    {
        foreach (var result in TestMatches.SimulateMany(TestMatches.EvenMatch(), SeedCount))
        {
            var appearing = new[] { result.Home, result.Away }.ToDictionary(
                side => side.TeamId,
                side => side.Skaters.Select(skater => skater.PlayerId).Append(side.Goalie.PlayerId).ToHashSet());
            Assert.All(result.Injuries, injury => Assert.Contains(injury.PlayerId, appearing[injury.TeamId]));
            Assert.All(result.Wear, gain => Assert.Contains(gain.PlayerId, appearing.Values.SelectMany(ids => ids)));
            Assert.Equal(result.Wear.Count, result.Wear.Select(gain => (gain.PlayerId, gain.BodyPart)).Distinct().Count());
        }
    }

    [Fact]
    public void APlayerWhoCannotPlayThroughAnInjuryTakesNoFurtherShifts()
    {
        var checkedInjuries = 0;
        foreach (var result in TestMatches.SimulateMany(TestMatches.EvenMatch(), SeedCount))
        {
            var events = result.Events;
            for (var index = 0; index < events.Count; index++)
            {
                if (events[index] is not InjuryEvent injury || InjuryCatalogue.For(injury.Type).CanPlayThrough)
                {
                    continue;
                }

                checkedInjuries++;
                Assert.All(events.Skip(index + 1), later =>
                    Assert.DoesNotContain(injury.PlayerId, later.OnIce.HomeSkaters.Concat(later.OnIce.AwaySkaters)));
            }
        }

        Assert.True(checkedInjuries > 10, $"Injuries checked: {checkedInjuries}");
    }

    [Fact]
    public void GoaliesSufferOnlyInjuriesTheyCanPlayThrough()
    {
        var match = TestMatches.EvenMatch();
        var goalies = new[] { match.Home, match.Away }.SelectMany(team => team.Roster)
            .Where(player => player.Position == Position.Goalie)
            .Select(player => player.Id)
            .ToHashSet();

        var goalieInjuries = TestMatches.SimulateMany(match, SeedCount * 2)
            .SelectMany(result => result.Injuries)
            .Where(injury => goalies.Contains(injury.PlayerId))
            .ToList();

        Assert.All(goalieInjuries, injury => Assert.True(InjuryCatalogue.For(injury.Type).CanPlayThrough));
    }

    [Fact]
    public void ADressedSkaterWhoCannotPlayMissesTheWholeMatch()
    {
        var match = TestMatches.EvenMatch();
        var injured = match.Home.Lineup.ForwardLines[0].Centre.Id;
        var health = HealthWith((injured, InjuryType.SprainedKnee));

        foreach (var result in Simulate(match, health, 20))
        {
            Assert.DoesNotContain(result.Home.Skaters, skater => skater.PlayerId == injured);
            Assert.Equal(17, result.Home.Skaters.Count);
            Assert.All(result.Events, matchEvent => Assert.DoesNotContain(injured, matchEvent.OnIce.HomeSkaters));
            Assert.DoesNotContain(result.Injuries, injury => injury.PlayerId == injured);
        }
    }

    [Fact]
    public void TheBackupStartsWhenTheStartingGoalieCannotPlay()
    {
        var match = TestMatches.EvenMatch();
        var health = HealthWith((match.Home.Lineup.StartingGoalie.Id, InjuryType.GroinStrain));

        var result = Simulate(match, health, 1).Single();

        Assert.Equal(match.Home.Lineup.BackupGoalie.Id, result.Home.Goalie.PlayerId);
    }

    [Fact]
    public void InjuriesThatWouldBreakTheCapDoNotHappen()
    {
        // Both healthy scratches are already out, leaving the home team eighteen able skaters.
        var match = TestMatches.EvenMatch();
        var scratches = match.Home.Roster
            .Where(player => player.Position != Position.Goalie && !match.Home.Lineup.DressedPlayers.Contains(player))
            .Select(player => (player.Id, InjuryType.SprainedKnee))
            .ToArray();
        Assert.Equal(2, scratches.Length);

        var results = Simulate(match, HealthWith(scratches), SeedCount);
        var homeInjuries = results.SelectMany(result => result.Injuries).Where(injury => injury.TeamId == match.Home.Id).ToList();

        Assert.All(homeInjuries, injury => Assert.True(InjuryCatalogue.For(injury.Type).CanPlayThrough));
        Assert.NotEmpty(homeInjuries);
        Assert.Contains(results.SelectMany(result => result.Injuries), injury =>
            injury.TeamId == match.Away.Id && !InjuryCatalogue.For(injury.Type).CanPlayThrough);
    }

    [Fact]
    public void MatchesThatCannotInjurePlayersInjureNobodyAndAddNoWear()
    {
        var preseason = new MatchHealth(MatchDate, [], injuriesPossible: false);

        Assert.All(Simulate(TestMatches.EvenMatch(), preseason, 50), result =>
        {
            Assert.Empty(result.Injuries);
            Assert.Empty(result.Wear);
        });
    }

    [Fact]
    public void WornBodyPartsAreInjuredMoreOften()
    {
        var match = TestMatches.EvenMatch();
        var worn = new MatchHealth(MatchDate, match.Home.Roster.Concat(match.Away.Roster).Select(player =>
            Enum.GetValues<BodyPart>().Aggregate(PlayerHealth.Healthy(player.Id), (health, part) => health.AddWear(part, 60))));

        var fresh = Simulate(match, MatchHealth.AllHealthy, SeedCount).Sum(result => result.Injuries.Count);
        var wornOut = Simulate(match, worn, SeedCount).Sum(result => result.Injuries.Count);

        Assert.True(wornOut > fresh * 1.5, $"Injuries with no wear: {fresh}; with heavy wear: {wornOut}");
    }

    [Fact]
    public void LessDurablePlayersAreInjuredMoreOften()
    {
        Team WithDurability(string name, int durability) =>
            TestTeams.Create(name, (_, rating) => rating == Rating.Durability ? durability : TestTeams.AverageRating);

        var fragile = Simulate(TestTeams.CreateMatch(WithDurability("Fragile", 35), WithDurability("Fragile away", 35)), MatchHealth.AllHealthy, SeedCount)
            .Sum(result => result.Injuries.Count);
        var durable = Simulate(TestTeams.CreateMatch(WithDurability("Durable", 95), WithDurability("Durable away", 95)), MatchHealth.AllHealthy, SeedCount)
            .Sum(result => result.Injuries.Count);

        Assert.True(fragile > durable * 2, $"Fragile: {fragile}; durable: {durable}");
    }

    [Fact]
    public void PlayingThroughInjuriesCostsMatches()
    {
        var match = TestMatches.EvenMatch();
        var hurt = match.Home.Roster.Select(player => (player.Id, InjuryType.SprainedAnkle))
            .Concat(match.Home.Roster.Select(player => (player.Id, InjuryType.BrokenFinger)))
            .ToArray();

        var results = Simulate(match, HealthWith(hurt), 400, injuriesPossible: false);
        var awayWinShare = results.Count(result => result.WinnerId == match.Away.Id) / (double)results.Count;

        Assert.All(results, result => Assert.Equal(18, result.Home.Skaters.Count));
        Assert.True(awayWinShare > 0.53, $"Healthy team's win share: {awayWinShare}");
    }

    [Fact]
    public void TheSameHealthAndRandomStateGiveTheSameInjuriesAndWear()
    {
        var match = TestMatches.EvenMatch();
        var health = HealthWith((match.Home.Lineup.ForwardLines[1].Centre.Id, InjuryType.BruisedRibs));
        var simulator = new MatchSimulator();

        for (ulong seed = 0; seed < 20; seed++)
        {
            var first = simulator.Simulate(match, OvertimeFormat.RegularSeason, health, new RandomState(seed));
            var second = simulator.Simulate(match, OvertimeFormat.RegularSeason, health, new RandomState(seed));

            Assert.Equal(Describe(first), Describe(second));
            Assert.Equal(first.Wear, second.Wear);
            Assert.Equal(first.RandomState, second.RandomState);
        }
    }

    private static List<(int, TimeSpan, TeamId, PlayerId, InjuryType, int)> Describe(MatchResult result) =>
        result.Injuries
            .Select(injury => (injury.Period, injury.TimeInPeriod, injury.TeamId, injury.PlayerId, injury.Type, injury.RecoveryDays))
            .ToList();

    /// <summary>Health on the match date for players hurt the day before, each with the injury's longest recovery.</summary>
    private static MatchHealth HealthWith(params (PlayerId Player, InjuryType Type)[] injuries) =>
        HealthWith(injuries, injuriesPossible: true);

    private static MatchHealth HealthWith((PlayerId Player, InjuryType Type)[] injuries, bool injuriesPossible) =>
        new(
            MatchDate,
            injuries.GroupBy(injury => injury.Player).Select(group => group.Aggregate(
                PlayerHealth.Healthy(group.Key),
                (health, injury) => health.Add(new Injury(
                    injury.Type,
                    MatchDate.AddDays(-1),
                    InjuryCatalogue.For(injury.Type).MaximumRecoveryDays)))),
            injuriesPossible);

    private static List<MatchResult> Simulate(Match match, MatchHealth health, int count, bool injuriesPossible = true)
    {
        if (!injuriesPossible)
        {
            health = new MatchHealth(health.Date, match.Home.Roster.Concat(match.Away.Roster).Select(player => health.For(player.Id)), injuriesPossible: false);
        }

        var simulator = new MatchSimulator();
        return Enumerable.Range(0, count)
            .Select(seed => simulator.Simulate(match, OvertimeFormat.RegularSeason, health, new RandomState((ulong)seed)))
            .ToList();
    }
}