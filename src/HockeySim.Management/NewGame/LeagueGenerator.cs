using HockeySim.Domain;

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

        var conferences = FictionalLeagueData.Conferences
            .Select(conference => CreateConference(conference, random))
            .ToList();

        return new League(seasonYear, conferences);
    }

    private static Conference CreateConference(
        FictionalLeagueData.ConferenceDefinition definition,
        ControlledRandom random)
    {
        var divisions = definition.Divisions
            .Select(division => CreateDivision(division, random))
            .ToList();

        return new Conference(definition.Name, divisions);
    }

    private static Division CreateDivision(
        FictionalLeagueData.DivisionDefinition definition,
        ControlledRandom random)
    {
        var teams = definition.TeamNames
            .Select(teamName => CreateTeam(teamName, random))
            .ToList();

        return new Division(definition.Name, teams);
    }

    private static Team CreateTeam(string name, ControlledRandom random)
    {
        var numbers = CreatePlayerNumbers(random);
        var players = new List<Player>(Team.RequiredRosterSize);

        AddPlayers(players, Position.Centre, CentreCount, numbers, random);
        AddPlayers(players, Position.Wing, WingCount, numbers, random);
        AddPlayers(players, Position.Defence, DefenceCount, numbers, random);
        AddPlayers(players, Position.Goalie, GoalieCount, numbers, random);

        var centres = players.Where(player => player.Position == Position.Centre).ToList();
        var wings = players.Where(player => player.Position == Position.Wing).ToList();
        var defencePlayers = players.Where(player => player.Position == Position.Defence).ToList();
        var goalies = players.Where(player => player.Position == Position.Goalie).ToList();

        var forwardLines = Enumerable.Range(0, Lineup.RequiredForwardLineCount)
            .Select(index => new ForwardLine(wings[index * 2], centres[index], wings[(index * 2) + 1]))
            .ToList();
        var defencePairs = Enumerable.Range(0, Lineup.RequiredDefencePairCount)
            .Select(index => new DefencePair(defencePlayers[index * 2], defencePlayers[(index * 2) + 1]))
            .ToList();
        var lineup = new Lineup(forwardLines, defencePairs, goalies[0], goalies[1]);

        return new Team(new TeamId(random.NextGuid()), name, players, lineup);
    }

    private static void AddPlayers(
        ICollection<Player> players,
        Position position,
        int count,
        IReadOnlyList<int> numbers,
        ControlledRandom random)
    {
        for (var index = 0; index < count; index++)
        {
            players.Add(CreatePlayer(position, numbers[players.Count], random));
        }
    }

    private static Player CreatePlayer(Position position, int number, ControlledRandom random)
    {
        var firstName = FictionalLeagueData.FirstNames[
            random.NextInt(0, FictionalLeagueData.FirstNames.Count)];
        var lastName = FictionalLeagueData.LastNames[
            random.NextInt(0, FictionalLeagueData.LastNames.Count)];
        var ratings = Enum.GetValues<Rating>()
            .ToDictionary(rating => rating, _ => new RatingScore(random.NextInt(40, 91)));

        return new Player(
            new PlayerId(random.NextGuid()),
            firstName,
            lastName,
            position,
            random.NextInt(18, 36),
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