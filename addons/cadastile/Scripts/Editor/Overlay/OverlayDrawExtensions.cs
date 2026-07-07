#if TOOLS
using Godot;

namespace Cadastile.Editor.Overlay;

/// <summary>
/// Viewport overlay draw helpers. Takes a layer-local cell rectangle (topLeft + size) and maps it to
/// screen space via <paramref name="xform"/>. Shared by the cursor and the tools; global-used, so
/// reachable everywhere.
/// </summary>
internal static class OverlayDrawExtensions
{
    // Maps a layer-local rectangle's 4 corners to screen space (clockwise: a=TL b=TR c=BR d=BL).
    private static (Vector2 a, Vector2 b, Vector2 c, Vector2 d) Corners(
        Transform2D xform, Vector2 topLeft, Vector2 size)
    {
        Vector2 a = xform * topLeft;
        Vector2 b = xform * (topLeft + new Vector2(size.X, 0));
        Vector2 c = xform * (topLeft + size);
        Vector2 d = xform * (topLeft + new Vector2(0, size.Y));
        return (a, b, c, d);
    }

    /// <summary>Draws the outline of a layer-local cell rectangle.</summary>
    public static void DrawCellOutline(this Control overlay, Transform2D xform,
                                       Vector2 topLeft, Vector2 size, Color color, float width)
    {
        var (a, b, c, d) = Corners(xform, topLeft, size);
        overlay.DrawLine(a, b, color, width);
        overlay.DrawLine(b, c, color, width);
        overlay.DrawLine(c, d, color, width);
        overlay.DrawLine(d, a, color, width);
    }

    /// <summary>Fills a layer-local cell rectangle.</summary>
    public static void DrawCellFill(this Control overlay, Transform2D xform,
                                    Vector2 topLeft, Vector2 size, Color color)
    {
        var (a, b, c, d) = Corners(xform, topLeft, size);
        overlay.DrawColoredPolygon(new[] { a, b, c, d }, color);
    }
}
#endif
