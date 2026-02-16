public class League
{
    public int SeasonYear { get; set; }

    // league data for sim
    public List<Team> Teams { get; set; } = new();
    public List<GameRecord> GamesCompleted { get; set; } = new();
    public Schedule Schedule { get; set; } = new();
}