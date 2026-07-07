using System;

namespace Cadastile.Tilemap;

/// <summary>
/// A flag set describing the four-corner fill combination. Each corner is a single bit: bit set =
/// that corner is filled, bit unset = empty. Four bits give 16 combinations; all are represented
/// here, including <see cref="None"/> (0).
///
/// The bit assignment is the SINGLE source of truth for the convention: both atlas reading and
/// world reading must go through <see cref="CadastileTileCornerExtensions"/> using this order.
/// </summary>
[Flags]
public enum CadasTileCorner
{
    None = 0,
    Nw = 1 << 0, // 1
    Ne = 1 << 1, // 2
    Sw = 1 << 2, // 4
    Se = 1 << 3, // 8
    All = Nw | Ne | Sw | Se, // 15
}
