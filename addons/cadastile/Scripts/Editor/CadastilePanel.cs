#if TOOLS
using System;
using System.Collections.Generic;
using Godot;

namespace Cadastile.Editor;

/// <summary>
/// The CadasTile bottom panel. Top: tool selection (radio) plus the selected tool's modes (radio, if
/// any). Left: the TileSet sources as a thumbnail grid. Right: a tile list (placeholder). Left click
/// = selected tool, right click = always erase. The panel owns the tool list and renders from it.
/// </summary>
[Tool]
public partial class CadastilePanel : Control
{
    /// <summary>Fired when the active (left-click) tool changes; the plugin binds it to the cursor.</summary>
    public event Action<CadastileTool> ActiveToolChanged;

    // The panel owns the tool list and renders the tool bar from it.
    private readonly List<CadastileTool> _tools = new()
    {
        new NoneTool(),
        new DrawTool(),
        new EraseTool(),
    };

    private const int DefaultToolIndex = 1; // Draw

    /// <summary>The currently selected (left-click) tool.</summary>
    public CadastileTool ActiveTool { get; private set; }

    private HBoxContainer _modeRow;
    private GridContainer _sourceGrid;

    private const int SourceColumns = 4;
    private static readonly Vector2 ThumbSize = new(72, 72);

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, 320);

        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 6);
        AddChild(root);

        root.AddChild(BuildToolRow());

        _modeRow = new HBoxContainer();
        _modeRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(_modeRow);

        root.AddChild(new HSeparator());
        root.AddChild(BuildBody());

        // Select the default tool (this also builds the mode row).
        SelectTool(_tools[DefaultToolIndex]);
    }

    // Top row: tools as radio buttons (ButtonGroup). Selecting one makes it the ActiveTool.
    private HBoxContainer BuildToolRow()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = "Tool:" });

        var group = new ButtonGroup();
        foreach (CadastileTool tool in _tools)
        {
            var button = new Button
            {
                Text = tool.Name,
                ToggleMode = true,
                ButtonGroup = group,
                ButtonPressed = tool == _tools[DefaultToolIndex],
            };
            button.Pressed += () => SelectTool(tool);
            row.AddChild(button);
        }
        return row;
    }

    // Applies the selected tool: set ActiveTool + fire the event + rebuild the mode row.
    private void SelectTool(CadastileTool tool)
    {
        ActiveTool = tool;
        ActiveToolChanged?.Invoke(tool);
        RebuildModeRow(tool);
    }

    // Shows the selected tool's modes as radio buttons; hides the row if the tool has no modes.
    private void RebuildModeRow(CadastileTool tool)
    {
        foreach (Node child in _modeRow.GetChildren())
            child.QueueFree();

        _modeRow.Visible = tool.Modes.Length > 0;
        if (!_modeRow.Visible)
            return;

        _modeRow.AddChild(new Label { Text = "Mode:" });

        var group = new ButtonGroup();
        for (int i = 0; i < tool.Modes.Length; i++)
        {
            int index = i; // closure capture
            var button = new Button
            {
                Text = tool.Modes[i],
                ToggleMode = true,
                ButtonGroup = group,
                ButtonPressed = i == tool.SelectedMode,
            };
            button.Pressed += () => tool.SelectedMode = index;
            _modeRow.AddChild(button);
        }
    }

    // Body: left source grid (scroll) | right tile list (placeholder).
    private HSplitContainer BuildBody()
    {
        var body = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill };

        var left = new VBoxContainer { CustomMinimumSize = new Vector2(260, 0) };
        left.AddChild(new Label { Text = "Sources" });

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _sourceGrid = new GridContainer { Columns = SourceColumns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sourceGrid.AddThemeConstantOverride("h_separation", 6);
        _sourceGrid.AddThemeConstantOverride("v_separation", 6);
        scroll.AddChild(_sourceGrid);
        left.AddChild(scroll);
        body.AddChild(left);

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddChild(new Label { Text = "Tiles" });
        right.AddChild(new Panel { SizeFlagsVertical = SizeFlags.ExpandFill });
        body.AddChild(right);

        return body;
    }

    /// <summary>Takes the active layer's TileSet and fills the source grid with thumbnails. Null clears it.</summary>
    public void SetTileSet(CadastileTileSet tileSet)
    {
        if (_sourceGrid == null)
            return;

        foreach (Node child in _sourceGrid.GetChildren())
            child.QueueFree();

        if (tileSet == null)
            return;

        foreach ((int sourceId, TileSetAtlasSource atlas) in tileSet.GetAtlasSources())
        {
            _sourceGrid.AddChild(new TextureRect
            {
                Texture = atlas.Texture,
                CustomMinimumSize = ThumbSize,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TooltipText = atlas.Texture?.ResourcePath ?? $"source {sourceId}",
            });
        }
    }
}
#endif
