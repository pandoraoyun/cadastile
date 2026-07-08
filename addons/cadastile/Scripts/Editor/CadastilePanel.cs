#if TOOLS
using System.Collections.Generic;
using Godot;

namespace Cadastile.Editor;

/// <summary>
/// The CadasTile bottom panel -- a view bound to the cursor. The tool bar shows every tool; the ACTIVE
/// tool is wrapped in a bordered group together with its brushes, while the other tools sit as plain
/// icon buttons pushed along. A brush is bound by clicking it: left = primary (green), right =
/// secondary (blue). Below the bar: the TileSet sources.
/// </summary>
[Tool]
public partial class CadastilePanel : Control
{
    /// <summary>The model this panel drives; set by the plugin before the panel enters the tree.</summary>
    public CadastileCursor Cursor { get; set; }

    private static readonly Color PrimaryAccent   = new(0.55f, 1.00f, 0.65f);
    private static readonly Color SecondaryAccent = new(0.55f, 0.78f, 1.00f);

    private readonly Dictionary<CadastileBrush, Button> _brushButtons = new();
    private HBoxContainer _toolRow;
    private Button _bakeButton;
    private GridContainer _sourceGrid;
    private CadastileGridLayer _layer;
    private CadastileTileSet _watchedTileSet;
    private readonly List<int> _sourceIds = new();

