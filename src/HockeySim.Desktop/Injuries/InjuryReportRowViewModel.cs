using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using HockeySim.Desktop.Players;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Injuries;

/// <summary>One injury in a team's injury report.</summary>
public sealed class InjuryReportRowViewModel
{
    /// <param name="openPlayer">Opens the player's profile; the name is plain text without it.</param>
    public InjuryReportRowViewModel(PlayerSnapshot player, InjurySnapshot injury, Action<PlayerId>? openPlayer = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(injury);

        OpenPlayerCommand = openPlayer is null ? null : new RelayCommand(() => openPlayer(player.Id));
        Name = PlayerDisplay.FullName(player);
        Position = PlayerDisplay.PositionAbbreviation(player.Position);
        Injury = InjuryDisplay.Name(injury.Type);
        Status = InjuryDisplay.Status(injury);
        IsOut = !injury.CanPlayThrough;
        ExpectedReturn = InjuryDisplay.ExpectedReturn(injury.ExpectedReturn);
    }

    public string Name { get; }

    /// <summary>Opens the player's profile; absent where the name links nowhere.</summary>
    public ICommand? OpenPlayerCommand { get; }

    public string Position { get; }

    public string Injury { get; }

    /// <summary>Out, or playing through.</summary>
    public string Status { get; }

    public bool IsOut { get; }

    public string ExpectedReturn { get; }
}