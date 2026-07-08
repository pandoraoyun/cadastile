#if TOOLS
using System.Collections.Generic;
using Godot;

namespace Cadastile.Editor.Brush;

/// <summary>
/// Handles viewport input, holds the GLOBAL cursor state (coordinates, active source, held button),
/// owns the tool list, and draws its own overlay. Both mouse buttons run the active tool; the button
/// only chooses the brush (left = primary, right = secondary), which the tool holds. Middle = pan.
/// Pressing the other button mid-drag cancels. The plugin calls <see cref="Draw"/> from
/// _ForwardCanvasDrawOverViewport.
/// </summary>
public sealed class CadastileCursor
{
    /// <summary>The mouse's current global (world) position. The tool/overlay derive the cell from it.</summary>
    public Vector2 MousePosition { get; private set; }

    /// <summary>Whether the mouse is over a position in the viewport.</summary>
    public bool HasCursor { get; private set; }

    /// <summary>The active paint source (selected from the panel). -1 = none / any source.</summary>
    public int ActiveSourceId { get; set; } = -1;

    /// <summary>The mouse button currently held for a drag (None if nothing held). A tool reads this to
    /// tell a primary (left) action from a secondary (right) one.</summary>
    public MouseButton HeldButton => _heldButton;

    /// <summary>The available tools (interactions), owned here; the panel renders the tool bar from them.</summary>
    public IReadOnlyList<CadastileTool> Tools => _tools;

    /// <summary>The active tool (interaction) run by both buttons.</summary>
    public CadastileTool ActiveTool { get; set; }

    /// <summary>The editor's undo/redo manager (set by the plugin). Each stroke becomes one undo step.</summary>
    public EditorUndoRedoManager UndoRedo { get; set; }

    // --- Overlay settings (could move to EditorSettings later) ---

    /// <summary>Whether the guide overlay is drawn (can be toggled on/off).</summary>
    public bool ShowGuide { get; set; } = true;

    /// <summary>The world grid's radius around the cursor (in cells).</summary>
    public int GuideRadius { get; set; } = 4;

    private static readonly Color WorldGridColor = new(0.35f, 0.85f, 0.45f, 0.50f);
    private static readonly Color CursorFill     = new(0.35f, 0.85f, 0.45f, 0.16f);
    private static readonly Color CursorOutline  = new(0.50f, 0.95f, 0.60f, 0.90f);
    private const float WorldLineWidth     = 1.0f;
    private const float CursorOutlineWidth = 1.6f;
    private const float DashLength         = 4f;

    private readonly List<CadastileTool> _tools;

    // The held button (for drag painting). None = nothing held.
    private MouseButton _heldButton = MouseButton.None;

    public CadastileCursor()
    {
        _tools = new List<CadastileTool> { new NoneTool(), new DrawTool(), new LineTool(), new RectangleTool(), new EraseTool() };
        ActiveTool = _tools[1]; // Draw
    }

    /// <summary>
    /// Handles viewport input. Left = primary brush, right = secondary brush; both run ActiveTool. The
    /// middle button is UNTOUCHED (pan). Pressing the other button mid-drag cancels the drag.
    /// </summary>
    /// <returns>(handled, changed): handled = was the input consumed; changed = should the overlay refresh.</returns>
    public (bool handled, bool changed) HandleInput(InputEvent @event, CadastileGridLayer layer)
    {
        if (layer == null) return (false, false);

        switch (@event)
        {
            case InputEventMouseButton mb:
            {
                if (mb.ButtonIndex is MouseButton.Left or MouseButton.Right)
                {
                    if (mb.Pressed)
                    {
                        // Another button is already held (a drag is in progress) -> cancel it, no commit.
                        if (_heldButton != MouseButton.None && mb.ButtonIndex != _heldButton)
                        {
                            UpdateCursor(layer);
                            ActiveTool?.OnCancel(layer, this);
                            _heldButton = MouseButton.None;
                            CommitStroke(layer);
                            return (true, true);
                        }

                        _heldButton = mb.ButtonIndex;
                        UpdateCursor(layer);
                        layer.BeginStroke();
                        ActiveTool?.OnPress(layer, this);
                        return (true, true); // consumed + refresh
                    }

                    // release: run the tool's release (e.g. rectangle commits here), then drop the button
                    if (mb.ButtonIndex == _heldButton)
                    {
                        UpdateCursor(layer);
                        ActiveTool?.OnRelease(layer, this);
                        _heldButton = MouseButton.None;
                        CommitStroke(layer);
                        return (true, true);
                    }
                    return (true, false);
                }
                break;
            }

            case InputEventMouseMotion:
            {
                UpdateCursor(layer);

                // drag with a held button -> the tool's drag step (continuous paint, or rect preview)
                if (_heldButton != MouseButton.None)
                {
                    ActiveTool?.OnDrag(layer, this);
                    return (true, true);
                }
                return (false, true); // don't consume, just refresh the overlay
            }
        }

        return (false, false);
    }

