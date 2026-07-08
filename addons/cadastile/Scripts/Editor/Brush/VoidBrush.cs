#if TOOLS
using Godot;

namespace Cadastile.Editor.Brush;

/// <summary>Paints void (Negative) with the active source.</summary>
public sealed class VoidBrush : CadastileBrush
{
    public override string Name => "Lower";
    public override Color Tint => new(0.55f, 0.30f, 0.75f, 0.28f);

    public override (int source, WorldCellKind kind)? EditFor(CadastileCursor cursor)
        => (cursor.ActiveSourceId, WorldCellKind.Negative);
}
#endif
