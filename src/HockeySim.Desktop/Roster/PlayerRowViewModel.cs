using HockeySim.Desktop.Players;
using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Roster;

/// <summary>
/// One roster table row. Rating abbreviations follow the table headers in <c>TeamRosterView</c>.
/// </summary>
public sealed class PlayerRowViewModel
{
    public PlayerRowViewModel(PlayerSnapshot player, LineupSnapshot lineup)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(lineup);

        Player = player;
        Number = player.Number;
        Name = PlayerDisplay.FullName(player);
        Position = PlayerDisplay.PositionAbbreviation(player.Position);
        Age = player.Age;
        LineupRole = PlayerDisplay.LineupRole(player.Id, lineup);
        IsScratched = LineupRole == "Scratch";

        Skating = player.Ratings[Rating.Skating];
        ShotPower = player.Ratings[Rating.ShotPower];
        ShotAccuracy = player.Ratings[Rating.ShotAccuracy];
        PuckControl = player.Ratings[Rating.PuckControl];
        Passing = player.Ratings[Rating.Passing];
        OffensiveAwareness = player.Ratings[Rating.OffensiveAwareness];
        DefensiveAwareness = player.Ratings[Rating.DefensiveAwareness];
        Checking = player.Ratings[Rating.Checking];
        ShotBlocking = player.Ratings[Rating.ShotBlocking];
        StickChecking = player.Ratings[Rating.StickChecking];
        GoalieReflex = player.Ratings[Rating.GoalieReflex];
        GoaliePositioning = player.Ratings[Rating.GoaliePositioning];
        GoalieReboundControl = player.Ratings[Rating.GoalieReboundControl];
    }

    public PlayerSnapshot Player { get; }

    public int Number { get; }

    public string Name { get; }

    public string Position { get; }

    public int Age { get; }

    public string LineupRole { get; }

    public bool IsScratched { get; }

    public int Skating { get; }

    public int ShotPower { get; }

    public int ShotAccuracy { get; }

    public int PuckControl { get; }

    public int Passing { get; }

    public int OffensiveAwareness { get; }

    public int DefensiveAwareness { get; }

    public int Checking { get; }

    public int ShotBlocking { get; }

    public int StickChecking { get; }

    public int GoalieReflex { get; }

    public int GoaliePositioning { get; }

    public int GoalieReboundControl { get; }
}