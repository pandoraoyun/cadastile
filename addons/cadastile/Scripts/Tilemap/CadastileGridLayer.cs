using Godot;
using Godot.Collections;
using PendingEdits = System.Collections.Generic.IReadOnlyDictionary<Godot.Vector2I, (int, Cadastile.Tilemap.WorldCellKind)?>;

namespace Cadastile.Tilemap;

/// <summary>
/// The painted state of a world cell. A missing entry means "Empty" (never painted). Positive =
/// terrain (a 1 in the corner mask); Negative = void (a 0 in the mask but a visible abyss region).
/// For the mask, Negative and Empty are the same (both 0); they only differ in a None region:
/// Negative -> abyss tile, Empty -> EraseCell (transparent).
/// </summary>
public enum WorldCellKind
{
    Positive,
    Negative,
}

/// <summary>
/// Abstract base for a dual-grid tile map layer. Holds all the shared world->display logic but NOT
/// the world-grid storage -- concrete subclasses (single- vs multi-source) provide storage by
/// overriding a few primitives (<see cref="TryReadCell"/> / <see cref="WriteCell"/> /
/// <see cref="RemoveCell"/> / <see cref="WorldCount"/>). Nothing here exposes the storage type.
///
/// The user paints a virtual "world" grid; each display cell's tile is resolved from the four world
/// cells at its corners via <see cref="CadastileTileSet"/>. The world grid is not serialized, so on
/// load it is rebuilt from the persisted display cells.
/// </summary>
[Tool]
public abstract partial class CadastileGridLayer : TileMapLayer
{
    /// <summary>
    /// The TileSet slot. Exported as the base type -- Godot's inspector wires up the native TileSet
    /// flow for TileSet-derived slots, and 'New' ALWAYS creates a base TileSet (not the subclass).
    /// The typed getter accepts only a CadastileTileSet or null.
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
    /// The world grid is NOT serialized (in-memory, flushed on reload); the display grid (the
    /// TileMapLayer cells) IS. On load, if the world grid is empty, rebuild it from the persisted
    /// display -- otherwise painting next to existing tiles loses neighbor info and resolves wrong.
    /// </summary>
    public override void _Ready()
    {
        if (WorldCount == 0)
            RebuildWorldFromDisplay();
    }

    // A world cell c is a corner (offset) of these 4 display cells. (Godot: +X right, +Y down.)
    // Keep in ONE place; the tool preview reads the same set (single source of truth).
    public static readonly Vector2I[] AffectedDisplayOffsets =
    {
        new(0, 0), new(1, 0), new(0, 1), new(1, 1),
    };

    // --- Storage primitives: the only thing subclasses provide ---

    /// <summary>Number of painted world cells (used for the load-time rebuild guard).</summary>
    protected abstract int WorldCount { get; }

    /// <summary>
    /// Reads a painted cell: its source (or -1 when the layer has no per-cell source) and its kind.
    /// False if the cell is unpainted.
    /// </summary>
    protected abstract bool TryReadCell(Vector2I cell, out int sourceId, out WorldCellKind kind);

    /// <summary>Writes a painted cell. A single-source layer may ignore <paramref name="sourceId"/>.</summary>
    protected abstract void WriteCell(Vector2I cell, int sourceId, WorldCellKind kind);

    /// <summary>Removes a painted cell (makes it Empty).</summary>
    protected abstract void RemoveCell(Vector2I cell);

    /// <summary>Clears all painted world cells (for a from-scratch rebuild).</summary>
    protected abstract void ClearWorld();

    /// <summary>All currently-painted world cells (for a full refresh after a global change, e.g. a source swap).</summary>
    protected abstract System.Collections.Generic.IEnumerable<Vector2I> WorldCells { get; }

    /// <summary>
    /// The source a paint would actually be stored under. A single-source layer ignores the source
    /// (returns -1) so the preview's hypothetical matches how the layer really stores/reads it.
    /// </summary>
    protected virtual int StoredSource(int sourceId) => sourceId;

    // --- Shared world -> display logic ---

    // Reads a cell, consulting 'pending' first (a hypothetical edit map for the preview): a present
    // entry with a value = painted (source, kind); a present entry that is null = erased (empty);
    // absent (or null map) = live storage. No state is mutated.
    private bool ReadCell(Vector2I cell, PendingEdits pending, out int sourceId, out WorldCellKind kind)
    {
        if (pending != null && pending.TryGetValue(cell, out (int src, WorldCellKind kind)? e))
        {
            if (e is (var s, var k)) { sourceId = StoredSource(s); kind = k; return true; }
            sourceId = -1; kind = default; return false; // pending erase -> empty
        }
        return TryReadCell(cell, out sourceId, out kind);
    }

