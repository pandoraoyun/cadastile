using Godot;

namespace Cadastile.Tilemap;

/// <summary>Which source's atlas tile a given corner mask maps to.</summary>
public readonly struct CadastileRef
{
    /// <summary>The TileSet source id.</summary>
    public readonly int SourceId;

    /// <summary>The atlas coordinates of the tile within that source.</summary>
    public readonly Vector2I AtlasCoords;

    public CadastileRef(int sourceId, Vector2I atlasCoords)
    {
        SourceId = sourceId;
        AtlasCoords = atlasCoords;
    }
}
