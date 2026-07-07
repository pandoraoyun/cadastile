using Godot;
using Godot.Collections;

namespace Cadastile.Tilemap;

/// <summary>
/// The painted state of a world cell. A missing key means the cell is "Empty" (never painted).
/// Positive = terrain (a 1 in the corner mask); Negative = void (a 0 in the mask but a visible abyss
/// region). For the corner mask, Negative and Empty are the SAME (both 0); they only differ in a
/// None region: Negative -> abyss tile, Empty -> EraseCell (transparent).
/// </summary>
public enum WorldCellKind
{
    Positive,
    Negative,
}

/// <summary>
/// A dual-grid tile map layer. The user paints a virtual "world" grid (<see cref="WorldCellKind"/> per
/// cell, held in memory); each display cell's tile is resolved from the four world cells at its
/// corners via <see cref="CadastileTileSet"/>. The world grid is not serialized, so on load it is
/// rebuilt from the persisted display cells.
/// </summary>
[Tool]
[GlobalClass]
public partial class CadastileGridLayer : TileMapLayer
{
    private readonly Dictionary<Vector2I, WorldCellKind> _worldGrid = new();

    /// <summary>
    /// The TileSet slot. Exported as the base type -- Godot's inspector wires up the native TileSet
    /// flow for TileSet-derived slots, and 'New' ALWAYS creates a base TileSet (not the subclass).
    /// The base type avoids export marshalling crashes; the getter guard accepts only a
    /// CadastileTileSet or null, rejecting a plain TileSet with a warning.
    ///
    /// To CREATE a CadastileTileSet: NOT via the inspector 'New' (that makes a base) -- use the
    /// FileSystem dock > right-click > Create New > Resource > CadastileTileSet > save .tres > drag it in.
    /// </summary>
    [Export]
    public new CadastileTileSet TileSet
    {
        get => GetTileSet() as CadastileTileSet;
        set => SetTileSet(value);
    }

    /// <summary>Tile size (pixels). Returns a safe default when there is no TileSet.</summary>
    public Vector2I TileSizePx => GetTileSet()?.TileSize ?? new Vector2I(16, 16);

    /// <summary>
    /// _worldGrid is NOT serialized (in-memory, flushed on reload); but the display grid (the
    /// TileMapLayer cells) IS persisted. On load, if the world grid is empty, rebuild it from the
    /// persisted display -- otherwise painting next to existing tiles loses neighbor info and
    /// resolves wrong.
    /// </summary>
    public override void _Ready()
    {
        if (_worldGrid.Count == 0)
            RebuildWorldFromDisplay();
    }

    // --- World grid (virtual) -> display resolution ---

    // A world cell c is a corner (offset) of these 4 display cells. (Godot: +X right, +Y down.)
    // A wrong offset shifts resolution by half a tile -> keep it in ONE place, flip here if needed.
    // Public so the tool preview uses the same offset set (single source of truth).
    public static readonly Vector2I[] AffectedDisplayOffsets =
    {
        new(0, 0), new(1, 0), new(0, 1), new(1, 1),
    };

    private bool IsPositive(Vector2I worldCell)
        => _worldGrid.TryGetValue(worldCell, out WorldCellKind v) && v == WorldCellKind.Positive;

    /// <summary>Builds the mask for a display cell from the world cells at its 4 corners (only a Positive corner sets a bit).</summary>
    private CadasTileCorner MaskAt(Vector2I d)
    {
        CadasTileCorner m = CadasTileCorner.None;
        if (IsPositive(new Vector2I(d.X - 1, d.Y - 1))) m |= CadasTileCorner.Nw;
        if (IsPositive(new Vector2I(d.X,     d.Y - 1))) m |= CadasTileCorner.Ne;
        if (IsPositive(new Vector2I(d.X - 1, d.Y)))     m |= CadasTileCorner.Sw;
        if (IsPositive(new Vector2I(d.X,     d.Y)))     m |= CadasTileCorner.Se;
        return m;
    }

