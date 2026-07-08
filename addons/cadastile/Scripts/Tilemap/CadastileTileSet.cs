using Godot;
using Godot.Collections;

namespace Cadastile.Tilemap;

/// <summary>
/// Addresses dual-grid tiles not by their atlas position but by the corner-fill combination
/// (<see cref="CadasTileCorner"/>) they represent. Every atlas tile carries a single Vector4I
/// custom-data layer (X:NW Y:NE Z:SW W:SE); the corner data is stored per source in a
/// <see cref="CadastileCoords"/> resource, keyed by atlas SOURCE ID (not enumeration order, so adding,
/// removing or reordering sources can never misalign the mapping).
///
/// Two-way sync (editor-only):
///  - Paint -> resource: TileSet.Changed -> HandleChanged reads the custom-data into CadastileCoords.
///  - Resource -> paint: when CadastileCoords changes (inspector/load), it is written back to custom-data.
/// A reconcile step keeps one coords resource per current source. All guarded by <c>_syncing</c>.
/// </summary>
[Tool]
[GlobalClass]
public partial class CadastileTileSet : TileSet
{
    /// <summary>Corner tags keyed by atlas source id.</summary>
    private Dictionary<int, CadastileCoords> _tileCoords = [];

    /// <summary>Per-source corner-tag resources, keyed by source id. Auto-synced with the atlas sources.</summary>
    [Export]
    public Dictionary<int, CadastileCoords> TileCoords
    {
        get => _tileCoords;
        set
        {
            #if TOOLS
            UnsubscribeCoords(_tileCoords);
            #endif
            _tileCoords = value ?? [];
            #if TOOLS
            SubscribeCoords(_tileCoords);
            // Only reflect immediately once init has finished (runtime/inspector reassignment).
            // During load _ready is false; DeferredInit does the first write-back.
            if (_ready)
                WriteBackFromResources();
            #endif
        }
    }

    [Export] public byte CornerCustomDataIndex; // unused for now; kept in case we want to cache the layer index.

    /// <summary>
    /// Mask -> atlas tile (reverse direction). If <paramref name="sourceId"/> is >= 0 the search is
    /// scoped to that single source; if it is < 0 the first match across ALL sources (in enumeration
    /// order) is returned (single-source layers that don't track a per-cell source). Cache-free linear
    /// scan for small n (~16/source). Null if not found (or, when scoped, if that source lacks the mask).
    /// </summary>
    public CadastileRef? Resolve(int sourceId, CadasTileCorner mask)
    {
        if (sourceId >= 0)
            return ResolveIn(sourceId, mask);

        foreach ((int sid, TileSetAtlasSource _) in this.GetAtlasSources())
            if (ResolveIn(sid, mask) is CadastileRef r)
                return r;
        return null;
    }

    private CadastileRef? ResolveIn(int sourceId, CadasTileCorner mask)
    {
        if (!_tileCoords.TryGetValue(sourceId, out CadastileCoords coords) || coords is null)
            return null;

        foreach (var kv in coords.CornerTags)
            if (kv.Value == mask)
                return new CadastileRef(sourceId, kv.Key);
        return null;
    }

    /// <summary>
    /// Inverse of Resolve: returns the mask of a display tile (sourceId + atlas). Used to rebuild
    /// the (non-serialized) world grid from the persisted display on load. False if untagged.
    /// </summary>
    public bool TryGetMask(int sourceId, Vector2I atlasCoords, out CadasTileCorner mask)
    {
        if (_tileCoords.TryGetValue(sourceId, out CadastileCoords coords) && coords is not null)
            return coords.TryGetCorner(atlasCoords, out mask);

        mask = CadasTileCorner.None;
        return false;
    }

    // Editor-only: the TileSet subscribes to its own Changed (not tied to a layer, no shared state).
    #if TOOLS
    public CadastileTileSet()
    {
        Changed += HandleChanged;
        // Wait until load/deserialize is done, then reconcile + write resource -> custom-data.
        Callable.From(DeferredInit).CallDeferred();
    }
    #endif


    #if TOOLS

    private const string CustomDataLayerName = "Corners (X:NW | Y:NE | Z:SW | W:SE)";

    // Prevents the two sync directions (and reconcile) from triggering each other.
    private bool _syncing;

    // Silences HandleChanged until load/deserialize is complete (wipe protection).
    private bool _ready;

    // Deferred: runs once load finishes. Reconcile the per-source boxes, then resource -> custom-data,
    // then _ready = true -> from here on the paint -> resource direction is open.
    private void DeferredInit()
    {
        _ready = true; // open the paint->resource direction first, so a hiccup below can't disable it
        _syncing = true;
        try
        {
            ReconcileSources();
            ForEachTaggedTile(PushResourceToTile);
        }
        finally
        {
            _syncing = false;
        }
    }

