#if TOOLS
using Godot;

namespace Cadastile.Editor;

/// <summary>Coordinate helpers for <see cref="CadastileGridLayer"/>: world-cell centers and mouse/local -> world-cell mapping.</summary>
public static class CadastileGridLayerExtensions
{
    /// <summary>Local-space center of a world cell.</summary>
    public static Vector2 WorldCellCenterLocal(this CadastileGridLayer layer, Vector2I worldCell)
    {
        Vector2 ts = layer.TileSizePx;
        return new Vector2(worldCell.X * ts.X, worldCell.Y * ts.Y) + ts / 2f;
    }

    /// <summary>Maps a local-space position to a world cell.</summary>
    public static Vector2I LocalToWorldCell(this CadastileGridLayer layer, Vector2 local)
    {
        Vector2 ts = layer.TileSizePx;
        // The world grid is offset half a tile from the display grid (dual-grid interlock). Shifting
        // local back by half a tile before the floor maps the mouse to the world cell at the CENTER
        // of the 2x2 display block it AFFECTS -> the cursor is centered on the block, not shifted
        // down-right.
        return new Vector2I(
            Mathf.FloorToInt((local.X - ts.X * 0.5f) / ts.X),
            Mathf.FloorToInt((local.Y - ts.Y * 0.5f) / ts.Y));
    }
}
#endif
