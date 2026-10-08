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
        CanPlay = player.CanPlay;
        InjuryMarker = InjuryDisplay.StatusMarker(player);
        InjurySummary = InjuryDisplay.Summary(player);
    }

    public PlayerId Id { get; }

    public Position Position { get; }

    /// <summary>Gets the abbreviation of the player's natural position.</summary>
    public string PositionAbbreviation { get; }

    public string Number { get; }

    public string Name { get; }

    public int Age { get; }

    /// <summary>Gets whether the player can play today: they have no injury they cannot play through.</summary>
    public bool CanPlay { get; }

    /// <summary>OUT, INJ, or nothing for a healthy player.</summary>
    public string? InjuryMarker { get; }

    public bool HasInjury => InjuryMarker is not null;

    public bool IsOut => !CanPlay;

    public bool IsPlayingHurt => HasInjury && CanPlay;

    public string InjurySummary { get; }

    public override string ToString() => $"{Number} {Name}";
}