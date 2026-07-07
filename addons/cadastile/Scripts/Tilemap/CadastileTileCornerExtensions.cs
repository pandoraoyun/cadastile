using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Cadastile.Tilemap;

/// <summary>
/// Conversions between <see cref="CadasTileCorner"/> masks and their representations (single-corner
/// layer names and the custom-data Vector4I). This class is the one place that encodes the bit
/// convention, so all mask reading/writing goes through here.
/// </summary>
public static class CadastileTileCornerExtensions
{
    /// <summary>Single-corner layer name ("nw"/"ne"/"sw"/"se"); throws for a non-single corner.</summary>
    public static string ToLayerName(this CadasTileCorner corner)
    {
        return corner switch
        {
            CadasTileCorner.Nw => "nw",
            CadasTileCorner.Ne => "ne",
            CadasTileCorner.Sw => "sw",
            CadasTileCorner.Se => "se",
            _ => throw new ArgumentException($"'{corner}' is not a valid single corner layer!")
        };
    }

    private static readonly CadasTileCorner[] SingleCorners = { CadasTileCorner.Nw, CadasTileCorner.Ne, CadasTileCorner.Sw, CadasTileCorner.Se };

    /// <summary>Fills <paramref name="results"/> with the single corners present in the mask (no allocation).</summary>
    public static void GetActiveCornersNonAlloc(this CadasTileCorner mask, List<CadasTileCorner> results)
    {
        results.Clear();
        for (int i = 0; i < SingleCorners.Length; i++)
        {
            if ((mask & SingleCorners[i]) != 0)
            {
                results.Add(SingleCorners[i]);
            }
        }
    }

    /// <summary>Enumerates the single corners present in the mask.</summary>
    public static IEnumerable<CadasTileCorner> GetActiveCorners(this CadasTileCorner mask)
    {
        // Nothing to yield for None / an empty mask.
        if (mask == CadasTileCorner.None) yield break;

        for (int i = 0; i < SingleCorners.Length; i++)
        {
            // Bitwise AND (&): is this corner part of the mask?
            if ((mask & SingleCorners[i]) != 0)
            {
                yield return SingleCorners[i];
            }
        }
    }

    /// <summary>The four single corners, in bit order.</summary>
    public static IEnumerable<CadasTileCorner> GetBaseCorners() => SingleCorners;

    /// <summary>The four single-corner layer names, in bit order.</summary>
    public static IEnumerable<string> GetCornerNames(this CadasTileCorner _)
    {
        return GetBaseCorners().Select(x => x.ToLayerName());
    }

    /// <summary>Reads a custom-data Vector4I (X:NW Y:NE Z:SW W:SE) into a mask; any non-zero component sets its corner.</summary>
    public static CadasTileCorner ToTileCorner(this Vector4I value)
    {
        CadasTileCorner corner = CadasTileCorner.None;

        if (value.X != 0) corner |= CadasTileCorner.Nw;
        if (value.Y != 0) corner |= CadasTileCorner.Ne;
        if (value.Z != 0) corner |= CadasTileCorner.Sw;
        if (value.W != 0) corner |= CadasTileCorner.Se;

        return corner;
    }

    /// <summary>Inverse of <see cref="ToTileCorner"/>: writes a mask into a custom-data Vector4I (X:NW Y:NE Z:SW W:SE).</summary>
    public static Vector4I ToVector4I(this CadasTileCorner corner)
    {
        return new Vector4I(
            (corner & CadasTileCorner.Nw) != 0 ? 1 : 0,
            (corner & CadasTileCorner.Ne) != 0 ? 1 : 0,
            (corner & CadasTileCorner.Sw) != 0 ? 1 : 0,
            (corner & CadasTileCorner.Se) != 0 ? 1 : 0);
    }
}
