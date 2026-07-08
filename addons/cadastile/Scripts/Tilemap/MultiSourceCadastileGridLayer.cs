using Godot;
using System.Collections.Generic;

namespace Cadastile.Tilemap;

/// <summary>
/// A multi-terrain dual-grid layer. Stores (source, kind) per cell so several sources can be painted
/// on one layer. Each terrain autotiles against everything-not-itself; at a seam between two sources
/// the SE-priority owner (effectively the last painted) wins the cell. Heavier than the single-source
/// layer, so use it only when a layer really needs multiple terrains.
/// </summary>
[Tool]
[GlobalClass]
public partial class MultiSourceCadastileGridLayer : CadastileGridLayer
{
    private readonly Dictionary<Vector2I, (int sourceId, WorldCellKind kind)> _world = new();

    protected override int WorldCount => _world.Count;

    protected override IEnumerable<Vector2I> WorldCells => _world.Keys;

    protected override bool TryReadCell(Vector2I cell, out int sourceId, out WorldCellKind kind)
    {
        if (_world.TryGetValue(cell, out var v))
        {
            sourceId = v.sourceId;
            kind = v.kind;
            return true;
        }
        sourceId = -1;
        kind = default;
        return false;
    }

    protected override void WriteCell(Vector2I cell, int sourceId, WorldCellKind kind)
        => _world[cell] = (sourceId, kind);

    protected override void RemoveCell(Vector2I cell) => _world.Remove(cell);

    protected override void ClearWorld() => _world.Clear();
}
