using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace HockeySim.Desktop.Lines;

/// <summary>
/// Wires the Lines page's drag handles and drop slots to the lineup editor. Pressing a handle and
/// moving past a small distance drags that player; a ghost of the player follows the pointer and
/// releasing over a slot drops them there. The drag is in-process rather than an OS drag, which
/// would show the system's file icon and cannot be given a custom visual. The view model decides
/// which slots accept the drop, so this only translates pointer events.
/// </summary>
internal sealed class LineupDragDrop
{
    // Marks the grip a player is picked up by, and a slot (or goalie choice) that can be dropped on.
    private const string HandleClass = "drag-handle";
    private const string SlotClass = "drop-slot";

    private const double DragDistance = 4;

    // Keeps the ghost clear of the pointer so the slot underneath stays visible.
    private static readonly Vector GhostOffset = new(14, 10);

    private readonly Control _page;
    private Control? _handle;
    private IPointer? _pointer;
    private TopLevel? _topLevel;
    private Point _pressedAt;
    private PlayerOptionViewModel? _pressedPlayer;
    private LineupEditorViewModel? _lineup;

    private LineupDragDrop(Control page)
    {
        _page = page;
    }

    /// <summary>Handles drags from the handles under <paramref name="page"/>, whose data context is the Lines page.</summary>
    public static void Attach(Control page)
    {
        var dragDrop = new LineupDragDrop(page);
        page.AddHandler(InputElement.PointerPressedEvent, dragDrop.OnPointerPressed, handledEventsToo: true);
        page.AddHandler(InputElement.PointerMovedEvent, dragDrop.OnPointerMoved, handledEventsToo: true);
        page.AddHandler(InputElement.PointerReleasedEvent, dragDrop.OnPointerReleased, handledEventsToo: true);
        page.DataContextChanged += (_, _) => dragDrop.Cancel();
        page.DetachedFromVisualTree += (_, _) => dragDrop.Cancel();

        // Keys go to the focused control, which may be none, so Escape is caught on the window.
        page.AttachedToVisualTree += (_, _) =>
        {
            dragDrop._topLevel = TopLevel.GetTopLevel(page);
            dragDrop._topLevel?.AddHandler(InputElement.KeyDownEvent, dragDrop.OnKeyDown, RoutingStrategies.Tunnel);
        };
        page.DetachedFromVisualTree += (_, _) =>
        {
            dragDrop._topLevel?.RemoveHandler(InputElement.KeyDownEvent, dragDrop.OnKeyDown);
            dragDrop._topLevel = null;
        };
    }

    private LinesPageViewModel? Lines => _page.DataContext as LinesPageViewModel;

    private bool IsDragging => _lineup is not null;

    private Border? Ghost => _page.FindControl<Border>("DragGhost");

    private static Control? Find(object? source, string cssClass) =>
        (source as Visual)?.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(control => control.Classes.Contains(cssClass));

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_page).Properties.IsLeftButtonPressed
            || Find(e.Source, HandleClass) is not { } handle)
        {
            return;
        }

        _pressedPlayer = handle.DataContext switch
        {
            LineupSlotViewModel slot => slot.SelectedPlayer,
            PlayerOptionViewModel player => player,
            _ => null,
        };
        if (_pressedPlayer is not null)
        {
            _handle = handle;
            _pressedAt = e.GetPosition(_page);
            _pointer = e.Pointer;
            e.Pointer.Capture(handle);
            handle.PointerCaptureLost += OnCaptureLost;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_handle is null || _pressedPlayer is not { } player || Lines is not { } lines)
        {
            return;
        }

        var position = e.GetPosition(_page);
        if (!IsDragging)
        {
            var offset = position - _pressedAt;
            if (Math.Abs(offset.X) < DragDistance && Math.Abs(offset.Y) < DragDistance)
            {
                return;
            }

            // A read-only lineup ignores the drag.
            if (!lines.Lineup.IsEditable || Ghost is not { } ghost)
            {
                Cancel();
                return;
            }

            _lineup = lines.Lineup;
            _lineup.BeginDrag(player);
            ghost.FindControl<TextBlock>("DragGhostText")!.Text = player.ToString();
            ghost.IsVisible = true;
        }

        MoveGhost(position);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var lineup = _lineup;

        // The ghost ignores hit testing, so this finds the slot under it.
        var slot = lineup is null ? null : Find(_page.InputHitTest(e.GetPosition(_page)), SlotClass)?.DataContext as LineupSlotViewModel;
        EndPress();

        if (slot is not null)
        {
            lineup!.Drop(slot);
        }
        else
        {
            lineup?.EndDrag();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _handle is not null)
        {
            Cancel();
            e.Handled = true;
        }
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        Cancel();
    }

    private void MoveGhost(Point position)
    {
        if (Ghost is { } ghost)
        {
            Canvas.SetLeft(ghost, position.X + GhostOffset.X);
            Canvas.SetTop(ghost, position.Y + GhostOffset.Y);
        }
    }

    // Abandons any press or drag without dropping.
    private void Cancel()
    {
        var lineup = _lineup;
        EndPress();
        lineup?.EndDrag();
    }

    // Forgets the press: hides the ghost and releases the pointer.
    private void EndPress()
    {
        _lineup = null;
        _pressedPlayer = null;
        if (Ghost is { } ghost)
        {
            ghost.IsVisible = false;
        }

        if (_handle is { } handle)
        {
            _handle = null;
            handle.PointerCaptureLost -= OnCaptureLost;
            _pointer?.Capture(null);
        }

        _pointer = null;
    }
}