namespace HockeySim.Domain;

public class GameRecord
{
    // game identification info
    public DateTime Date { get; set; }
    public Guid HomeId { get; set; }
    public Guid AwayId { get; set; }

    // game outcome info
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }

    // been player or no
    public bool Played => HomeGoals.HasValue;
}