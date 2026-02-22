namespace HockeySim.Domain;

public class Division
{
    public string Name { get; set; } = "";
    public List<Team> Teams { get; set; } = new();
}