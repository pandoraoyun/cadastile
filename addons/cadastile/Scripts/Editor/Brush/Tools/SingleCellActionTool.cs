#if TOOLS
using Godot;

namespace Cadastile.Editor.Brush.Tools;

/// <summary>
/// Base for tools that act on the SINGLE world cell under the mouse. Resolves the cell from the
/// cursor's mouse position in one place (layer.ToLocal + LocalToWorldCell); subclasses only specify
/// what to do to that cell (<see cref="ApplyToCell"/>). Multi-cell / region tools derive directly
/// from <see cref="CadastileTool"/> and write their own Apply instead.
/// </summary>
public abstract class SingleCellActionTool : CadastileTool
{
    public sealed override void Apply(CadastileGridLayer layer, CadastileCursor cursor)
    {
        Vector2I cell = layer.LocalToWorldCell(layer.ToLocal(cursor.MousePosition));
        ApplyToCell(layer, cell);
    }

    /// <summary>Applies the tool's effect to the resolved single world cell.</summary>
    protected abstract void ApplyToCell(CadastileGridLayer layer, Vector2I cell);

    // Painting a single world cell affects the 4 display cells it is a corner of. The preview walks
    // those 4 cells; the drawing of each (fill/outline) is left to the subclass. Those cells render
    // NATIVELY at d*ts (the dual-grid shift is on the world-grid side; the display is native), so the
    // preview is drawn at d*ts to line up exactly with the real tiles.
    public sealed override void Draw(Control overlay, CadastileGridLayer layer, CadastileCursor cursor)
    {
        Transform2D xform = layer.GetViewportTransform() * layer.GetGlobalTransform();
        Vector2 ts = layer.TileSizePx;
        Vector2I c = layer.LocalToWorldCell(layer.ToLocal(cursor.MousePosition));

        foreach (Vector2I off in CadastileGridLayer.AffectedDisplayOffsets)
        {
            Vector2I d = c + off;
            Vector2 topLeft = new(d.X * ts.X, d.Y * ts.Y);
            DrawDisplayCell(overlay, xform, topLeft, ts);
        }
    }

    /// <summary>Draws the preview for one affected display cell (fill/outline is up to the subclass).</summary>
    protected virtual void DrawDisplayCell(Control overlay, Transform2D xform, Vector2 topLeft, Vector2 size) { }
}
#endif
