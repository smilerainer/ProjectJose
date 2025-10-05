using Godot;
/// <summary>
/// Main dialogue UI controller - NOT a portrait scene.
/// Manages dialogue display only. Portraits are handled by Dialogic.
/// </summary>
public partial class DialogueControls : Control
{
    [Export] public string TestTimelinePath { get; set; } = "";
   
    [ExportGroup("Text Nodes")]
    [Export] public NodePath NameLabelPath { get; set; }
    [Export] public NodePath TextLabelPath { get; set; }
    
    [ExportGroup("Layer Settings")]
    [Export] public int UIZIndex { get; set; } = 100;

    [ExportGroup("Portrait Settings")]
    [Export] public float PortraitTopOffset { get; set; } = 0f;
    
    private bool _panelDisabled = false;
    
    public override void _Ready()
    {
        // Set high z-index to render above portraits
        ZIndex = UIZIndex;
        
        // Load test timeline if provided (only once)
        if (!string.IsNullOrEmpty(TestTimelinePath) && !GetTree().Root.HasMeta("dialogic_started"))
        {
            GetTree().Root.SetMeta("dialogic_started", true);
            CallDeferred(nameof(StartTimeline));
        }
    }
    
    public override void _Process(double delta)
    {
        if (!_panelDisabled)
        {
            DisableTextPanel();
        }
        
        LowerPortraitLayers();
        FixPortraits(); // Add this
    }
   
    private void FixPortraits()
    {
        var portraits = GetTree().GetNodesInGroup("dialogic_portrait");
        
        foreach (var portrait in portraits)
        {
            if (portrait is TextureRect textureRect && IsInstanceValid(textureRect))
            {
                // 1. Full size (no scaling)
                textureRect.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
                textureRect.ExpandMode = TextureRect.ExpandModeEnum.KeepSize;
                textureRect.StretchMode = TextureRect.StretchModeEnum.Keep;
                
                // 2. Anchor to top
                textureRect.AnchorTop = 0;
                textureRect.OffsetTop = PortraitTopOffset;
                
                // 3. Crop from bottom (set ClipContents on parent if needed)
                if (textureRect.GetParent() is Control parent)
                {
                    parent.ClipContents = true;
                }
            }
        }
    }

    private void DisableTextPanel()
    {
        var panel = GetNodeOrNull("/root/DialogicLayout_VisualNovelStyle/VN_TextboxLayer/Anchor/AnimationParent/Sizer/DialogTextPanel");

        if (panel is Control control && IsInstanceValid(control))
        {
            // Make it completely invisible and non-interactive but keep it alive
            control.Visible = false;
            control.Modulate = new Color(1, 1, 1, 0); // Fully transparent
            control.ProcessMode = ProcessModeEnum.Disabled;
            control.MouseFilter = MouseFilterEnum.Ignore;

            // Move it far off-screen as extra insurance
            control.Position = new Vector2(-10000, -10000);

            // Try to disable it if it has that property
            if (control.HasMethod("set_enabled"))
            {
                control.Call("set_enabled", false);
            }

            _panelDisabled = true;
            GD.Print("Disabled DialogTextPanel (kept in tree for Dialogic)");
        }
    }
    
    private void LowerPortraitLayers()
    {
        // Lower the portrait layer z-index
        var portraitLayer = GetNodeOrNull("/root/DialogicLayout_VisualNovelStyle/VN_PortraitLayer");
        if (portraitLayer is CanvasLayer canvasLayer && IsInstanceValid(canvasLayer))
        {
            // Set to a lower layer than the UI
            if (canvasLayer.Layer >= 0)
            {
                canvasLayer.Layer = -1;
            }
        }
        
        // Also try to lower any portrait containers
        var portraits = GetTree().GetNodesInGroup("dialogic_portrait");
        foreach (var portrait in portraits)
        {
            if (portrait is Control control && IsInstanceValid(control))
            {
                control.ZIndex = -10;
            }
        }
    }
   
    private void StartTimeline()
    {
        var dialogic = GetNode("/root/Dialogic");
        dialogic.Call("start", TestTimelinePath);
    }
    
    public new void SetName(string name)
    {
        GetNodeOrNull<Label>(NameLabelPath)?.Set("text", name);
    }
    
    public void SetText(string text)
    {
        GetNodeOrNull<RichTextLabel>(TextLabelPath)?.Set("text", text);
    }
}