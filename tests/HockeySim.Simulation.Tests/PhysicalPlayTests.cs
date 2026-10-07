using HockeySim.Domain;
using HockeySim.Simulation.Events;

using Xunit;

namespace HockeySim.Simulation.Tests;

public sealed class PhysicalPlayTests
{
    private const int MatchCount = 200;

    [Fact]
    public void BiggerSkatersThrowMoreHits()
    {
        var big = TestTeams.Create("Big", (_, _) => TestTeams.AverageRating, _ => TestBiography.Create(heightInches: 77, weightPounds: 235));
        var small = TestTeams.Create("Small", (_, _) => TestTeams.AverageRating, _ => TestBiography.Create(heightInches: 69, weightPounds: 175));

        var (bigHits, smallHits) = HitsBy(big, small);

        Assert.True(bigHits > smallHits * 1.3, $"Big {bigHits}, small {smallHits} hits.");
    }

    [Fact]
    public void CheckingAndToughnessDriveHitting()
    {
        var physical = TestTeams.Create("Physical", (_, rating) => rating is Rating.Checking or Rating.Toughness ? 90 : TestTeams.AverageRating);
        var finesse = TestTeams.Create("Finesse", (_, rating) => rating is Rating.Checking or Rating.Toughness ? 40 : TestTeams.AverageRating);

        var (physicalHits, finesseHits) = HitsBy(physical, finesse);

        Assert.True(physicalHits > finesseHits * 1.3, $"Physical {physicalHits}, finesse {finesseHits} hits.");
    }

    private static (int First, int Second) HitsBy(Team first, Team second)
    {
        var hits = TestMatches.SimulateMany(TestTeams.CreateMatch(first, second), MatchCount)
            .SelectMany(result => result.Events.OfType<HitEvent>())
            .ToList();
        return (hits.Count(hit => hit.TeamId == first.Id), hits.Count(hit => hit.TeamId == second.Id));
    }
}