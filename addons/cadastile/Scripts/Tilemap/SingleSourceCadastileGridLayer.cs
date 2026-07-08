#if TOOLS
using Godot;
using System.Collections.Generic;

namespace Cadastile.Tilemap;

/// <summary>
/// A single-terrain dual-grid layer. Stores only fill/empty per cell (no per-cell source); the whole
/// layer renders and paints with ONE source, chosen in the panel. Changing that source re-skins every
/// painted cell with the new source's tiles. Lighter than the multi-source layer.
/// </summary>
[Tool]
[GlobalClass]
public partial class SingleSourceCadastileGridLayer : CadastileGridLayer
{
    private readonly Dictionary<Vector2I, WorldCellKind> _world = new();

    // The one source this whole layer renders/paints with (-1 = any). Setting it re-skins all cells.
    private int _source = -1;

    /// <summary>The source used for the entire layer. Setting it re-resolves every cell with that source.</summary>
    public int ActiveSource
    {
        get => _source;
        set
        {
            if (_source == value)
                return;
            _source = value;
            RefreshAll();
        }
    }

    protected override int WorldCount => _world.Count;

    protected override IEnumerable<Vector2I> WorldCells => _world.Keys;

    protected override bool TryReadCell(Vector2I cell, out int sourceId, out WorldCellKind kind)
    {
        sourceId = _source; // one source for the whole layer
        return _world.TryGetValue(cell, out kind);
    }

    protected override void WriteCell(Vector2I cell, int sourceId, WorldCellKind kind)
        => _world[cell] = kind; // per-cell source not stored; the layer has one source

    protected override void RemoveCell(Vector2I cell) => _world.Remove(cell);

    protected override void ClearWorld() => _world.Clear();

    // The layer paints with its one source, so a preview paint uses it too.
    protected override int StoredSource(int sourceId) => _source;

    public override void _Ready()
    {
        // Adopt the source the existing tiles use (before the base rebuild), so a reload keeps the skin.
        foreach (Vector2I d in GetUsedCells())
        {
            _source = GetCellSourceId(d);
            break;
        }
        base._Ready();
    }
}
#endif