    // Is the cell Positive, and which source painted it (-1 = none / single-source). A Positive cell
    // whose source was DELETED from the tile set is treated as empty, so stale multi-source data can't
    // corrupt the mask/owner of its neighbors. 'pending' overrides cells for a hypothetical preview.
    private bool TryPositive(Vector2I cell, PendingEdits pending, out int sourceId)
    {
        if (ReadCell(cell, pending, out sourceId, out WorldCellKind kind) && kind == WorldCellKind.Positive
            && (sourceId < 0 || TileSet?.HasSource(sourceId) == true))
            return true;
        sourceId = -1;
        return false;
    }

    // True if ANY of the display cell's 4 corners is Negative (void); reports the SE-priority source.
    // Void is corner-based just like terrain, so painting one void cell fills the same 2x2 block.
    private bool TryVoidCorner(Vector2I d, PendingEdits pending, out int voidSource)
    {
        voidSource = -1;
        bool any = false;
        if (ReadCell(new Vector2I(d.X - 1, d.Y - 1), pending, out int s, out WorldCellKind k) && k == WorldCellKind.Negative) { voidSource = s; any = true; }
        if (ReadCell(new Vector2I(d.X,     d.Y - 1), pending, out s, out k) && k == WorldCellKind.Negative) { voidSource = s; any = true; }
        if (ReadCell(new Vector2I(d.X - 1, d.Y),     pending, out s, out k) && k == WorldCellKind.Negative) { voidSource = s; any = true; }
        if (ReadCell(new Vector2I(d.X,     d.Y),     pending, out s, out k) && k == WorldCellKind.Negative) { voidSource = s; any = true; }
        return any;
    }

    /// <summary>
    /// Builds the mask + owner source for a display cell. Owner = the SE-priority source among the
    /// Positive corners. Only corners of the OWNER source set a bit; a corner of a DIFFERENT source
    /// counts as empty -- so each terrain autotiles against everything-not-itself, and at a seam the
    /// owner draws its own edge while the other recedes (last painted wins the cell). For a
    /// single-source layer every source is -1, so all Positive corners count and ownerSource stays -1
    /// (any-source resolve). None if no Positive corner.
    /// </summary>
    private CadasTileCorner MaskAt(Vector2I d, PendingEdits pending, out int ownerSource)
    {
        bool nw = TryPositive(new Vector2I(d.X - 1, d.Y - 1), pending, out int nwS);
        bool ne = TryPositive(new Vector2I(d.X,     d.Y - 1), pending, out int neS);
        bool sw = TryPositive(new Vector2I(d.X - 1, d.Y),     pending, out int swS);
        bool se = TryPositive(new Vector2I(d.X,     d.Y),     pending, out int seS);

        // Owner = last Positive corner in NW,NE,SW,SE order (SE-priority).
        ownerSource = -1;
        if (nw) ownerSource = nwS;
        if (ne) ownerSource = neS;
        if (sw) ownerSource = swS;
        if (se) ownerSource = seS;

        // Only the owner source's corners set a bit; a different source counts as empty.
        CadasTileCorner m = CadasTileCorner.None;
        if (nw && nwS == ownerSource) m |= CadasTileCorner.Nw;
        if (ne && neS == ownerSource) m |= CadasTileCorner.Ne;
        if (sw && swS == ownerSource) m |= CadasTileCorner.Sw;
        if (se && seS == ownerSource) m |= CadasTileCorner.Se;
        return m;
    }

    /// <summary>
    /// Paints a world cell as <paramref name="kind"/> with <paramref name="sourceId"/> (ignored by a
    /// single-source layer), then re-resolves the 4 display cells it affects.
    /// </summary>
    public void Paint(Vector2I worldCell, int sourceId, WorldCellKind kind)
    {
        CaptureBefore(worldCell);
        WriteCell(worldCell, sourceId, kind);
        RefreshAround(worldCell);
    }

    /// <summary>Clears a world cell entirely (Empty), then re-resolves the affected display cells.</summary>
    public void Erase(Vector2I worldCell)
    {
        CaptureBefore(worldCell);
        RemoveCell(worldCell);
        RefreshAround(worldCell);
    }

