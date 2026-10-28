namespace HockeySim.Desktop.Game;

/// <summary>
/// Where the shell is: a page, the section it is shown in, and the page's entity selection, such
/// as the team chosen or the message open. It is a history entry. The section is kept because the
/// player profile belongs to none and shows the one it was opened from.
/// </summary>
/// <param name="Selection">
/// The page's <see cref="ISelectablePage.Selection"/>, a value record owned by the page, or null
/// for a page with none. Opening a page without one keeps what the page last showed.
/// </param>
public sealed record ShellLocation(ShellPage Page, ShellSection Section, object? Selection = null);