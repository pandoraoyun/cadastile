#if TOOLS
using System;
using Godot;

namespace Cadastile.Editor.Brush.Tools;

/// <summary>
/// A brush tool (strategy). The cursor runs the selected tool for left click and always the
/// EraseTool for right click. A tool may have optional modes (radio); the selected tool's modes
/// appear next to it in the panel and selecting one updates <see cref="SelectedMode"/>.
/// </summary>
public abstract class CadastileTool
{
    /// <summary>The label shown on the tool's button in the panel.</summary>
    public abstract string Name { get; }

    /// <summary>The modes offered as radio buttons. An empty array means a mode-less tool.</summary>
    public virtual string[] Modes => Array.Empty<string>();

    /// <summary>The selected mode index (into <see cref="Modes"/>). Unused for a mode-less tool.</summary>
    public int SelectedMode { get; set; }

    /// <summary>Applies the tool's effect; it derives the target cell from cursor.MousePosition + the layer.</summary>
    public abstract void Apply(CadastileGridLayer layer, CadastileCursor cursor);

    /// <summary>
    /// Draws the active tool's viewport preview (optional). The cursor calls this when ShowGuide is
    /// on. Default: draws nothing -- subclasses override to preview "what will happen".
    /// </summary>
    public virtual void Draw(Control overlay, CadastileGridLayer layer, CadastileCursor cursor) { }
}
#endif
