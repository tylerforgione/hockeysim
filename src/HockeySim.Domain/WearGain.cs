namespace HockeySim.Domain;

/// <summary>
/// Hidden wear one player's body part took in a completed match, from impacts and injuries. Never
/// shown to the user.
/// </summary>
public sealed record WearGain
{
    /// <param name="points">Wear added; always positive.</param>
    public WearGain(PlayerId playerId, BodyPart bodyPart, int points)
    {
        if (!Enum.IsDefined(bodyPart))
        {
            throw new ArgumentOutOfRangeException(nameof(bodyPart), "The body part is not recognised.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);

        PlayerId = playerId;
        BodyPart = bodyPart;
        Points = points;
    }

    public PlayerId PlayerId { get; }

    public BodyPart BodyPart { get; }

    public int Points { get; }
}