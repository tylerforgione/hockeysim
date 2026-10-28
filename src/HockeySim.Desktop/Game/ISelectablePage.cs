namespace HockeySim.Desktop.Game;

/// <summary>
/// A shell page whose choice of an entity, such as a team or a message, is a navigation history
/// entry. Choices of how to look at the entity, such as tabs and sorts, are not.
/// </summary>
public interface ISelectablePage
{
    /// <summary>Raised when the entity chosen changes.</summary>
    event EventHandler? SelectionChanged;

    /// <summary>Gets the entity chosen, as a value record with value equality.</summary>
    object Selection { get; }

    /// <summary>
    /// Chooses an entity from an earlier <see cref="Selection"/>. One that no longer exists falls
    /// back to the page's default. Raises <see cref="SelectionChanged"/> like any other change.
    /// </summary>
    void Select(object selection);
}