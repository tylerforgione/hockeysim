namespace HockeySim.Desktop.Players;

public sealed record RatingGroupViewModel(string Name, IReadOnlyList<RatingLineViewModel> Ratings);

/// <summary>
/// One rating as a bar. A player playing through an injury plays at less than their healthy rating:
/// <see cref="Value"/> is what they play at now and <see cref="Healthy"/> where the bar's mark sits.
/// </summary>
/// <param name="Value">The rating the player plays at today.</param>
/// <param name="Healthy">The rating when healthy.</param>
public sealed record RatingLineViewModel(string Name, int Value, int Healthy)
{
    /// <summary>The points an injury takes off.</summary>
    public int Lost => Healthy - Value;

    public bool HasLoss => Lost > 0;

    /// <summary>The points lost with a minus sign, such as −3, or empty when none are.</summary>
    public string LossText => HasLoss ? $"−{Lost}" : string.Empty;
}