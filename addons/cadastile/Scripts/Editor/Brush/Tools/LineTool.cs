#if TOOLS
using System.Collections.Generic;
using Godot;

namespace Cadastile.Editor.Brush.Tools;

/// <summary>
/// Line region tool. Press a point, drag to the other end (previewed live), release to commit; pressing
/// the other button mid-drag cancels. Left drag = primary brush, right drag = secondary. Same drag
/// lifecycle as Rect -- only the selection differs (a Bresenham line instead of a filled rectangle).
/// </summary>
public sealed class LineTool : CadastileTool
{
    private Vector2I? _anchor;

    public LineTool() : base(new TerrainBrush(), new VoidBrush(), new EraseBrush()) { }

    public override string Name => "Line";
    public override string IconName => "Line";

    public override void OnPress(CadastileGridLayer layer, CadastileCursor cursor)
        => _anchor = layer.LocalToWorldCell(layer.ToLocal(cursor.MousePosition));

    public override void OnDrag(CadastileGridLayer layer, CadastileCursor cursor) { } // preview only

    public override void OnRelease(CadastileGridLayer layer, CadastileCursor cursor)
    {
        Apply(layer, cursor);
        _anchor = null;
    }

    public override void OnCancel(CadastileGridLayer layer, CadastileCursor cursor) => _anchor = null;

    // Bresenham line from the anchor to the cursor. No anchor (hovering) = the single cursor cell.
    protected override IEnumerable<Vector2I> CollectCells(CadastileCursor cursor, Vector2I cursorCell)
    {
        Vector2I a = _anchor ?? cursorCell;

        int dx = Mathf.Abs(cursorCell.X - a.X);
        int dy = -Mathf.Abs(cursorCell.Y - a.Y);
        int sx = a.X < cursorCell.X ? 1 : -1;
        int sy = a.Y < cursorCell.Y ? 1 : -1;
        int err = dx + dy;

        int x = a.X, y = a.Y;
        while (true)
        {
            yield return new Vector2I(x, y);
            if (x == cursorCell.X && y == cursorCell.Y)
                break;

            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x += sx; }
            if (e2 <= dx) { err += dx; y += sy; }
        }
    }
}
#endif