    // Keeps exactly one CadastileCoords per current atlas source: creates a box for a new source (so it
    // shows up in the inspector) and drops the entry of a removed source. Keyed by id, so it survives
    // reordering. Caller holds the _syncing guard.
    private void ReconcileSources()
    {
        var present = new System.Collections.Generic.HashSet<int>();

        foreach ((int sourceId, TileSetAtlasSource _) in this.GetAtlasSources())
        {
            present.Add(sourceId);
            if (_tileCoords.TryGetValue(sourceId, out CadastileCoords existing) && existing is not null)
                continue;

            var coords = new CadastileCoords();
            coords.TileCornerChangedFromInspector += WriteBackFromResources;
            _tileCoords[sourceId] = coords;
        }

        // Collect stale keys (source removed) by enumerating the dict directly (no .Keys), then remove.
        var stale = new System.Collections.Generic.List<int>();
        foreach (System.Collections.Generic.KeyValuePair<int, CadastileCoords> kv in _tileCoords)
            if (!present.Contains(kv.Key))
                stale.Add(kv.Key);

        foreach (int key in stale)
        {
            if (_tileCoords.TryGetValue(key, out CadastileCoords c) && c is not null)
                c.TileCornerChangedFromInspector -= WriteBackFromResources;
            _tileCoords.Remove(key);
        }
    }

    private void SubscribeCoords(Dictionary<int, CadastileCoords> map)
    {
        foreach (System.Collections.Generic.KeyValuePair<int, CadastileCoords> kv in map)
        {
            if (kv.Value is null)
                continue;
            // idempotent: unsubscribe first, then subscribe -> always exactly one subscription
            kv.Value.TileCornerChangedFromInspector -= WriteBackFromResources;
            kv.Value.TileCornerChangedFromInspector += WriteBackFromResources;
        }
    }

    private void UnsubscribeCoords(Dictionary<int, CadastileCoords> map)
    {
        foreach (System.Collections.Generic.KeyValuePair<int, CadastileCoords> kv in map)
        {
            if (kv.Value is null)
                continue;
            kv.Value.TileCornerChangedFromInspector -= WriteBackFromResources;
        }
    }

    /// <summary>Ensures the corner custom-data layer exists (Vector4I), adding it if missing.</summary>
    public void EnsureCornerLayers()
    {
        if (GetCustomDataLayerByName(CustomDataLayerName) != -1)
            return;

        int index = GetCustomDataLayersCount();
        AddCustomDataLayer(); // append at the end (-1 default)
        SetCustomDataLayerName(index, CustomDataLayerName);
        SetCustomDataLayerType(index, Variant.Type.Vector4I);
    }

    /// <summary>A per-tile sync action, given that tile's coords/tileData/layerId.</summary>
    private delegate void TileAction(CadastileCoords coords, TileData tileData, Vector2I atlasCoords, int layerId);

    /// <summary>
    /// Ensures the corner custom-data layer, then invokes <paramref name="action"/> for every tile of
    /// each source's CadastileCoords (looked up by source id). Both sync directions share this iteration.
    /// </summary>
    private void ForEachTaggedTile(TileAction action)
    {
        EnsureCornerLayers();

        int layerId = GetCustomDataLayerByName(CustomDataLayerName);
        if (layerId < 0)
            return;

        foreach ((int sourceId, TileSetAtlasSource atlas) in this.GetAtlasSources())
        {
            if (!_tileCoords.TryGetValue(sourceId, out CadastileCoords coords) || coords is null)
                continue;

            foreach ((Vector2I coord, TileData data) in atlas.GetTiles())
                action(coords, data, coord, layerId);
        }
    }

    /// <summary>Paint -> resource: reads each tile's Vector4I custom-data into CadastileCoords.</summary>
    private void HandleChanged()
    {
        if (_syncing || !_ready)
            return;

        _syncing = true;
        try
        {
            ReconcileSources();
            ForEachTaggedTile(PullTileToResource);
        }
        finally
        {
            _syncing = false;
        }
    }

    private static void PullTileToResource(CadastileCoords coords, TileData tileData, Vector2I atlasCoords, int layerId)
    {
        // Push all, same or not: UpdateCorner now always writes (including None), so the (0,0,0,0)
        // abyss tile also lands in CornerTags. We do not keep the equality guard.
        CadasTileCorner corner = tileData.GetCustomDataByLayerId(layerId).AsVector4I().ToTileCorner();
        coords.UpdateCorner(atlasCoords, corner);
    }

    /// <summary>Resource -> paint: writes the CadastileCoords mask back into the tile's Vector4I custom-data.</summary>
    private void WriteBackFromResources()
    {
        if (_syncing)
            return;

        _syncing = true;
        try
        {
            ForEachTaggedTile(PushResourceToTile);
        }
        finally
        {
            _syncing = false;
        }
    }

    private static void PushResourceToTile(CadastileCoords coords, TileData tileData, Vector2I atlasCoords, int layerId)
    {
        CadasTileCorner corner = coords.TryGetCorner(atlasCoords, out CadasTileCorner c) ? c : CadasTileCorner.None;
        Vector4I want = corner.ToVector4I();
        if (tileData.GetCustomDataByLayerId(layerId).AsVector4I() != want)
            tileData.SetCustomDataByLayerId(layerId, want);
    }
#endif
}
