using Godot;
using System;
using System.Linq;

namespace Cadastile.Tilemap;

/// <summary>
/// Addresses dual-grid tiles not by their atlas position but by the corner-fill combination
/// (<see cref="CadasTileCorner"/>) they represent. Every atlas tile carries a single Vector4I
/// custom-data layer (X:NW Y:NE Z:SW W:SE); the corner data is stored per source in a
/// <see cref="CadastileCoords"/> resource.
///
/// Two-way sync (editor-only):
///  - Paint -> resource: TileSet.Changed -> HandleChanged reads the custom-data into CadastileCoords.
///  - Resource -> paint: when CadastileCoords changes (inspector/load), it is written back to custom-data.
/// Both directions are guarded by <c>_syncing</c> to break the feedback loop.
/// </summary>
[Tool]
[GlobalClass]
public partial class CadastileTileSet : TileSet
{
    /// <summary>Corner tags per source. Index = source enumeration order (GetSourceId(i)).</summary>
    private CadastileCoords[] _tileCoords = Array.Empty<CadastileCoords>();

    /// <summary>Per-source corner-tag resources, positionally matched to the atlas sources.</summary>
    [Export]
    public CadastileCoords[] TileCoords
    {
        get => _tileCoords;
        set
        {
            #if TOOLS
            UnsubscribeCoords(_tileCoords);
            #endif
            _tileCoords = value ?? Array.Empty<CadastileCoords>();
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
    /// Mask -> atlas tile (reverse direction). CornerTags stores coord -> mask; this scans the
    /// inverse and returns the first matching tile. Cache-free linear scan for small n (~16/source);
    /// a runtime hot path may need a cache. Returns null if not found.
    /// </summary>
    public CadastileRef? Resolve(CadasTileCorner mask)
    {
        foreach ((CadastileCoords coords, int sourceId) in
                 _tileCoords.Zip(this.GetAtlasSources(), (c, s) => (c, s.sourceId)))
        {
            if (coords is null)
                continue;

            foreach (var kv in coords.CornerTags)
                if (kv.Value == mask)
                    return new CadastileRef(sourceId, kv.Key);
        }
        return null;
    }

    /// <summary>
    /// Inverse of Resolve: returns the mask of a display tile (sourceId + atlas). Used to rebuild
    /// the (non-serialized) world grid from the persisted display on load. False if untagged.
    /// </summary>
    public bool TryGetMask(int sourceId, Vector2I atlasCoords, out CadasTileCorner mask)
    {
        foreach ((CadastileCoords coords, int sid) in
                 _tileCoords.Zip(this.GetAtlasSources(), (c, s) => (c, s.sourceId)))
        {
            if (coords is null || sid != sourceId)
                continue;
            return coords.TryGetCorner(atlasCoords, out mask);
        }
        mask = CadasTileCorner.None;
        return false;
    }

    // Editor-only: the TileSet subscribes to its own Changed (not tied to a layer, no shared state).
    #if TOOLS
    public CadastileTileSet()
    {
        Changed += HandleChanged;
        // Wait until load/deserialize is done, then write resource -> custom-data and start listening.
        Callable.From(DeferredInit).CallDeferred();
    }

    #endif


    #if TOOLS

    private const string CustomDataLayerName = "Corners (X:NW | Y:NE | Z:SW | W:SE)";

    // Prevents the two sync directions from triggering each other (reentrancy / loop guard).
    private bool _syncing;

    // Silences HandleChanged until load/deserialize is complete (wipe protection).
    private bool _ready;

    // Deferred: runs once load finishes. First resource -> custom-data (resource is the source),
    // then _ready = true -> from here on the paint -> resource direction is open.
    private void DeferredInit()
    {
        WriteBackFromResources();
        _ready = true;
    }

    private void SubscribeCoords(CadastileCoords[] arr)
    {
        foreach (CadastileCoords c in arr)
        {
            if (c is null)
                continue;
            // idempotent: unsubscribe first, then subscribe -> always exactly one subscription
            c.TileCornerChangedFromInspector -= WriteBackFromResources;
            c.TileCornerChangedFromInspector += WriteBackFromResources;
        }
    }

    private void UnsubscribeCoords(CadastileCoords[] arr)
    {
        foreach (CadastileCoords c in arr)
        {
            if (c is null)
                continue;
            c.TileCornerChangedFromInspector -= WriteBackFromResources;
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
    /// each source's matching CadastileCoords (index = source order). Both sync directions
    /// (pull/push) share this single iteration.
    /// </summary>
    private void ForEachTaggedTile(TileAction action)
    {
        EnsureCornerLayers();

        int layerId = GetCustomDataLayerByName(CustomDataLayerName);
        if (layerId < 0)
            return;

        // _tileCoords[s] <-> source[s] positional match. Zip stops when either ends
        // (the natural equivalent of the old 's >= all.Length' break).
        foreach ((CadastileCoords coords, TileSetAtlasSource atlas) in
                 _tileCoords.Zip(this.GetAtlasSources(), (c, s) => (c, s.atlas)))
        {
            if (coords is null)
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