    /// <summary>
    /// Paints a world cell as <paramref name="kind"/> (Positive=terrain, Negative=void), then
    /// re-resolves the 4 display cells it affects.
    /// </summary>
    public void Paint(Vector2I worldCell, WorldCellKind kind)
    {
        _worldGrid[worldCell] = kind;
        RefreshAround(worldCell);
    }

    /// <summary>Clears a world cell entirely (Empty), then re-resolves the affected display cells.</summary>
    public void Erase(Vector2I worldCell)
    {
        _worldGrid.Remove(worldCell);
        RefreshAround(worldCell);
    }

    // Rebuilds _worldGrid from the persisted display cells. For each tile it reads the mask (inverse
    // of Resolve, via TryGetMask): Positive corners go to the matching world cells, an abyss (None)
    // tile goes to its representative world cell (Negative). Inverse of MaskAt:
    // Nw=(d-1,d-1) Ne=(d,d-1) Sw=(d-1,d) Se=(d,d). The persisted display is self-consistent, so a
    // cell can never be both Positive and Negative (no collision).
    private void RebuildWorldFromDisplay()
    {
        CadastileTileSet ts = TileSet;
        if (ts is null)
            return;

        foreach (Vector2I d in GetUsedCells())
        {
            int sourceId = GetCellSourceId(d);
            if (sourceId < 0)
                continue;
            if (!ts.TryGetMask(sourceId, GetCellAtlasCoords(d), out CadasTileCorner mask))
                continue;

            if (mask == CadasTileCorner.None)
            {
                // Abyss tile -> this display cell's representative world cell was Negative.
                _worldGrid[d] = WorldCellKind.Negative;
                continue;
            }

            if ((mask & CadasTileCorner.Nw) != 0) _worldGrid[new Vector2I(d.X - 1, d.Y - 1)] = WorldCellKind.Positive;
            if ((mask & CadasTileCorner.Ne) != 0) _worldGrid[new Vector2I(d.X,     d.Y - 1)] = WorldCellKind.Positive;
            if ((mask & CadasTileCorner.Sw) != 0) _worldGrid[new Vector2I(d.X - 1, d.Y)]     = WorldCellKind.Positive;
            if ((mask & CadasTileCorner.Se) != 0) _worldGrid[new Vector2I(d.X,     d.Y)]      = WorldCellKind.Positive;
        }
    }

    private void RefreshAround(Vector2I worldCell)
    {
        foreach (Vector2I off in AffectedDisplayOffsets)
            ResolveDisplay(worldCell + off);
    }

    private void ResolveDisplay(Vector2I displayCell)
    {
        CadastileTileSet ts = TileSet;
        if (ts is null)
            return;

        CadasTileCorner mask = MaskAt(displayCell);

        if (mask != CadasTileCorner.None)
        {
            // Transition / full tile. If the mask is untagged (shouldn't happen), EraseCell.
            if (ts.Resolve(mask) is CadastileRef r)
                SetCell(displayCell, r.SourceId, r.AtlasCoords);
            else
                EraseCell(displayCell);
            return;
        }

        // None: no corner is Positive. Representative cell = SE corner = displayCell.
        // If Negative, try the abyss ((0,0,0,0) tagged) tile; otherwise/Empty -> EraseCell.
        bool isNegative = _worldGrid.TryGetValue(displayCell, out WorldCellKind t) && t == WorldCellKind.Negative;
        if (isNegative && ts.Resolve(CadasTileCorner.None) is CadastileRef abyss)
            SetCell(displayCell, abyss.SourceId, abyss.AtlasCoords);
        else
            EraseCell(displayCell);
    }

    /// <summary>
    /// Hides the native "tile_set" slot inherited from TileMapLayer from both the inspector and
    /// storage. That leaves only the typed CadastileTileSet export above -- the user can't
    /// accidentally assign a plain TileSet, and the resource serializes through a single path
    /// (double serialization is prevented).
    /// </summary>
    public override void _ValidateProperty(Dictionary property)
    {
        if (property["name"].AsStringName() == "tile_set")
        {
            var usage = property["usage"].As<PropertyUsageFlags>();
            property["usage"] =
                (int)(usage & ~(PropertyUsageFlags.Editor | PropertyUsageFlags.Storage));
        }
    }
}
