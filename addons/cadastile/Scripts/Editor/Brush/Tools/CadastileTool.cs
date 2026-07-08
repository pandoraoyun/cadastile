#if TOOLS
using System.Collections.Generic;
using Godot;

namespace Cadastile.Editor.Brush.Tools;

/// <summary>
/// A tool = an interaction wrapper. It owns the brushes available to it and the SELECTION logic (which
/// cells to affect); the active brush (primary on left, secondary on right) populates that selection.
/// Subclasses provide the selection via <see cref="CollectCells"/> and, if drag-based, override the
/// lifecycle. Apply + preview are shared here.
/// </summary>
public abstract class CadastileTool
{
    private static readonly Color GhostModulate = new(1f, 1f, 1f, 0.55f);
    private const float ClearOutlineWidth = 1.4f;

    private readonly List<CadastileBrush> _brushes;

    protected CadastileTool(params CadastileBrush[] brushes)
    {
        _brushes = new List<CadastileBrush>(brushes);
        PrimaryBrush = _brushes.Count > 0 ? _brushes[0] : null;
        SecondaryBrush = _brushes.Count > 2 ? _brushes[2] : PrimaryBrush;
    }

    /// <summary>The label shown on the tool's button in the panel.</summary>
    public abstract string Name { get; }

    /// <summary>Editor theme icon name (from EditorIcons) for the button; null = show the text label.</summary>
    public virtual string IconName => null;

    /// <summary>The brushes this tool offers (shown in the panel while the tool is active).</summary>
    public IReadOnlyList<CadastileBrush> Brushes => _brushes;

    /// <summary>Brush applied by the left button.</summary>
    public CadastileBrush PrimaryBrush { get; set; }

    /// <summary>Brush applied by the right button.</summary>
    public CadastileBrush SecondaryBrush { get; set; }

    /// <summary>The cells this tool targets this frame (the selection). Draw: one; Rect: a rectangle.</summary>
    protected abstract IEnumerable<Vector2I> CollectCells(CadastileCursor cursor, Vector2I cursorCell);

    public virtual void Apply(CadastileGridLayer layer, CadastileCursor cursor)
    {
        foreach ((Vector2I cell, (int src, WorldCellKind kind)? e) in BuildEdits(layer, cursor))
        {
            if (e is (var s, var k))
                layer.Paint(cell, s, k);
            else
                layer.Erase(cell);
        }
    }

    public virtual void Draw(Control overlay, CadastileGridLayer layer, CadastileCursor cursor)
    {
        CadastileBrush brush = ActiveBrush(cursor);
        if (brush == null)
            return;

        Dictionary<Vector2I, (int src, WorldCellKind kind)?> edits = BuildEdits(layer, cursor);
        if (edits.Count == 0)
            return;

        Transform2D xform = layer.GetViewportTransform() * layer.GetGlobalTransform();
        Vector2 ts = layer.TileSizePx;
        Color tint = brush.Tint;

        var shown = new HashSet<Vector2I>();
        foreach (Vector2I cell in edits.Keys)
            foreach (Vector2I off in CadastileGridLayer.AffectedDisplayOffsets)
            {
                Vector2I d = cell + off;
                if (!shown.Add(d))
                    continue;

                CadastileRef? preview = layer.PreviewResolve(d, edits);
                Vector2 topLeft = new(d.X * ts.X, d.Y * ts.Y);
                DrawPreviewCell(overlay, layer, preview, tint, xform, topLeft, ts);
            }
    }

    // --- Interaction lifecycle (cursor-driven). Default = continuous painting. ---
    public virtual void OnPress(CadastileGridLayer layer, CadastileCursor cursor) => Apply(layer, cursor);
    public virtual void OnDrag(CadastileGridLayer layer, CadastileCursor cursor) => Apply(layer, cursor);
    public virtual void OnRelease(CadastileGridLayer layer, CadastileCursor cursor) { }
    public virtual void OnCancel(CadastileGridLayer layer, CadastileCursor cursor) { }

    // The active brush for the current button: right = secondary, else primary (also while hovering).
    private CadastileBrush ActiveBrush(CadastileCursor cursor)
        => cursor.HeldButton == MouseButton.Right ? SecondaryBrush : PrimaryBrush;

    // Applies the active brush's edit uniformly to every targeted cell.
    private Dictionary<Vector2I, (int src, WorldCellKind kind)?> BuildEdits(CadastileGridLayer layer, CadastileCursor cursor)
    {
        var edits = new Dictionary<Vector2I, (int src, WorldCellKind kind)?>();
        CadastileBrush brush = ActiveBrush(cursor);
        if (brush == null)
            return edits;

        (int source, WorldCellKind kind)? value = brush.EditFor(cursor);
        Vector2I cursorCell = layer.LocalToWorldCell(layer.ToLocal(cursor.MousePosition));
        foreach (Vector2I cell in CollectCells(cursor, cursorCell))
            edits[cell] = value;
        return edits;
    }

    // Tint fill behind + the resulting tile as a ghost on top (the fill tints it); if the cell would end
    // up empty, a faint outline in the tint marks it.
    private static void DrawPreviewCell(Control overlay, CadastileGridLayer layer, CadastileRef? preview,
                                        Color tint, Transform2D xform, Vector2 topLeft, Vector2 size)
    {
        overlay.DrawCellFill(xform, topLeft, size, tint);

        if (preview is CadastileRef r
            && layer.TileSet is { } ts
            && ts.GetSource(r.SourceId) is TileSetAtlasSource atlas
            && atlas.Texture is { } texture)
        {
            Rect2 region = atlas.GetTileTextureRegion(r.AtlasCoords);
            overlay.DrawTilePreview(xform, topLeft, size, texture, region, GhostModulate);
        }
        else
        {
            overlay.DrawCellOutline(xform, topLeft, size, tint, ClearOutlineWidth);
        }
    }
}
#endif
