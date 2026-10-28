using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Game;
using HockeySim.Domain;

namespace HockeySim.Desktop.Injuries;

/// <summary>
/// The managed team's injury report: each injury that has not healed, players who cannot play
/// first, with its status and expected return.
/// </summary>
public sealed partial class InjuriesPageViewModel : ShellPageViewModel
{
    private readonly GameSession _session;
    private readonly Action<PlayerId>? _openPlayer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInjuries), nameof(Subtitle))]
    private IReadOnlyList<InjuryReportRowViewModel> _injuries = [];

    /// <param name="openPlayer">Opens a player's profile from the report; names are plain text without it.</param>
    public InjuriesPageViewModel(GameSession session, Action<PlayerId>? openPlayer = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _openPlayer = openPlayer;
        Refresh();
    }

    public override string Subtitle
    {
        get
        {
            var outCount = Injuries.Count(row => row.IsOut);
            return string.Create(
                CultureInfo.CurrentCulture,
                $"{_session.ManagedTeam.Name} · {outCount} out · {Injuries.Count - outCount} playing hurt");
        }
    }

    public bool HasInjuries => Injuries.Count > 0;

    public override void Refresh()
    {
        // Players who cannot play come first, then those playing hurt; each in roster order.
        Injuries = _session.ManagedTeam.Roster
            .SelectMany(player => player.Injuries.Select(injury => (Player: player, Injury: injury)))
            .OrderBy(entry => entry.Injury.CanPlayThrough)
            .Select(entry => new InjuryReportRowViewModel(entry.Player, entry.Injury, _openPlayer))
            .ToList();
    }
}