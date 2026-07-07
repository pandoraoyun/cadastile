#if TOOLS
using Godot;

namespace Cadastile.Editor;

/// <summary>
/// Watches the bottom-panel tabs: reports whether the CadasTile tab is active and catches the native
/// TileMap tab coming to the front. Encapsulates access to the editor's bottom TabContainer so the
/// plugin doesn't deal with those details.
/// </summary>
public sealed class CadastileTabWatcher
{
    private const string NativeTileMapTabTitle = "TileMap";
    private const string CadasTileTabTitle = "CadasTile";

    private readonly TabContainer _tabs;

    private CadastileTabWatcher(TabContainer tabs) => _tabs = tabs;

    /// <summary>Finds the bottom TabContainer by walking the panel's parent chain. Null if not found.</summary>
    public static CadastileTabWatcher TryCreate(Node panel)
    {
        Node n = panel?.GetParent();
        while (n != null && n is not TabContainer)
            n = n.GetParent();
        return n is TabContainer tc ? new CadastileTabWatcher(tc) : null;
    }

    /// <summary>Connects a handler to the tab_changed signal.</summary>
    public void Connect(TabContainer.TabChangedEventHandler handler)
        => _tabs.TabChanged += handler;

    /// <summary>Disconnects a previously connected tab_changed handler.</summary>
    public void Disconnect(TabContainer.TabChangedEventHandler handler)
    {
        if (GodotObject.IsInstanceValid(_tabs))
            _tabs.TabChanged -= handler;
    }

    /// <summary>Is the CadasTile tab currently at the front?</summary>
    public bool IsCadasTileActive() => ActiveTitle() == CadasTileTabTitle;

    /// <summary>Is the native TileMap tab currently at the front?</summary>
    public bool IsNativeTileMapActive() => ActiveTitle() == NativeTileMapTabTitle;

    private string ActiveTitle()
    {
        if (!GodotObject.IsInstanceValid(_tabs)) return null;
        int idx = _tabs.CurrentTab;
        if (idx < 0 || idx >= _tabs.GetTabCount()) return null;
        return _tabs.GetTabTitle(idx);
    }
}
#endif
