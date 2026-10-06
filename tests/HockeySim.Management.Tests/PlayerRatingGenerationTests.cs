using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.NewGame;
using HockeySim.Simulation.Randomness;

using Xunit;

namespace HockeySim.Management.Tests;

/// <summary>
/// Checks that generated ratings differ believably by position. Each league has 160 centres,
/// 256 wings, 224 defence, and 96 goalies, so position averages are stable for a fixed seed;
/// the margins are wide enough that retuning the generator need not break them.
/// </summary>
public sealed class PlayerRatingGenerationTests
{
    [Theory]
    [MemberData(nameof(Seeds))]
    public void CentresAreTheStrongestFaceoffTakers(ulong seed)
    {
        var players = StartGame(seed);

        var centres = Average(players, Position.Centre, Rating.Faceoffs);
        var wings = Average(players, Position.Wing, Rating.Faceoffs);
        var defence = Average(players, Position.Defence, Rating.Faceoffs);

        Assert.True(centres > wings + 10, $"Centres {centres:F1}, wings {wings:F1}.");
        Assert.True(wings > defence, $"Wings {wings:F1}, defence {defence:F1}.");
        Assert.True(defence > Average(players, Position.Goalie, Rating.Faceoffs));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void DefenceDefendAndForwardsScore(ulong seed)
    {
        var players = StartGame(seed);

        foreach (var rating in new[] { Rating.DefensiveAwareness, Rating.ShotBlocking })
        {
            Assert.True(Average(players, Position.Defence, rating) > Average(players, Position.Centre, rating) + 3);
            Assert.True(Average(players, Position.Defence, rating) > Average(players, Position.Wing, rating) + 3);
        }

        foreach (var rating in new[] { Rating.ShotAccuracy, Rating.OffensiveAwareness })
        {
            Assert.True(Average(players, Position.Wing, rating) > Average(players, Position.Defence, rating) + 3);
            Assert.True(Average(players, Position.Centre, rating) > Average(players, Position.Defence, rating) + 3);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void OnlyGoaliesAreRatedForGoaltendingAndOnlySkatersForSkating(ulong seed)
    {
        var players = StartGame(seed);
        var skaterPositions = new[] { Position.Centre, Position.Wing, Position.Defence };

        foreach (var rating in new[] { Rating.GoalieReflex, Rating.GoaliePositioning, Rating.GoalieReboundControl })
        {
            Assert.InRange(Average(players, Position.Goalie, rating), 55, 85);
            Assert.All(skaterPositions, position => Assert.InRange(Average(players, position, rating), 0, 35));
        }

        foreach (var rating in new[] { Rating.Skating, Rating.ShotAccuracy, Rating.Passing, Rating.DefensiveAwareness })
        {
            Assert.InRange(Average(players, Position.Goalie, rating), 0, 40);
            Assert.All(skaterPositions, position => Assert.InRange(Average(players, position, rating), 50, 85));
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void OverallRatingsSpreadFromFringeToStarAtEveryPosition(ulong seed)
    {
        var players = StartGame(seed);

        foreach (var position in Enum.GetValues<Position>())
        {
            var overalls = players.Where(player => player.Position == position).Select(player => player.Overall).ToList();

            Assert.All(overalls, overall => Assert.InRange(overall, 40, 95));
            Assert.InRange(overalls.Average(), 58, 72);
            Assert.True(overalls.Max() - overalls.Min() >= 15, $"{position} overalls {overalls.Min()}-{overalls.Max()}.");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TraitsVaryForEveryPositionIncludingHiddenDurability(ulong seed)
    {
        var manager = new GameManager();
        manager.StartNewGame(new NewGameCommand(2026, new RandomState(seed), "Halifax Mariners"));
        var store = new MemorySaveStore();
        manager.SaveGame(store);
        var savedPlayers = store.Saved!.Conferences
            .SelectMany(conference => conference.Divisions)
            .SelectMany(division => division.Teams)
            .SelectMany(team => team.Roster)
            .ToList();

        foreach (var rating in new[] { Rating.Discipline, Rating.Stamina, Rating.Durability, Rating.Toughness })
        {
            foreach (var position in Enum.GetValues<Position>())
            {
                var values = savedPlayers
                    .Where(player => player.Position == position)
                    .Select(player => player.Ratings[rating])
                    .ToList();

                Assert.InRange(values.Average(), 40, 75);
                Assert.True(values.Max() - values.Min() >= 15, $"{position} {rating} {values.Min()}-{values.Max()}.");
            }
        }
    }

    public static TheoryData<ulong> Seeds() => new(7UL, 42UL, 2026UL);

    private static List<PlayerSnapshot> StartGame(ulong seed) =>
        new GameManager()
            .StartNewGame(new NewGameCommand(2026, new RandomState(seed), "Halifax Mariners"))
            .League.Teams
            .SelectMany(team => team.Roster)
            .ToList();

    private static double Average(IEnumerable<PlayerSnapshot> players, Position position, Rating rating) =>
        players.Where(player => player.Position == position).Average(player => player.Ratings[rating]);
}