#if TOOLS
using Godot;

namespace Cadastile.Editor.Brush;

/// <summary>Erases a cell (clears it to Empty); neighboring cells re-autotile.</summary>
public sealed class EraseBrush : CadastileBrush
{
    public override string Name => "Erase";
    public override Color Tint => new(0.90f, 0.32f, 0.32f, 0.24f);

    public override (int source, WorldCellKind kind)? EditFor(CadastileCursor cursor) => null;
}
#endif
