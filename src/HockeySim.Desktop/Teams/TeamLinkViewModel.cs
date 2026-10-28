using System.Windows.Input;

namespace HockeySim.Desktop.Teams;

/// <summary>A team's name where a view lays out several in one line, such as a matchup.</summary>
/// <param name="OpenCommand">Opens the team's page; absent where the name links nowhere.</param>
public sealed record TeamLinkViewModel(string Name, ICommand? OpenCommand);