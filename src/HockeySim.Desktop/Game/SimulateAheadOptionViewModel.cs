using System.Windows.Input;

using HockeySim.Management.SeasonPlay;

namespace HockeySim.Desktop.Game;

/// <summary>One choice in Continue's menu: how far to simulate ahead.</summary>
/// <param name="Command">The shell's simulate-ahead command, given <paramref name="Target"/> as its parameter.</param>
public sealed record SimulateAheadOptionViewModel(SimulationTarget Target, string Label, ICommand Command)
{
    public static string LabelFor(SimulationTarget target) => target switch
    {
        SimulationTarget.NextDay => "Next day",
        SimulationTarget.NextMatch => "To the next match",
        SimulationTarget.OneWeek => "One week",
        SimulationTarget.OneMonth => "One month",
        SimulationTarget.EndOfPreseason => "To the end of the preseason",
        SimulationTarget.EndOfRegularSeason => "To the playoffs",
        SimulationTarget.EndOfRound => "To the end of the round",
        SimulationTarget.EndOfPlayoffs => "To the end of the playoffs",
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown simulation target."),
    };
}