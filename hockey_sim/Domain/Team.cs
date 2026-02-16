public class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    // roster
    public List<Player> Roster { get; set; } = new();
    public List<List<Player>> ForwardLines { get; set; } = new();
    public List<List<Player>> DefencePairs { get; set; } = new();
    public List<Player> Goalies { get; set; } = new();

    // standings data
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int OTLosses { get; set; }
    public int SOLosses { get; set; }
    public int Points => Wins * 2 + (OTLosses + SOLosses);
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
}