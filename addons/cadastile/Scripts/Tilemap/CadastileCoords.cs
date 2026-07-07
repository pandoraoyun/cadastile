using Godot;
using Godot.Collections;

namespace Cadastile.Tilemap;

/// <summary>
/// Per-source corner tags for a <see cref="CadastileTileSet"/>: maps an atlas coordinate to the
/// <see cref="CadasTileCorner"/> mask that tile represents. Stored as a serialized resource so the
/// mask assignment survives reloads; the tile-set keeps it in sync with each tile's custom-data.
/// </summary>
[Tool]
[GlobalClass]
public sealed partial class CadastileCoords : Resource
{
    private Dictionary<Vector2I, CadasTileCorner> _cornerTags = [];

    /// <summary>Atlas coordinate -> corner mask for a single source.</summary>
    [Export]
    public Dictionary<Vector2I, CadasTileCorner> CornerTags
    {
        get => _cornerTags;
        private set
        {
            _cornerTags = value;
            // The setter only runs on an inspector edit or a .tres load (UpdateCorner mutates in
            // place and does NOT trigger the setter).
#if TOOLS
            EmitSignalTileCornerChangedFromInspector();
#endif
        }
    }

    /// <summary>Fired when a single corner tag changes (via <see cref="UpdateCorner"/>).</summary>
    [Signal] public delegate void TileCornerChangedEventHandler(Vector2I coords, CadasTileCorner oldValue, CadasTileCorner newValue);

    /// <summary>Fired when the whole dictionary is replaced from the inspector or a .tres load.</summary>
    [Signal] public delegate void TileCornerChangedFromInspectorEventHandler();

    /// <summary>Gets the mask tagged for <paramref name="coords"/>; false if untagged.</summary>
    public bool TryGetCorner(Vector2I coords, out CadasTileCorner corner)
    {
        return CornerTags.TryGetValue(coords, out corner);
    }

    /// <summary>Tags <paramref name="coords"/> with <paramref name="corner"/>.</summary>
    public void UpdateCorner(Vector2I coords, CadasTileCorner corner)
    {
        bool had = CornerTags.TryGetValue(coords, out var oldValue);

        // Always write -- including None (abyss) and an unchanged value. This is what lets a
        // (0,0,0,0) tagged tile land in CornerTags so Resolve(None) can find it.
        CornerTags[coords] = corner;

        // Only emit on a real change (new entry or different value); re-firing for an unchanged
        // value on every TileSet.Changed would just be noise.
        if (!had || oldValue != corner)
            EmitSignalTileCornerChanged(coords, had ? oldValue : CadasTileCorner.None, corner);
    }
}
