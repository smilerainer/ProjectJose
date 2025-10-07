using Godot;
/// <summary>
/// Main dialogue UI controller - NOT a portrait scene.
/// Manages dialogue display only. Portraits are handled by Dialogic.
/// </summary>
public partial class DialogueControls : Control
{
    [Export] public string TestTimelinePath { get; set; } = "";
    
    [ExportGroup("Layer Settings")]
    [Export] public int UIZIndex { get; set; } = 100;

    [ExportGroup("Portrait Settings")]
    [Export] public float PortraitTopOffset { get; set; } = 0f;
    
    private bool _panelDisabled = false;
    
    public override void _Ready()
    {
        ZIndex = UIZIndex;
        
        // Check if we should start a timeline (VN scenes only)
        var sceneManager = GetNodeOrNull<SceneManager>("/root/SceneManager");
        if (sceneManager != null && sceneManager.HasSceneParameter("timeline"))
        {
            string timelinePath = sceneManager.GetSceneParameter("timeline").AsString();
            
            var dialogic = GetNodeOrNull("/root/Dialogic");
            if (dialogic != null && !string.IsNullOrEmpty(timelinePath))
            {
                GD.Print($"[DialogueControls] Starting timeline: {timelinePath}");
                dialogic.Call("start", timelinePath);
            }
        }
        // NOTE: Visibility is managed by CentralInputManager - no manual Show/Hide needed
    }
    
    public override void _Process(double delta)
    {
        if (!_panelDisabled)
        {
            DisableTextPanel();
        }
        
        LowerPortraitLayers();
        FixPortraitLayers();
    }
   
    private void FixPortraitLayers()
    {
        var portraits = GetTree().GetNodesInGroup("dialogic_portrait");
        
        foreach (var portrait in portraits)
        {
            if (portrait is TextureRect textureRect && IsInstanceValid(textureRect))
            {
                textureRect.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
                textureRect.ExpandMode = TextureRect.ExpandModeEnum.KeepSize;
                textureRect.StretchMode = TextureRect.StretchModeEnum.Keep;
                
                textureRect.AnchorTop = 0;
                textureRect.OffsetTop = PortraitTopOffset;
                
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
            control.Visible = false;
            control.Modulate = new Color(1, 1, 1, 0);
            control.ProcessMode = ProcessModeEnum.Disabled;
            control.MouseFilter = MouseFilterEnum.Ignore;
            control.Position = new Vector2(-10000, -10000);

            if (control.HasMethod("set_enabled"))
            {
                control.Call("set_enabled", false);
            }

            _panelDisabled = true;
            GD.Print("[DialogueControls] Disabled DialogTextPanel");
        }
    }
    
    private void LowerPortraitLayers()
    {
        var portraitLayer = GetNodeOrNull("/root/DialogicLayout_VisualNovelStyle/VN_PortraitLayer");
        if (portraitLayer is CanvasLayer canvasLayer && IsInstanceValid(canvasLayer))
        {
            if (canvasLayer.Layer >= 0)
            {
                canvasLayer.Layer = -1;
            }
        }
        
        var portraits = GetTree().GetNodesInGroup("dialogic_portrait");
        foreach (var portrait in portraits)
        {
            if (portrait is Control control && IsInstanceValid(control))
            {
                control.ZIndex = -10;
            }
        }
    }
}