    // --- Undo/redo -----------------------------------------------------------------------------------
    // A stroke (press..release) is one undo step. We capture each touched world cell's state before it
    // first changes, encoded as a Vector2I (X = source, Y = kind: 0=Positive, 1=Negative, 2=Empty).
    // The cursor hands the before/after maps to EditorUndoRedoManager; RestoreCells is the do/undo entry
    // point. Restoring the world cells + re-resolving keeps the display grid in sync too.

    private Dictionary<Vector2I, Vector2I> _strokeBefore;

    /// <summary>Starts capturing world-cell changes for one undoable stroke.</summary>
    public void BeginStroke() => _strokeBefore = new Dictionary<Vector2I, Vector2I>();

    /// <summary>
    /// Ends the stroke. If anything changed, outputs the before/after encoded cell maps (for the plugin
    /// to register with the editor's undo history) and returns true; otherwise false.
    /// </summary>
    public bool EndStroke(out Dictionary<Vector2I, Vector2I> before, out Dictionary<Vector2I, Vector2I> after)
    {
        before = _strokeBefore;
        _strokeBefore = null;
        if (before == null || before.Count == 0)
        {
            before = null;
            after = null;
            return false;
        }
        after = new Dictionary<Vector2I, Vector2I>();
        foreach (Vector2I cell in before.Keys)
            after[cell] = EncodeCell(cell);
        return true;
    }

    /// <summary>Restores a set of encoded world cells (the undo/redo entry point) and re-resolves them.</summary>
    public void RestoreCells(Dictionary<Vector2I, Vector2I> cells)
    {
        foreach (System.Collections.Generic.KeyValuePair<Vector2I, Vector2I> kv in cells)
        {
            if (kv.Value.Y == 2)
                RemoveCell(kv.Key);
            else
                WriteCell(kv.Key, kv.Value.X, (WorldCellKind)kv.Value.Y);
        }

        var affected = new System.Collections.Generic.HashSet<Vector2I>();
        foreach (Vector2I cell in cells.Keys)
            foreach (Vector2I off in AffectedDisplayOffsets)
                affected.Add(cell + off);
        foreach (Vector2I d in affected)
            ResolveDisplay(d);
    }

    // Snapshots a cell's state before it is first mutated in the active stroke (no-op when not recording).
    private void CaptureBefore(Vector2I cell)
    {
        if (_strokeBefore != null && !_strokeBefore.ContainsKey(cell))
            _strokeBefore[cell] = EncodeCell(cell);
    }

    // Encodes a cell's current state: X = source, Y = kind (0=Positive, 1=Negative, 2=Empty).
    private Vector2I EncodeCell(Vector2I cell)
        => TryReadCell(cell, out int s, out WorldCellKind k) ? new Vector2I(s, (int)k) : new Vector2I(-1, 2);

    /// <summary>
    /// Clears the world grid and rebuilds it from scratch from the persisted display tiles. Called when
    /// the plugin is enabled so an existing (or externally-edited) tilemap gets a fresh world grid.
    /// Terrain round-trips exactly; void is recovered by erosion (see RebuildWorldFromDisplay).
    /// </summary>
    public void RebuildWorld()
    {
        ClearWorld();
        RebuildWorldFromDisplay();
    }

