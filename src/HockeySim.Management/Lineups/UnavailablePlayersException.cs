using System.Collections.ObjectModel;

using HockeySim.Domain;

namespace HockeySim.Management.Lineups;

/// <summary>
/// The managed team plays today, but its lineup dresses players who cannot play. The user must
/// replace them before the day can be played.
/// </summary>
public sealed class UnavailablePlayersException : InvalidOperationException
{
    internal UnavailablePlayersException(IReadOnlyList<Player> players)
        : base(
            "Replace the injured players who cannot play before your team's match: "
            + string.Join(", ", players.Select(player => $"{player.FirstName} {player.LastName} (#{player.Number})"))
            + ".")
    {
        PlayerIds = new ReadOnlyCollection<PlayerId>(players.Select(player => player.Id).ToList());
    }

    /// <summary>The dressed players who cannot play, in lineup order.</summary>
    public IReadOnlyList<PlayerId> PlayerIds { get; }
}