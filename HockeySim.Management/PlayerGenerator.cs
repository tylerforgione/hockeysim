namespace HockeySim.Domain;

public class PlayerGenerator
{
    public static Player CreateRandomSkater(Position pos)
    {
        Random random = new Random((int)DateTime.Now.Ticks);
        var p = new Player
        {
            Position = pos,
            Age = random.Next(18, 36)
        };

        foreach (Rating r in Enum.GetValues<Rating>())
        {
            p.SetRating(r, random.Next(40, 90));
        }

        return p;
    }
}