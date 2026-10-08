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
        Handedness = player.Biography.Handedness;
        HandednessAbbreviation = PlayerDisplay.HandednessAbbreviation(Handedness);
        Number = $"#{player.Number}";
        Name = PlayerDisplay.FullName(player);
        Age = player.Age;
    }

    public PlayerId Id { get; }

    public Position Position { get; }

    /// <summary>Gets the abbreviation of the player's natural position.</summary>
    public string PositionAbbreviation { get; }

    public Handedness Handedness { get; }

    /// <summary>Gets "L" or "R": the hand the player shoots or catches with.</summary>
    public string HandednessAbbreviation { get; }

    public string Number { get; }

    public string Name { get; }

    public int Age { get; }

    public override string ToString() => $"{Number} {Name}";
}