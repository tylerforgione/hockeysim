namespace HockeySim.Domain;

public class Conference
{
    public string Name { get; set; } = "";
    public List<Division> Divisions { get; set; } = new();
}