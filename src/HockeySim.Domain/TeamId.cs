namespace HockeySim.Domain;

public readonly record struct TeamId(Guid Value)
{
    public override string ToString() => Value.ToString("D");
}