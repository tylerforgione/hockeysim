namespace HockeySim.Domain;

/// <summary>A round of the playoffs, in the order they are played.</summary>
public enum PlayoffRound
{
    /// <summary>Within each division's bracket: the division winner against a wild card, second against third.</summary>
    FirstRound,

    /// <summary>The two first-round winners of each division's bracket.</summary>
    SecondRound,

    /// <summary>The two second-round winners of each conference.</summary>
    ConferenceFinal,

    /// <summary>The two conference champions.</summary>
    Final,
}