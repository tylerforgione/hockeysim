using HockeySim.Domain;
using HockeySim.Management.Scheduling;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Management.NewGame;

internal static class LeagueGenerator
{
    private const int CentreCount = 5;
    private const int WingCount = 8;
    private const int DefenceCount = 7;
    private const int GoalieCount = 3;

    public static League Create(int seasonYear, ControlledRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);

        // Ages are generated for opening day, so they read the same in the first snapshot.
        var openingDay = ScheduleGenerator.OpeningDay(seasonYear);
        var conferences = FictionalLeagueData.Conferences
            .Select(conference => CreateConference(conference, openingDay, random))
            .ToList();

        return new League(seasonYear, conferences);
    }

    private static Conference CreateConference(
        FictionalLeagueData.ConferenceDefinition definition,
        DateOnly openingDay,
        ControlledRandom random)
    {
        var divisions = definition.Divisions
            .Select(division => CreateDivision(division, openingDay, random))
            .ToList();

        return new Conference(definition.Name, divisions);
    }

    private static Division CreateDivision(
        FictionalLeagueData.DivisionDefinition definition,
        DateOnly openingDay,
        ControlledRandom random)
    {
        var teams = definition.TeamNames
            .Select(teamName => CreateTeam(teamName, openingDay, random))
            .ToList();

        return new Division(definition.Name, teams);
    }

    private static Team CreateTeam(string name, DateOnly openingDay, ControlledRandom random)
    {
        var numbers = CreatePlayerNumbers(random);
        var players = new List<Player>(Team.RequiredRosterSize);

        AddPlayers(players, Position.Centre, CentreCount, numbers, openingDay, random);
        AddPlayers(players, Position.Wing, WingCount, numbers, openingDay, random);
        AddPlayers(players, Position.Defence, DefenceCount, numbers, openingDay, random);
        AddPlayers(players, Position.Goalie, GoalieCount, numbers, openingDay, random);

        var centres = players.Where(player => player.Position == Position.Centre).ToList();
        var wings = players.Where(player => player.Position == Position.Wing).ToList();
        var defencePlayers = players.Where(player => player.Position == Position.Defence).ToList();
        var goalies = players.Where(player => player.Position == Position.Goalie).ToList();

        var forwardLines = Enumerable.Range(0, Lineup.RequiredForwardLineCount)
            .Select(index =>
            {
                var (leftWing, rightWing) = OnNaturalSides(wings[index * 2], wings[(index * 2) + 1]);
                return new ForwardLine(leftWing, centres[index], rightWing);
            })
            .ToList();
        var defencePairs = Enumerable.Range(0, Lineup.RequiredDefencePairCount)
            .Select(index =>
            {
                var (leftDefence, rightDefence) = OnNaturalSides(defencePlayers[index * 2], defencePlayers[(index * 2) + 1]);
                return new DefencePair(leftDefence, rightDefence);
            })
            .ToList();
        var lineup = Lineup.CreateWithDefaultUnits(forwardLines, defencePairs, goalies[0], goalies[1]);

        return new Team(new TeamId(random.NextGuid()), name, players, lineup);
    }

    /// <summary>
    /// Orders two wings or defence players left side first, swapping them only when that puts both
    /// on their forehand side. Pairs that shoot the same way keep their order, so one plays off-hand.
    /// </summary>
    private static (Player Left, Player Right) OnNaturalSides(Player first, Player second) =>
        first.Biography.Handedness == Handedness.Right && second.Biography.Handedness == Handedness.Left
            ? (second, first)
            : (first, second);

    private static void AddPlayers(
        ICollection<Player> players,
        Position position,
        int count,
        IReadOnlyList<int> numbers,
        DateOnly openingDay,
        ControlledRandom random)
    {
        for (var index = 0; index < count; index++)
        {
            players.Add(CreatePlayer(position, numbers[players.Count], openingDay, random));
        }
    }

    private static Player CreatePlayer(Position position, int number, DateOnly openingDay, ControlledRandom random)
    {
        var identity = PlayerBiographyGenerator.Create(position, openingDay, random);
        var ratings = PlayerRatingGenerator.Create(position, random);

        return new Player(
            new PlayerId(random.NextGuid()),
            identity.FirstName,
            identity.LastName,
            position,
            identity.Biography,
            number,
            ratings);
    }

    private static IReadOnlyList<int> CreatePlayerNumbers(ControlledRandom random)
    {
        var availableNumbers = Enumerable.Range(1, 99).ToList();
        var selectedNumbers = new List<int>(Team.RequiredRosterSize);

        for (var index = 0; index < Team.RequiredRosterSize; index++)
        {
            var selectedIndex = random.NextInt(0, availableNumbers.Count);
            selectedNumbers.Add(availableNumbers[selectedIndex]);
            availableNumbers.RemoveAt(selectedIndex);
        }

        return selectedNumbers;
    }
}