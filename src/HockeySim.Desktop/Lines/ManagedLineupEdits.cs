using CommunityToolkit.Mvvm.ComponentModel;

using HockeySim.Desktop.Game;

namespace HockeySim.Desktop.Lines;

/// <summary>
/// The managed team's lineup being edited, shared by every page that shows it (the Lines page and
/// the managed team's page) so an unsaved edit made on one shows on the other and either can save
/// or revert it. Edits stay local until saved, when Management validates and applies the whole
/// lineup at once.
/// </summary>
public sealed class ManagedLineupEdits
{
    private readonly GameSession _session;
    private object? _builtFrom;
    private LineupEditorViewModel _editor;

    public ManagedLineupEdits(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _editor = CreateEditor();
    }

    /// <summary>Raised when the editor is replaced or any of the other state changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the editable lineup.</summary>
    public LineupEditorViewModel Editor => _editor;

    public bool HasChanges { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? StatusMessage { get; private set; }

    /// <summary>
    /// Rebuilds the lineup from the latest snapshot unless it has unsaved edits, which survive
    /// unrelated game state changes such as reading a message. Pages sharing the edits may each
    /// call this for one snapshot; only the first rebuilds.
    /// </summary>
    public void Refresh()
    {
        if (HasChanges || ReferenceEquals(_builtFrom, _session.Snapshot))
        {
            return;
        }

        _editor = CreateEditor();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Applies the lineup through Management, or reports why it was rejected.</summary>
    public void Save()
    {
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            _session.SetLineup(_editor.CreateCommand());
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = $"The lineup was not saved. {exception.Message}";
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        Reload("Lineup saved.");
    }

    /// <summary>Discards the unsaved edits.</summary>
    public void Revert()
    {
        Reload("Changes discarded.");
    }

    private void Reload(string status)
    {
        _editor = CreateEditor();
        ErrorMessage = null;
        StatusMessage = status;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private LineupEditorViewModel CreateEditor()
    {
        _builtFrom = _session.Snapshot;
        HasChanges = false;
        return new LineupEditorViewModel(_session.ManagedTeam, isEditable: true, OnEdited);
    }

    private void OnEdited()
    {
        StatusMessage = null;
        ErrorMessage = null;
        HasChanges = _editor.HasChanges;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}