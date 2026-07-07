using System.Collections.Generic;
using Godot;

namespace Cadastile.Tilemap;

/// <summary>
/// Helpers that turn TileSet/atlas iteration into index-free foreach loops.
/// NOTE: yield/LINQ allocates an iterator on every call. Fine for editor authoring; on a runtime
/// hot path (per-frame resolution) the allocations can add up, so be careful there.
/// </summary>
public static class CadastileTileSetEnumerableExtensions
{
    /// <summary>Yields every atlas source (sourceId + atlas) in enumeration order.</summary>
    public static IEnumerable<(int sourceId, TileSetAtlasSource atlas)> GetAtlasSources(this CadastileTileSet tileSet)
    {
        for (int s = 0; s < tileSet.GetSourceCount(); s++)
        {
            int sourceId = tileSet.GetSourceId(s);
            if (tileSet.GetSource(sourceId) is TileSetAtlasSource atlas)
                yield return (sourceId, atlas);
        }
    }

    /// <summary>Yields every tile (coord + TileData) in the atlas; tiles with null TileData are skipped.</summary>
    public static IEnumerable<(Vector2I coord, TileData data)> GetTiles(this TileSetAtlasSource atlas)
    {
        for (int i = 0; i < atlas.GetTilesCount(); i++)
        {
            Vector2I coord = atlas.GetTileId(i);
            TileData data = atlas.GetTileData(coord, 0);
            if (data is not null)
                yield return (coord, data);
        }
    }
}
