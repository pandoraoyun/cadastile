#if TOOLS
using System.Collections.Generic;
using Godot;

namespace Cadastile.Editor.Brush.Tools;

/// <summary>Does nothing (no brushes, empty selection) -- prevents accidental painting while selecting/panning.</summary>
public sealed class NoneTool : CadastileTool
{
    public NoneTool() : base() { }

    public override string Name => "None";
    public override string IconName => "ToolSelect";

    protected override IEnumerable<Vector2I> CollectCells(CadastileCursor cursor, Vector2I cursorCell)
    {
        yield break;
    }
}
#endif
