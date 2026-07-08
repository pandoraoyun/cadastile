#if TOOLS
using System.Collections.Generic;
using Godot;

namespace Cadastile.Editor.Brush.Tools;

/// <summary>
/// A dedicated eraser that composes the existing interactions: left button = single-cell erase (Draw),
/// right button = rectangle erase (Rect). It holds a Draw and a Rect instance (both wired to only erase)
/// and routes the lifecycle to one of them based on the held button. No new interaction logic here.
/// </summary>
public sealed class EraseTool : CadastileTool
{
    private readonly DrawTool _single = new();
    private readonly RectangleTool _rect = new();

    public EraseTool() : base() // the eraser itself offers no brush choice
    {
        var erase = new EraseBrush();
        _single.PrimaryBrush = _single.SecondaryBrush = erase; // left  -> single-cell erase
        _rect.PrimaryBrush = _rect.SecondaryBrush = erase;     // right -> rectangle erase
    }

    public override string Name => "Erase";
    public override string IconName => "Eraser";

    // Route: right button -> rectangle erase; anything else (left / hover) -> single-cell erase.
    private CadastileTool Inner(CadastileCursor cursor)
        => cursor.HeldButton == MouseButton.Right ? _rect : _single;

    public override void OnPress(CadastileGridLayer layer, CadastileCursor cursor)   => Inner(cursor).OnPress(layer, cursor);
    public override void OnDrag(CadastileGridLayer layer, CadastileCursor cursor)    => Inner(cursor).OnDrag(layer, cursor);
    public override void OnRelease(CadastileGridLayer layer, CadastileCursor cursor) => Inner(cursor).OnRelease(layer, cursor);
    public override void OnCancel(CadastileGridLayer layer, CadastileCursor cursor)  => Inner(cursor).OnCancel(layer, cursor);

    public override void Draw(Control overlay, CadastileGridLayer layer, CadastileCursor cursor)
        => Inner(cursor).Draw(overlay, layer, cursor);

    // Never used -- delegation handles everything -- but required by the base.
    protected override IEnumerable<Vector2I> CollectCells(CadastileCursor cursor, Vector2I cursorCell)
    {
        yield break;
    }
}
#endif
