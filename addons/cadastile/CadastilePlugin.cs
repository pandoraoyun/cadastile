#if TOOLS

using Cadastile.Editor;
using Godot;

namespace Cadastile;

/// <summary>
/// The CadasTile editor plugin. Adds the bottom panel, tracks the selected CadastileGridLayer, and
/// forwards viewport input/drawing to the cursor. It only coordinates -- input lives in the cursor,
/// drawing in the cursor's overlay, tab tracking in the tab watcher, and UI in the panel.
/// </summary>
[Tool]
public partial class CadastilePlugin : EditorPlugin
{
    private CadastilePanel _panel;
    private CadastileTabWatcher _tabs;
    private readonly CadastileCursor _cursor = new();

    private CadastileGridLayer _activeLayer;

    // Connect and Disconnect must use the SAME Callable; every Callable.From(...) makes a new
    // delegate wrapper and the disconnect won't match ("nonexistent connection" + dangling handle).
    private Callable _onSelectionChanged;
    private bool _tabsConnected;

    public override void _EnterTree()
    {
        _panel = new CadastilePanel();
        #pragma warning disable CS0618 // Type or member is obsolete
        AddControlToBottomPanel(_panel, "CadasTile");
        #pragma warning restore CS0618 // Type or member is obsolete
        // NOTE: no _panel.Hide() -- AddControlToBottomPanel already adds it hidden; a manual Hide()
        // corrupted the bottom panel's visibility state and blocked the full-width layout on show.
        _panel.ActiveToolChanged += tool => _cursor.ActiveTool = tool;
        _cursor.ActiveTool = _panel.ActiveTool; // initial sync: the panel picked the default in _Ready

        _onSelectionChanged = Callable.From(OnSelectionChanged);
        EditorInterface.Singleton.GetSelection().Connect(
            EditorSelection.SignalName.SelectionChanged,
            _onSelectionChanged);

        CallDeferred(nameof(InitTabWatcher));
    }

    public override void _ExitTree()
    {
        EditorSelection selection = EditorInterface.Singleton.GetSelection();
        if (selection.IsConnected(EditorSelection.SignalName.SelectionChanged, _onSelectionChanged))
            selection.Disconnect(EditorSelection.SignalName.SelectionChanged, _onSelectionChanged);

        if (_tabsConnected)
        {
            _tabs?.Disconnect(OnBottomTabChanged);
            _tabsConnected = false;
        }
        _tabs = null;

        if (_panel != null)
        {
            #pragma warning disable CS0618 // Godot 4.7 suggests RemoveDock; kept for now so the bottom-panel flow (TabWatcher) still works.
            RemoveControlFromBottomPanel(_panel);
            #pragma warning restore CS0618
            _panel.QueueFree();
            _panel = null;
        }
    }

    // Find the bottom TabContainer, connect to its signal, sync the initial state.
    private void InitTabWatcher()
    {
        _tabs = CadastileTabWatcher.TryCreate(_panel);
        if (_tabs == null)
        {
            GD.PushWarning("[CadasTile] Bottom TabContainer not found.");
            return;
        }
        _tabs.Connect(OnBottomTabChanged);
        _tabsConnected = true;

        // First load: the selected node may already be a CadastileGridLayer -> sync manually.
        OnSelectionChanged();
    }

    private void OnBottomTabChanged(long tabIndex) => ForceCadasTileTabIfNative();

    // If the native TileMap tab came to the front and our layer is selected -> switch to CadasTile.
    private void ForceCadasTileTabIfNative()
    {
        if (_activeLayer == null || _tabs == null) return;
        if (_tabs.IsNativeTileMapActive())
            MakeBottomPanelItemVisible(_panel);
    }

    private void OnSelectionChanged()
    {
        var nodes = EditorInterface.Singleton.GetSelection().GetSelectedNodes();
        _activeLayer = nodes.Count > 0 && nodes[0] is CadastileGridLayer layer ? layer : null;

        // Feed the panel's source grid with the active layer's TileSet (clear it if none).
        _panel?.SetTileSet(_activeLayer?.TileSet);

        if (_activeLayer != null)
        {
            MakeBottomPanelItemVisible(_panel);
            ForceCadasTileTabIfNative();
        }

        _cursor.ResetCursor();
        UpdateOverlays();
    }

    // --- Viewport integration: input delegated to the cursor, drawing to its overlay ---

    public override bool _Handles(GodotObject @object) => @object is CadastileGridLayer;

    public override bool _ForwardCanvasGuiInput(InputEvent @event)
    {
        if (_activeLayer == null) return false;

        var (handled, changed) = _cursor.HandleInput(@event, _activeLayer);
        if (changed) UpdateOverlays();
        return handled;
    }

    public override void _ForwardCanvasDrawOverViewport(Control overlay)
    {
        if (_activeLayer == null || _activeLayer.TileSet == null) return;
        if (_tabs == null || !_tabs.IsCadasTileActive()) return;

        _cursor.Draw(overlay, _activeLayer);
    }
}
#endif
