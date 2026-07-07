#if TOOLS
using Godot;

namespace Cadastile.Editor.Brush.Tools;

/// <summary>Paints terrain (Positive) or void (Negative); which one is chosen by the mode.</summary>
public sealed class DrawTool : SingleCellActionTool
{
    private const int ModeVoid = 1;

    private static readonly Color TerrainPreview = new(0.35f, 0.85f, 0.45f, 0.22f);
    private static readonly Color VoidPreview    = new(0.55f, 0.30f, 0.75f, 0.22f);

    public override string Name => "Draw";
    public override string[] Modes { get; } = { "Terrain", "Void" };

    protected override void ApplyToCell(CadastileGridLayer layer, Vector2I cell)
    {
        WorldCellKind kind = SelectedMode == ModeVoid ? WorldCellKind.Negative : WorldCellKind.Positive;
        layer.Paint(cell, kind);
    }

    // Faintly fill the affected display cell by mode (terrain = green, void = purple).
    protected override void DrawDisplayCell(Control overlay, Transform2D xform, Vector2 topLeft, Vector2 size)
    {
        Color fill = SelectedMode == ModeVoid ? VoidPreview : TerrainPreview;
        overlay.DrawCellFill(xform, topLeft, size, fill);
    }
}
#endif
