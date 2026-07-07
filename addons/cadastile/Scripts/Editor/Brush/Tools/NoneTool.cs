#if TOOLS
using Godot;

namespace Cadastile.Editor.Brush.Tools;

/// <summary>Does nothing -- a safe empty tool that prevents accidental painting while selecting/panning.</summary>
public sealed class NoneTool : CadastileTool
{
    public override string Name => "None";

    public override void Apply(CadastileGridLayer layer, CadastileCursor cursor) { }
}
#endif
