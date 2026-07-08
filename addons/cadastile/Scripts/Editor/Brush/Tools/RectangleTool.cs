#if TOOLS
using System.Collections.Generic;
using Godot;

namespace Cadastile.Editor.Brush.Tools;

/// <summary>
/// Rectangle region tool. Press a corner, drag to size (previewed live), release to commit; pressing
/// the other button mid-drag cancels. Left drag = primary brush, right drag = secondary brush (so
/// right-drag becomes a rectangle erase when secondary is the erase brush). Only the selection lives
/// here; content + preview come from the active brush.
/// </summary>
public sealed class RectangleTool : CadastileTool
{
    private Vector2I? _anchor;

    public RectangleTool() : base(new TerrainBrush(), new VoidBrush(), new EraseBrush()) { }

    public override string Name => "Rect";
    public override string IconName => "Rectangle";

    public override void OnPress(CadastileGridLayer layer, CadastileCursor cursor)
        => _anchor = layer.LocalToWorldCell(layer.ToLocal(cursor.MousePosition));

    public override void OnDrag(CadastileGridLayer layer, CadastileCursor cursor) { } // preview only

    public override void OnRelease(CadastileGridLayer layer, CadastileCursor cursor)
    {
        Apply(layer, cursor); // commits the rectangle with the active brush
        _anchor = null;
    }

    public override void OnCancel(CadastileGridLayer layer, CadastileCursor cursor) => _anchor = null;

    // The rectangle from the anchor to the cursor. With no anchor (just hovering) it is the single
    // cursor cell, so hover still previews one blob.
    protected override IEnumerable<Vector2I> CollectCells(CadastileCursor cursor, Vector2I cursorCell)
    {
        Vector2I a = _anchor ?? cursorCell;
        int minX = a.X < cursorCell.X ? a.X : cursorCell.X;
        int maxX = a.X > cursorCell.X ? a.X : cursorCell.X;
        int minY = a.Y < cursorCell.Y ? a.Y : cursorCell.Y;
        int maxY = a.Y > cursorCell.Y ? a.Y : cursorCell.Y;

        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                yield return new Vector2I(x, y);
    }
}
#endif
