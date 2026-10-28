using System.Windows.Input;

namespace HockeySim.Desktop.Players;

/// <summary>A player's name where a view lays out several in one line, such as a goal's assists.</summary>
/// <param name="Text">What is shown for the player.</param>
/// <param name="OpenCommand">Opens the player's profile; absent where the name links nowhere.</param>
/// <param name="Separator">What follows the name before the next one: ", " or nothing for the last.</param>
public sealed record PlayerLinkViewModel(string Text, ICommand? OpenCommand, string Separator);