    private const int SourceColumns = 6;
    private static readonly Vector2 ThumbSize = new(96, 96);

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, 320);

        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 6);
        AddChild(root);

        _toolRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _toolRow.AddThemeConstantOverride("separation", 6);

        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", 6);
        topRow.AddChild(_toolRow);
        topRow.AddChild(BuildBakeButton());
        root.AddChild(topRow);

        root.AddChild(new HSeparator());
        root.AddChild(BuildBody());

        RebuildToolRow();
        Callable.From(FixToolRowHeight).CallDeferred();
    }

    public override void _ExitTree()
    {
        if (_watchedTileSet != null)
            _watchedTileSet.Changed -= OnTileSetChanged;
        _watchedTileSet = null;
    }

    // A "Bake" button: rebuilds the world grid from the layer's current tiles, so tiles painted with the
    // native TileMap editor get adopted. Disabled when no layer is selected.
    private Button BuildBakeButton()
    {
        _bakeButton = new Button
        {
            Text = "Bake",
            Icon = GetEditorIcon("Reload"),
            TooltipText = "Rebuild the world grid from the layer's current tiles",
            Disabled = true,
        };
        _bakeButton.Pressed += () => _layer?.RebuildWorld();
        return _bakeButton;
    }

    // The tool bar: "Tool:" + every tool. The active tool is grouped (bordered) with its brushes; the
    // others are plain icon buttons. Rebuilt whenever the active tool changes.
    private void RebuildToolRow()
    {
        if (_toolRow == null)
            return;

        foreach (Node child in _toolRow.GetChildren())
            child.QueueFree();
        _brushButtons.Clear();

        _toolRow.AddChild(new Label { Text = "Tool:" });

        var group = new ButtonGroup();
        foreach (CadastileTool tool in Cursor.Tools)
        {
            CadastileTool t = tool; // closure capture
            bool active = t == Cursor.ActiveTool;

            var box = new HBoxContainer();
            box.AddThemeConstantOverride("separation", 4);
            box.AddChild(MakeToolButton(t, group));
            if (active)
                foreach (CadastileBrush brush in t.Brushes)
                    box.AddChild(MakeBrushButton(t, brush));

            // Every tool sits in a same-sized box (border visible only for the active one), so the row
            // height never jumps when switching to a mode-less tool.
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", active ? ActiveToolBorder() : PhantomToolBorder());
            panel.AddChild(box);
            _toolRow.AddChild(panel);

            if (active && t.Brushes.Count > 0)
                RefreshBrushButtons(t);
        }
    }

    private Button MakeToolButton(CadastileTool tool, ButtonGroup group)
    {
        Texture2D icon = GetEditorIcon(tool.IconName);
        var button = new Button
        {
            Text = icon == null ? tool.Name : "",
            Icon = icon,
            TooltipText = tool.Name,
            ToggleMode = true,
            ButtonGroup = group,
            ButtonPressed = tool == Cursor.ActiveTool,
        };
        button.Pressed += () => SelectTool(tool);
        return button;
    }

    private Button MakeBrushButton(CadastileTool tool, CadastileBrush brush)
    {
        var button = new Button
        {
            Text = brush.Name,
            TooltipText = $"{brush.Name}  (left = primary, right = secondary)",
        };
        button.GuiInput += e => OnBrushButtonInput(e, tool, brush);
        _brushButtons[brush] = button;
        return button;
    }

    // Pins the tool row to a constant height (measured from a text button + the cell margins) so
    // switching to a mode-less tool -- which shows no brush buttons -- doesn't shrink the row. The
    // buttons fill this height (Control fills its container vertically by default).
    private void FixToolRowHeight()
    {
        if (_toolRow == null)
            return;

        var probe = new Button { Text = "Ay" };
        _toolRow.AddChild(probe);
        float h = probe.GetCombinedMinimumSize().Y;
        _toolRow.RemoveChild(probe);
        probe.Free();

        if (h < 1f)
            h = 28f * EditorInterface.Singleton.GetEditorScale();
        _toolRow.CustomMinimumSize = new Vector2(0, h + 10f); // + panel content margins + border
    }

    private void SelectTool(CadastileTool tool)
    {
        Cursor.ActiveTool = tool;
        Callable.From(RebuildToolRow).CallDeferred(); // relayout once the click is fully handled
    }

    private void OnBrushButtonInput(InputEvent @event, CadastileTool tool, CadastileBrush brush)
    {
        if (@event is not InputEventMouseButton { Pressed: true } mb)
            return;
        if (mb.ButtonIndex == MouseButton.Left)
            tool.PrimaryBrush = brush;
        else if (mb.ButtonIndex == MouseButton.Right)
            tool.SecondaryBrush = brush;
        else
            return;
        RefreshBrushButtons(tool);
    }

    // Tint each brush button by its role for the active tool: primary = green, secondary = blue, else neutral.
    private void RefreshBrushButtons(CadastileTool tool)
    {
        foreach ((CadastileBrush brush, Button button) in _brushButtons)
        {
            if (brush == tool.PrimaryBrush)
                button.Modulate = PrimaryAccent;
            else if (brush == tool.SecondaryBrush)
                button.Modulate = SecondaryAccent;
            else
                button.Modulate = Colors.White;
        }
    }

    // A subtle rounded border box wrapping the active tool + its brushes.
    private static StyleBoxFlat ActiveToolBorder()
    {
        var sb = new StyleBoxFlat
        {
            BgColor = new Color(1f, 1f, 1f, 0.04f),
            BorderColor = new Color(0.55f, 0.60f, 0.66f, 0.85f),
        };
        sb.SetBorderWidthAll(1);
        sb.SetCornerRadiusAll(4);
        sb.SetContentMarginAll(4);
        return sb;
    }

    // Same footprint as ActiveToolBorder but invisible -- keeps every tool cell the same height.
    private static StyleBoxFlat PhantomToolBorder()
    {
        var sb = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = Colors.Transparent };
        sb.SetBorderWidthAll(1);
        sb.SetCornerRadiusAll(4);
        sb.SetContentMarginAll(4);
        return sb;
    }

    // Looks up a Godot editor icon by name (EditorIcons); null if absent (button falls back to text).
    private static Texture2D GetEditorIcon(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        Theme theme = EditorInterface.Singleton.GetEditorTheme();
        return theme != null && theme.HasIcon(name, "EditorIcons") ? theme.GetIcon(name, "EditorIcons") : null;
    }

    // Body: the TileSet sources, full width (no header label).
    private Control BuildBody()
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _sourceGrid = new GridContainer { Columns = SourceColumns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sourceGrid.AddThemeConstantOverride("h_separation", 10);
        _sourceGrid.AddThemeConstantOverride("v_separation", 10);
        scroll.AddChild(_sourceGrid);
        return scroll;
    }

    /// <summary>
    /// Binds the panel to the active layer: watches its TileSet for source add/remove and (re)builds the
    /// selectable source grid. For a single-source layer, selecting a source re-skins the whole layer.
    /// </summary>
    public void SetActiveLayer(CadastileGridLayer layer)
    {
        _layer = layer;
        if (_bakeButton != null)
            _bakeButton.Disabled = layer == null;

        CadastileTileSet tileSet = layer?.TileSet;
        if (_watchedTileSet != tileSet)
        {
            if (_watchedTileSet != null)
                _watchedTileSet.Changed -= OnTileSetChanged;
            _watchedTileSet = tileSet;
            if (_watchedTileSet != null)
                _watchedTileSet.Changed += OnTileSetChanged;
        }

        BuildSources();
    }

    // The watched TileSet changed; rebuild the source grid only when the set of source ids actually
    // changed (a source added/removed) -- not on every tile edit, which would reset the selection.
    private void OnTileSetChanged()
    {
        var now = new List<int>();
        if (_watchedTileSet != null)
            foreach ((int id, TileSetAtlasSource _) in _watchedTileSet.GetAtlasSources())
                now.Add(id);

        if (now.Count != _sourceIds.Count || !SameIds(now, _sourceIds))
            BuildSources();
    }

    private static bool SameIds(List<int> a, List<int> b)
    {
        foreach (int x in a)
            if (!b.Contains(x))
                return false;
        return true;
    }

    // (Re)builds the source grid from the active layer's TileSet, preserving the current selection
    // (single-source: its ActiveSource; multi: the cursor's active source) when it is still valid.
    private void BuildSources()
    {
        if (_sourceGrid == null)
            return;

        foreach (Node child in _sourceGrid.GetChildren())
            child.QueueFree();

        _sourceIds.Clear();
        var sources = new List<(int id, TileSetAtlasSource atlas)>();
        if (_layer?.TileSet is CadastileTileSet tileSet)
            foreach ((int sourceId, TileSetAtlasSource atlas) in tileSet.GetAtlasSources())
            {
                sources.Add((sourceId, atlas));
                _sourceIds.Add(sourceId);
            }

        if (sources.Count == 0)
        {
            SelectSource(-1);
            return;
        }

        int current = _layer is SingleSourceCadastileGridLayer single ? single.ActiveSource : (Cursor?.ActiveSourceId ?? -1);
        int initial = sources.Exists(s => s.id == current) ? current : sources[0].id;

        var group = new ButtonGroup();
        foreach ((int id, TileSetAtlasSource atlas) in sources)
            _sourceGrid.AddChild(MakeSourceCard(id, atlas, group, id == initial));

        SelectSource(initial);
    }

    // A source card: padded, faintly-tinted panel with a selectable thumbnail + its name (ellipsized).
    private Control MakeSourceCard(int id, TileSetAtlasSource atlas, ButtonGroup group, bool pressed)
    {
        int sid = id; // closure capture
        var button = new Button
        {
            ToggleMode = true,
            ButtonGroup = group,
            CustomMinimumSize = ThumbSize,
            Icon = atlas.Texture,
            ExpandIcon = true,
            ButtonPressed = pressed,
        };
        button.Pressed += () => SelectSource(sid);

        string name = SourceName(atlas, id);
        var label = new Label
        {
            Text = name,
            TooltipText = name,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            CustomMinimumSize = new Vector2(ThumbSize.X, 0),
        };
        label.AddThemeFontSizeOverride("font_size", 10);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(button);
        box.AddChild(label);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", SourceCardStyle());
        card.AddChild(box);
        return card;
    }

    private static string SourceName(TileSetAtlasSource atlas, int id)
    {
        string path = atlas.Texture?.ResourcePath;
        return string.IsNullOrEmpty(path) ? $"source {id}" : System.IO.Path.GetFileNameWithoutExtension(path);
    }

    // A faint padded card behind each source.
    private static StyleBoxFlat SourceCardStyle()
    {
        var sb = new StyleBoxFlat { BgColor = new Color(1f, 1f, 1f, 0.05f) };
        sb.SetCornerRadiusAll(4);
        sb.SetContentMarginAll(6);
        return sb;
    }

    // Sets the active paint source: on the cursor (painting/preview) and, for a single-source layer, as
    // the whole layer's source (which re-skins its existing tiles).
    private void SelectSource(int sourceId)
    {
        if (Cursor != null)
            Cursor.ActiveSourceId = sourceId;
        if (_layer is SingleSourceCadastileGridLayer single)
            single.ActiveSource = sourceId;
    }
}
#endif
