public class GameRecord
{
    public DateTime Date { get; set; }
    public Guid HomeId { get; set; }
    public Guid AwayId { get; set; }

    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }

    public bool Played => HomeGoals.HasValue;
}