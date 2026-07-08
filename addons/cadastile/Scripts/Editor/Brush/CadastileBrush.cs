#if TOOLS
using Godot;

namespace Cadastile.Editor.Brush;

/// <summary>
/// A brush = the CONTENT of a paint (what a cell becomes), independent of the interaction. A tool owns
/// a list of brushes and picks one per button (primary = left, secondary = right); the brush only
/// concerns itself with how to POPULATE the tool's selection -- the edit + a preview tint per cell.
/// </summary>
public abstract class CadastileBrush
{
    /// <summary>Label shown on the brush's button in the panel.</summary>
    public abstract string Name { get; }

    /// <summary>Accent/preview color for this brush (preview fill tint + "would clear" outline).</summary>
    public abstract Color Tint { get; }

    /// <summary>The edit this brush makes to a cell: a value = paint (source, kind); null = erase.</summary>
    public abstract (int source, WorldCellKind kind)? EditFor(CadastileCursor cursor);
}
#endif
