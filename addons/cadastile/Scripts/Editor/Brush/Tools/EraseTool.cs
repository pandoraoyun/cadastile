#if TOOLS
using Godot;

namespace Cadastile.Editor.Brush.Tools;

/// <summary>Clears the cell entirely (Empty). Right click always uses this; it can also be selected for left click.</summary>
public sealed class EraseTool : SingleCellActionTool
{
    private static readonly Color ErasePreview = new(0.95f, 0.35f, 0.35f, 0.75f);
    private const float ErasePreviewWidth = 1.4f;

    public override string Name => "Erase";

    protected override void ApplyToCell(CadastileGridLayer layer, Vector2I cell) => layer.Erase(cell);

    // Mark the affected display cell with a faint red outline.
    protected override void DrawDisplayCell(Control overlay, Transform2D xform, Vector2 topLeft, Vector2 size)
        => overlay.DrawCellOutline(xform, topLeft, size, ErasePreview, ErasePreviewWidth);
}
#endif
