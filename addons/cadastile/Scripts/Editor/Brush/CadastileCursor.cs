#if TOOLS
using Godot;

namespace Cadastile.Editor.Brush;

/// <summary>
/// Handles viewport input, holds the cursor state, AND draws its own overlay. Left click runs the
/// selected tool (ActiveTool), right click always runs the EraseTool; the middle button is left
/// untouched (editor pan). Drawing: a faint/dashed world grid (fading outward from the center) + the
/// cursor's world cell + the active tool's own render-grid preview. The plugin calls
/// <see cref="Draw"/> from _ForwardCanvasDrawOverViewport.
/// </summary>
public sealed class CadastileCursor
{
    /// <summary>The mouse's current global (world) position. The tool/overlay derive the cell from it.</summary>
    public Vector2 MousePosition { get; private set; }

    /// <summary>Whether the mouse is over a position in the viewport.</summary>
    public bool HasCursor { get; private set; }

    /// <summary>The active tool run by left click (selected from the panel).</summary>
    public CadastileTool ActiveTool { get; set; }

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

    // Right click always erases; independent of the selected tool.
    private readonly CadastileTool _rightTool = new EraseTool();

    // The held button (for drag painting). None = nothing held.
    private MouseButton _heldButton = MouseButton.None;

    /// <summary>
    /// Handles viewport input. Left = ActiveTool, right = EraseTool. The middle button is UNTOUCHED
    /// (pan). Holding and dragging applies continuously.
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
                        _heldButton = mb.ButtonIndex;
                        UpdateCursor(layer);
                        ApplyAtCursor(layer);
                        return (true, true); // consumed + refresh
                    }

                    // release: if it's the same button, the drag is done
                    if (mb.ButtonIndex == _heldButton)
                        _heldButton = MouseButton.None;
                    return (true, false);
                }
                break;
            }

            case InputEventMouseMotion:
            {
                UpdateCursor(layer);

                // apply while dragging with a held button
                if (_heldButton != MouseButton.None)
                {
                    ApplyAtCursor(layer);
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

    // Runs the tool for the held button; the tool derives the cell from cursor.MousePosition. Left=ActiveTool, right=erase.
    private void ApplyAtCursor(CadastileGridLayer layer)
    {
        CadastileTool tool = _heldButton == MouseButton.Right ? _rightTool : ActiveTool;
        tool?.Apply(layer, this);
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
