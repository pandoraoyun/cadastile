#if TOOLS
using System.Collections.Generic;
using Godot;

namespace Cadastile.Editor.Brush.Tools;

/// <summary>Continuous single-cell painting: applies the active brush to the cursor cell on press and drag.</summary>
public sealed class DrawTool : CadastileTool
{
    public DrawTool() : base(new TerrainBrush(), new VoidBrush(), new EraseBrush()) { }

    public override string Name => "Draw";
    public override string IconName => "Edit";

    protected override IEnumerable<Vector2I> CollectCells(CadastileCursor cursor, Vector2I cursorCell)
    {
        yield return cursorCell;
    }
}
#endif
