namespace HockeySim.Domain;

public class League
{
    public int SeasonYear { get; set; }

    // league data for sim
    public List<Conference> Conferences { get; set; } = new();
    public List<Team> Teams =>
        Conferences.SelectMany(c => c.Divisions).SelectMany(d => d.Teams).ToList();
    public List<GameRecord> GamesCompleted { get; set; } = new();
    public Schedule Schedule { get; set; } = new();
}