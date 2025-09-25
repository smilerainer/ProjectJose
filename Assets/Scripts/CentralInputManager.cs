// CentralInputManager.cs - Manages all control types and draws action cursor
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class CentralInputManager : Node2D
{
    public enum InputContext
    {
        None,
        Menu,
        HexGrid,
        Novel,
        Mixed
    }
    
    [Export] private Texture2D cursorTexture;
    [Export] private Vector2 cursorOffset = Vector2.Zero;
    [Export] private bool enableCursor = true;
    [Export] private float cursorSmoothness = 8f;
    
    private InputContext currentContext = InputContext.None;
    private List<MenuControls> menuControls = new();
    private List<HexControls> hexControls = new();
    private List<NovelControls> novelControls = new();
    
    private TextureRect cursorDisplay;
    private Vector2 targetCursorPosition = Vector2.Zero;
    private Node currentActiveControl;
    
    public InputContext CurrentContext => currentContext;
    public Node ActiveControl => currentActiveControl;
    
    #region Public API
    
    public override void _Ready()
    {
        InitializeInputManager();
        SetupCursorDisplay();
    }
    
    public override void _Process(double delta)
    {
        ScanForActiveControls();
        UpdateCursorPosition(delta);
    }
    
    public override void _Input(InputEvent @event)
    {
        if (currentContext == InputContext.None) return;
        
        RouteInputToActiveControl(@event);
    }
    
    public void RegisterMenuControl(MenuControls menu)
    {
        if (!menuControls.Contains(menu))
        {
            menuControls.Add(menu);
            GD.Print($"[InputManager] Registered MenuControl: {menu.Name}");
        }
    }
    
    public void RegisterHexControl(HexControls hex)
    {
        if (!hexControls.Contains(hex))
        {
            hexControls.Add(hex);
            GD.Print($"[InputManager] Registered HexControl: {hex.Name}");
        }
    }
    
    public void RegisterNovelControl(NovelControls novel)
    {
        if (!novelControls.Contains(novel))
        {
            novelControls.Add(novel);
            GD.Print($"[InputManager] Registered NovelControl: {novel.Name}");
        }
    }
    
    #endregion
    
    #region Context Detection
    
    private void ScanForActiveControls()
    {
        var oldContext = currentContext;
        var oldActiveControl = currentActiveControl;
        
        DetectCurrentContext();
        
        if (currentContext != oldContext || currentActiveControl != oldActiveControl)
        {
            OnContextChanged(oldContext, currentContext);
        }
    }
    
    private void DetectCurrentContext()
    {
        var activeControls = FindActiveControls();
        
        if (activeControls.Count == 0)
        {
            SetContextAndControl(InputContext.None, null);
            return;
        }
        
        if (activeControls.Count == 1)
        {
            SetSingleActiveContext(activeControls[0]);
        }
        else
        {
            SetMixedContext(activeControls);
        }
    }
    
    private List<Node> FindActiveControls()
    {
        var activeControls = new List<Node>();
        
        // Check all registered controls for activity
        activeControls.AddRange(menuControls.Where(m => m.IsActive));
        activeControls.AddRange(hexControls.Where(h => h.IsActive));
        activeControls.AddRange(novelControls.Where(n => n.IsActive));
        
        return activeControls;
    }
    
    private void SetSingleActiveContext(Node activeControl)
    {
        var context = DetermineContextFromControl(activeControl);
        SetContextAndControl(context, activeControl);
    }
    
    private InputContext DetermineContextFromControl(Node control)
    {
        return control switch
        {
            MenuControls => InputContext.Menu,
            HexControls => InputContext.HexGrid,
            NovelControls => InputContext.Novel,
            _ => InputContext.None
        };
    }
    
    private void SetMixedContext(List<Node> activeControls)
    {
        // Determine priority - Novel > Menu > HexGrid
        var priorityControl = FindHighestPriorityControl(activeControls);
        SetContextAndControl(InputContext.Mixed, priorityControl);
    }
    
    private Node FindHighestPriorityControl(List<Node> controls)
    {
        // Priority order: NovelControls > MenuControls > HexControls
        return controls.FirstOrDefault(c => c is NovelControls) ??
               controls.FirstOrDefault(c => c is MenuControls) ??
               controls.FirstOrDefault(c => c is HexControls);
    }
    
    private void SetContextAndControl(InputContext context, Node control)
    {
        currentContext = context;
        currentActiveControl = control;
    }
    
    private void OnContextChanged(InputContext oldContext, InputContext newContext)
    {
        GD.Print($"[InputManager] Context changed: {oldContext} -> {newContext}");
        
        if (currentActiveControl != null)
            GD.Print($"[InputManager] Active control: {currentActiveControl.GetType().Name} ({currentActiveControl.Name})");
        
        UpdateCursorTarget();
    }
    
    #endregion
    
    #region Input Routing
    
    private void RouteInputToActiveControl(InputEvent @event)
    {
        if (currentActiveControl == null) return;
        
        switch (currentActiveControl)
        {
            case MenuControls menu:
                HandleMenuInput(menu, @event);
                break;
            case HexControls hex:
                HandleHexInput(hex, @event);
                break;
            case NovelControls novel:
                HandleNovelInput(novel, @event);
                break;
        }
    }
    
    private void HandleMenuInput(MenuControls menu, InputEvent @event)
    {
        Vector2I direction = Vector2I.Zero;
        
        if (@event.IsActionPressed("ui_up")) direction = new Vector2I(0, -1);
        else if (@event.IsActionPressed("ui_down")) direction = new Vector2I(0, 1);
        else if (@event.IsActionPressed("ui_left")) direction = new Vector2I(-1, 0);
        else if (@event.IsActionPressed("ui_right")) direction = new Vector2I(1, 0);
        else if (@event.IsActionPressed("ui_accept"))
        {
            menu.ActivateCurrentButton();
            return;
        }
        
        if (direction != Vector2I.Zero)
        {
            menu.Navigate(direction);
            UpdateCursorTarget();
        }
    }
    
    private void HandleHexInput(HexControls hex, InputEvent @event)
    {
        // // Mouse input (TODO: FIX PROTECTION LEVEL)
        // if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        // {
        //     if (mouseButton.ButtonIndex == MouseButton.Left)
        //     {
        //         hex.HandleMouseInput(@event);
        //         UpdateCursorTarget();
        //     }
        // }
        // else if (@event is InputEventMouseMotion && hex.enableDebugHover)
        // {
        //     hex.HandleMouseInput(@event);
        // }
        
        // // Keyboard input (if debug WASD is enabled)
        // if (hex.enableDebugWASD)
        // {
        //     hex.HandleKeyboardInput(@event);
        //     UpdateCursorTarget();
        // }
    }
    
    private void HandleNovelInput(NovelControls novel, InputEvent @event)
    {
        if (@event.IsActionPressed("ui_accept") || 
            (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left))
        {
            if (novel.IsShowingChoices)
            {
                // TODO: Handle choice selection when implemented
                // novel.MakeChoice(0);
            }
            else
            {
                novel.AdvanceText();
            }
        }
        else if (novel.IsShowingChoices)
        {
            Vector2I direction = Vector2I.Zero;
            
            if (@event.IsActionPressed("ui_up")) direction = new Vector2I(0, -1);
            else if (@event.IsActionPressed("ui_down")) direction = new Vector2I(0, 1);
            
            if (direction != Vector2I.Zero)
            {
                novel.Navigate(direction);
                UpdateCursorTarget();
            }
        }
    }
    
    #endregion
    
    #region Cursor Management
    
    private void InitializeInputManager()
    {
        // Auto-discover existing controls in scene
        AutoDiscoverControls();
        
        // Set high processing priority
        ProcessPriority = -1000;
        
        GD.Print("[InputManager] Initialized");
    }
    
    private void AutoDiscoverControls()
    {
        var sceneRoot = GetTree().CurrentScene;
        if (sceneRoot == null) return;
        
        DiscoverControlsRecursively(sceneRoot);
        
        GD.Print($"[InputManager] Auto-discovered: {menuControls.Count} menus, {hexControls.Count} hex, {novelControls.Count} novels");
    }
    
    private void DiscoverControlsRecursively(Node node)
    {
        // Register controls found in scene
        switch (node)
        {
            case MenuControls menu:
                RegisterMenuControl(menu);
                break;
            case HexControls hex:
                RegisterHexControl(hex);
                break;
            case NovelControls novel:
                RegisterNovelControl(novel);
                break;
        }
        
        // Recurse through children
        foreach (Node child in node.GetChildren())
        {
            DiscoverControlsRecursively(child);
        }
    }
    
    private void SetupCursorDisplay()
    {
        if (!enableCursor || cursorTexture == null) return;
        
        CreateCursorSprite();
        ConfigureCursorProperties();
        HideSystemCursor();
    }
    
    private void CreateCursorSprite()
    {
        cursorDisplay = new TextureRect();
        cursorDisplay.Texture = cursorTexture;
        cursorDisplay.MouseFilter = Control.MouseFilterEnum.Ignore;
        cursorDisplay.ZIndex = 2000; // Above everything
        AddChild(cursorDisplay);
        
        targetCursorPosition = GetViewport().GetMousePosition();
    }
    
    private void ConfigureCursorProperties()
    {
        if (cursorDisplay == null) return;
        
        cursorDisplay.PivotOffset = cursorOffset;
        cursorDisplay.TextureFilter = CanvasItem.TextureFilterEnum.Nearest; // Pixel perfect
    }

	private void HideSystemCursor()
	{
		// Does exactly what it says on the tin
        // Input.SetDefaultCursorShape(Input.CursorShape.Hidden);
	}
    
    private void UpdateCursorPosition(double delta)
    {
        if (cursorDisplay == null) return;
        
        // Get mouse position as fallback
        var mousePos = GetViewport().GetMousePosition();
        
        // Use action position if available, otherwise mouse
        var actionPos = GetCurrentActionPosition();
        if (actionPos != Vector2.Zero)
            targetCursorPosition = actionPos;
        else
            targetCursorPosition = mousePos;
        
        // Smooth cursor movement
        var currentPos = cursorDisplay.Position + cursorOffset;
        var newPos = currentPos.Lerp(targetCursorPosition, cursorSmoothness * (float)delta);
        
        cursorDisplay.Position = newPos.Round() - cursorOffset; // Pixel perfect
    }
    
    private Vector2 GetCurrentActionPosition()
    {
        return currentActiveControl switch
        {
            MenuControls menu => menu.CurrentButtonPosition,
            HexControls hex => hex.GetCursorWorldPosition(),
            NovelControls novel => novel.CurrentActionPosition,
            _ => Vector2.Zero
        };
    }
    
    private void UpdateCursorTarget()
    {
        // Force cursor to update to new action position
        var actionPos = GetCurrentActionPosition();
        if (actionPos != Vector2.Zero)
            targetCursorPosition = actionPos;
    }
    
    #endregion
}