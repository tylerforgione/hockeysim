namespace HockeySim.Domain;

/// <summary>
/// A skater's individual production in one completed match. An entry is an appearance; shootout
/// attempts are never counted.
/// </summary>
public sealed record SkaterBoxScore
{
    public SkaterBoxScore(PlayerId playerId, int goals, int assists)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(goals);
        ArgumentOutOfRangeException.ThrowIfNegative(assists);

        PlayerId = playerId;
        Goals = goals;
        Assists = assists;
    }

    public PlayerId PlayerId { get; }

    public int Goals { get; }

    public int Assists { get; }

    public int Points => Goals + Assists;
}