    // Rebuilds the world grid from the persisted display cells (inverse of Resolve, via TryGetMask).
    // Positive: each tile's mask maps to its Positive corner cells. Void: since void is now
    // corner-based, a world cell was Negative iff EVERY display cell it is a corner of
    // (c + AffectedDisplayOffsets) is an abyss tile -- an erosion of the abyss set. The abyss tile is
    // (0,0,0,0) and carries no corner data, so exact recovery isn't possible; erosion is non-growing
    // (it may drop a 1-cell void protrusion, or fill a sub-tile void hole, on reload). Inverse of
    // MaskAt: Nw=(d-1,d-1) Ne=(d,d-1) Sw=(d-1,d) Se=(d,d).
    private void RebuildWorldFromDisplay()
    {
        CadastileTileSet ts = TileSet;
        if (ts is null)
            return;

        var abyss = new System.Collections.Generic.HashSet<Vector2I>();

        // Pass 1: recover Positive cells from tile masks; collect the abyss (None) display cells.
        foreach (Vector2I d in GetUsedCells())
        {
            int sourceId = GetCellSourceId(d);
            if (sourceId < 0)
                continue;
            if (!ts.TryGetMask(sourceId, GetCellAtlasCoords(d), out CadasTileCorner mask))
                continue;

            if (mask == CadasTileCorner.None)
            {
                abyss.Add(d);
                continue;
            }

            if ((mask & CadasTileCorner.Nw) != 0) WriteCell(new Vector2I(d.X - 1, d.Y - 1), sourceId, WorldCellKind.Positive);
            if ((mask & CadasTileCorner.Ne) != 0) WriteCell(new Vector2I(d.X,     d.Y - 1), sourceId, WorldCellKind.Positive);
            if ((mask & CadasTileCorner.Sw) != 0) WriteCell(new Vector2I(d.X - 1, d.Y),     sourceId, WorldCellKind.Positive);
            if ((mask & CadasTileCorner.Se) != 0) WriteCell(new Vector2I(d.X,     d.Y),      sourceId, WorldCellKind.Positive);
        }

        // Pass 2: recover Negative (void) by eroding the abyss set.
        foreach (Vector2I c in abyss)
        {
            bool allAbyss = true;
            foreach (Vector2I off in AffectedDisplayOffsets)
                if (!abyss.Contains(c + off)) { allAbyss = false; break; }
            if (allAbyss)
                WriteCell(c, GetCellSourceId(c), WorldCellKind.Negative);
        }
    }

    private void RefreshAround(Vector2I worldCell)
    {
        foreach (Vector2I off in AffectedDisplayOffsets)
            ResolveDisplay(worldCell + off);
    }

    /// <summary>Re-resolves every display cell derived from the current world grid (e.g. after a source swap).</summary>
    public void RefreshAll()
    {
        foreach (Vector2I cell in WorldCells)
            RefreshAround(cell);
    }

    private void ResolveDisplay(Vector2I displayCell)
    {
        CadastileTileSet ts = TileSet;
        if (ts is null)
            return;

        CadasTileCorner mask = MaskAt(displayCell, null, out int ownerSource);
        if (mask != CadasTileCorner.None)
        {
            // Transition / full tile from the owner source (-1 = any source for a single-source layer).
            if (ts.Resolve(ownerSource, mask) is CadastileRef r)
                SetCell(displayCell, r.SourceId, r.AtlasCoords);
            else
                EraseCell(displayCell);
            return;
        }

        // None: no Positive corner. Void is corner-based (like terrain): if ANY corner is Negative,
        // draw that source's abyss ((0,0,0,0)) tile (any source for a single-source layer); else erase.
        if (TryVoidCorner(displayCell, null, out int voidSource) && ts.Resolve(voidSource, CadasTileCorner.None) is CadastileRef abyss)
            SetCell(displayCell, abyss.SourceId, abyss.AtlasCoords);
        else
            EraseCell(displayCell);
    }

    /// <summary>
    /// The (source, atlas) tile the display cell WOULD get under the hypothetical <paramref name="pending"/>
    /// edit map (world cell -> painted (source,kind), or null = erased). Pure -- no state changes. Used by
    /// the tool preview so the ghost matches the real result exactly. Null = the cell would be empty.
    /// </summary>
    public CadastileRef? PreviewResolve(Vector2I displayCell, PendingEdits pending)
    {
        CadastileTileSet ts = TileSet;
        if (ts is null)
            return null;

        CadasTileCorner mask = MaskAt(displayCell, pending, out int ownerSource);
        if (mask != CadasTileCorner.None)
            return ts.Resolve(ownerSource, mask);

        if (TryVoidCorner(displayCell, pending, out int voidSource))
            return ts.Resolve(voidSource, CadasTileCorner.None);

        return null;
    }

    /// <summary>The tile currently at a display cell as a CadastileRef, or null if the cell is empty.
    /// Lets the preview show only the cells that would actually change (added / re-tiled).</summary>
    public CadastileRef? CurrentRef(Vector2I displayCell)
    {
        int src = GetCellSourceId(displayCell);
        return src < 0 ? null : new CadastileRef(src, GetCellAtlasCoords(displayCell));
    }

    /// <summary>
    /// Hides the native "tile_set" slot inherited from TileMapLayer from the inspector and storage, so
    /// only the typed CadastileTileSet export above remains (single serialization path).
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
