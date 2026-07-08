#if TOOLS
using Godot;

namespace Cadastile.Editor.Brush;

/// <summary>Paints terrain (Positive) with the active source.</summary>
public sealed class TerrainBrush : CadastileBrush
{
    public override string Name => "Higher";
    public override Color Tint => new(0.35f, 0.85f, 0.45f, 0.28f);

    public override (int source, WorldCellKind kind)? EditFor(CadastileCursor cursor)
        => (cursor.ActiveSourceId, WorldCellKind.Positive);
}
#endif
