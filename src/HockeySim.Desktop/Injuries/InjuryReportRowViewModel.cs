using HockeySim.Desktop.Players;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Injuries;

/// <summary>One injury in a team's injury report.</summary>
public sealed class InjuryReportRowViewModel
{
    public InjuryReportRowViewModel(PlayerSnapshot player, InjurySnapshot injury)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(injury);

        Name = PlayerDisplay.FullName(player);
        Position = PlayerDisplay.PositionAbbreviation(player.Position);
        Injury = InjuryDisplay.Name(injury.Type);
        Status = InjuryDisplay.Status(injury);
        IsOut = !injury.CanPlayThrough;
        ExpectedReturn = InjuryDisplay.ExpectedReturn(injury.ExpectedReturn);
    }

    public string Name { get; }

    public string Position { get; }

    public string Injury { get; }

    /// <summary>Out, or playing through.</summary>
    public string Status { get; }

    public bool IsOut { get; }

    public string ExpectedReturn { get; }
}