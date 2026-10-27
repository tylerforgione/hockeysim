using System.Globalization;

using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Players;

/// <summary>One injury in a player's profile.</summary>
public sealed class PlayerInjuryViewModel
{
    public PlayerInjuryViewModel(InjurySnapshot injury)
    {
        ArgumentNullException.ThrowIfNull(injury);

        Name = InjuryDisplay.Name(injury.Type);
        BodyPart = injury.BodyPart.ToString();
        Status = InjuryDisplay.Status(injury);
        IsOut = !injury.CanPlayThrough;
        Suffered = injury.Date.ToString("ddd d MMM", CultureInfo.CurrentCulture);
        ExpectedReturn = InjuryDisplay.ExpectedReturn(injury.ExpectedReturn);

        // The catalogue's reductions are rules of the game, not hidden state, so the user may see
        // what playing through costs.
        Effect = injury.CanPlayThrough
            ? "Plays at " + string.Join(
                ", ",
                InjuryCatalogue.For(injury.Type).RatingReductions
                    .OrderBy(reduction => reduction.Key)
                    .Select(reduction => $"−{reduction.Value} {PlayerDisplay.RatingName(reduction.Key)}"))
            : "Cannot play until healed";
    }

    public string Name { get; }

    /// <summary>The injured body part, such as Ankle.</summary>
    public string BodyPart { get; }

    /// <summary>Out, or playing through.</summary>
    public string Status { get; }

    public bool IsOut { get; }

    /// <summary>The date of the match in which the player was hurt.</summary>
    public string Suffered { get; }

    /// <summary>The staff's estimate: a date, or the earliest and latest dates.</summary>
    public string ExpectedReturn { get; }

    /// <summary>What the injury does: the rating points it costs, or that the player cannot play.</summary>
    public string Effect { get; }
}