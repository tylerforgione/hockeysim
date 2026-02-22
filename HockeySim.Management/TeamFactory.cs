namespace HockeySim.Domain;

public class TeamFactory
{
    // creates a team with 12 forwards, 6 defencemen, 2 goalies
    public static Team CreateTeam(string name)
    {
        var team = new Team { Name = name };

        // random forward lines
        for (int i = 0; i < 4; i++)
        {
            var line = new List<Player>
            {
                PlayerGenerator.CreateRandomSkater(Position.W),
                PlayerGenerator.CreateRandomSkater(Position.C),
                PlayerGenerator.CreateRandomSkater(Position.W),
            };

            team.ForwardLines.Add(line);
            team.Roster.AddRange(line);
        }

        // random defence pairs
        for (int j = 0; j < 3; j++)
        {
            var pair = new List<Player>
            {
                PlayerGenerator.CreateRandomSkater(Position.D),
                PlayerGenerator.CreateRandomSkater(Position.D),
            };

            team.DefencePairs.Add(pair);
            team.Roster.AddRange(pair);
        }

        // random goalies
        for (int j = 0; j < 3; j++)
        {
            var goalie = PlayerGenerator.CreateRandomSkater(Position.G);

            team.Goalies.Add(goalie);
            team.Roster.Add(goalie);
        }

        return team;
    }
}