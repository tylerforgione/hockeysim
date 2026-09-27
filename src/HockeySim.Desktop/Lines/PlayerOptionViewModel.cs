using HockeySim.Desktop.Players;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Lines;

public sealed class PlayerOptionViewModel
{
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