    /// <summary>Resets the cursor when the selection changes.</summary>
    public void ResetCursor() => HasCursor = false;

    /// <summary>
    /// Draws the cursor overlay: faint/dashed world grid + the cursor's world cell + the active
    /// tool's preview. The plugin calls this from _ForwardCanvasDrawOverViewport.
    /// </summary>
    public void Draw(Control overlay, CadastileGridLayer layer)
    {
        if (!ShowGuide || !HasCursor || layer?.TileSet == null)
            return;

        Transform2D xform = layer.GetViewportTransform() * layer.GetGlobalTransform();
        Vector2 ts = layer.TileSizePx;
        Vector2 half = ts * 0.5f;
        Vector2I c = layer.LocalToWorldCell(layer.ToLocal(MousePosition));

        DrawFadingWorldGrid(overlay, xform, ts, c, GuideRadius);

        // The cursor's world cell -- the world grid is offset half a tile from the display, hence +half.
        Vector2 topLeft = new(c.X * ts.X + half.X, c.Y * ts.Y + half.Y);
        overlay.DrawCellFill(xform, topLeft, ts, CursorFill);
        overlay.DrawCellOutline(xform, topLeft, ts, CursorOutline, CursorOutlineWidth);

        // Let the active tool draw its own render-grid preview (what will be painted).
        ActiveTool?.Draw(overlay, layer, this);
    }

    // Updates the mouse's global position (the tool/overlay read this and derive the cell themselves).
    private void UpdateCursor(CadastileGridLayer layer)
    {
        MousePosition = layer.GetGlobalMousePosition();
        HasCursor = true;
    }

    // Ends the current stroke and registers it as one editor undo step (do = after state, undo = before).
    private void CommitStroke(CadastileGridLayer layer)
    {
        if (!layer.EndStroke(out var before, out var after) || UndoRedo == null)
            return;

        UndoRedo.CreateAction("CadasTile");
        UndoRedo.AddDoMethod(layer, "RestoreCells", after);
        UndoRedo.AddUndoMethod(layer, "RestoreCells", before);
        UndoRedo.CommitAction(false); // already applied during the stroke; don't re-run the Do
    }

    // A thin, dashed world grid whose alpha fades outward from the center. The world grid is offset
    // half a tile from the display grid (+half); its lines pass through the display cells' centers.
    private static void DrawFadingWorldGrid(Control overlay, Transform2D xform, Vector2 ts, Vector2I c, int r)
    {
        float baseA = WorldGridColor.A;
        float span = r + 1;
        Vector2 h = ts * 0.5f;

        // Vertical lines
        for (int gx = c.X - r; gx <= c.X + r + 1; gx++)
        {
            float f = 1f - Mathf.Abs(gx - c.X) / span; // 1 at center, 0 at the edge
            if (f <= 0f) continue;
            Color col = WorldGridColor; col.A = baseA * f;
            Vector2 a = xform * new Vector2(gx * ts.X + h.X, (c.Y - r) * ts.Y + h.Y);
            Vector2 b = xform * new Vector2(gx * ts.X + h.X, (c.Y + r + 1) * ts.Y + h.Y);
            overlay.DrawDashedLine(a, b, col, WorldLineWidth, DashLength);
        }

        // Horizontal lines
        for (int gy = c.Y - r; gy <= c.Y + r + 1; gy++)
        {
            float f = 1f - Mathf.Abs(gy - c.Y) / span;
            if (f <= 0f) continue;
            Color col = WorldGridColor; col.A = baseA * f;
            Vector2 a = xform * new Vector2((c.X - r) * ts.X + h.X, gy * ts.Y + h.Y);
            Vector2 b = xform * new Vector2((c.X + r + 1) * ts.X + h.X, gy * ts.Y + h.Y);
            overlay.DrawDashedLine(a, b, col, WorldLineWidth, DashLength);
        }
    }
}
#endif
