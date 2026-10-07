using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Players;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Lines;

public sealed partial class PlayerOptionViewModel : ObservableObject
{
    /// <summary>
    /// Gets or sets whether the player is scratched in the lineup being edited. Scratched skaters
    /// stay listed for unit slots but cannot be saved there.
    /// </summary>
    [ObservableProperty]
    private bool _isScratched;

    public PlayerOptionViewModel(PlayerSnapshot player)
    {
        ArgumentNullException.ThrowIfNull(player);

        Id = player.Id;
        Position = player.Position;
        PositionAbbreviation = PlayerDisplay.PositionAbbreviation(player.Position);
        Number = $"#{player.Number}";
        Name = PlayerDisplay.FullName(player);
        Age = player.Age;
    }

    public PlayerId Id { get; }

    public Position Position { get; }

    public string PositionAbbreviation { get; }

    public string Number { get; }

    public string Name { get; }

    public int Age { get; }

    public override string ToString() => $"{Number} {Name}";
}