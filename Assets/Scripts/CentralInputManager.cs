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
    [Export] private Vector2 uiCursorOffset = Vector2.Zero;
    [Export] private bool enableDebugTabCycling = false;
    [Export] private bool enableVerboseDebug = true;
    [Export] private bool debugCursorPositioning = true;
    [Export] private bool enableCursorSnapping = true; // New: instant positioning for pixel-perfect games
    
    // Cursor timing constants - optimized for responsive feel
    private const float CURSOR_SPEED = 1200.0f; // pixels per second - fast enough to reach anywhere in ~100ms
    private const float SNAP_DISTANCE = 3.0f; // snap when within 3 pixels - imperceptible to user

    private InputContext currentContext = InputContext.None;
    private List<MenuControls> menuControls = new();
    private List<HexControls> hexControls = new();
    private List<NovelControls> novelControls = new();
    
    // MenuControls spatial grid navigation
    private MenuControls[,] menuSpatialGrid;
    private Dictionary<MenuControls, Vector2I> menuGridPositions = new();
    private int gridWidth = 0;
    private int gridHeight = 0;
    private Vector2I currentGridPosition = Vector2I.Zero;

    private TextureRect cursorDisplay;
    private Vector2 targetCursorPosition = Vector2.Zero;
    private Node currentActiveControl;

    // Debug frame throttling
    private int lastDebugFrame = -1;
    private int lastVerboseDebugFrame = -1;

    public InputContext CurrentContext => currentContext;
    public Node ActiveControl => currentActiveControl;

    #region Public API

    public override void _Ready()
    {
        // Set HIGHEST priority to intercept inputs first
        ProcessPriority = -10000;
        
        InitializeInputManager();
        SetupCursorDisplay();
        
        // Position cursor on initial active control after everything is set up
        CallDeferred(nameof(InitialCursorPositioning));
        
        if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
            GD.Print($"[InputManager] _Ready complete - ProcessPriority: {ProcessPriority}");
    }

    public override void _Process(double delta)
    {
        ScanForActiveControls();
        UpdateCursorPosition(delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Try _UnhandledInput as backup - this runs AFTER other nodes have processed
        if (enableDebugTabCycling && @event.IsActionPressed("ui_focus_next"))
        {
            if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                GD.Print("[InputManager] _UnhandledInput: Tab detected as UNHANDLED - intercepting!");
            
            GetViewport().SetInputAsHandled();
            CycleDebugMenuFocus();
        }
    }

    public override void _Input(InputEvent @event)
    {
        // Only log Tab key, not every input
        if (enableDebugTabCycling && @event.IsActionPressed("ui_focus_next"))
        {
            if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                GD.Print("[InputManager] _Input: Tab detected - attempting to handle!");
            
            GetViewport().SetInputAsHandled();
            CycleDebugMenuFocus();
            return;
        }
        
        if (currentContext == InputContext.None) return;
        RouteInputToActiveControl(@event);
    }

    public void RegisterMenuControl(MenuControls menu)
    {
        if (!menuControls.Contains(menu))
        {
            menuControls.Add(menu);
            if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                GD.Print($"[InputManager] Registered MenuControl: {menu.Name}");
        }
    }

    public void RegisterHexControl(HexControls hex)
    {
        if (!hexControls.Contains(hex))
        {
            hexControls.Add(hex);
            if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                GD.Print($"[InputManager] Registered HexControl: {hex.Name}");
        }
    }

    public void RegisterNovelControl(NovelControls novel)
    {
        if (!novelControls.Contains(novel))
        {
            novelControls.Add(novel);
            if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                GD.Print($"[InputManager] Registered NovelControl: {novel.Name}");
        }
    }

    #endregion

    #region Debug Helpers

    private bool ShouldDebugThisFrame(ref int lastFrameRef)
    {
        int currentFrame = (int)Engine.GetProcessFrames();
        if (currentFrame != lastFrameRef)
        {
            lastFrameRef = currentFrame;
            return true;
        }
        return false;
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
        var priorityControl = FindHighestPriorityControl(activeControls);
        SetContextAndControl(InputContext.Mixed, priorityControl);
    }

    private Node FindHighestPriorityControl(List<Node> controls)
    {
        // Priority: Novel > Menu > Hex
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
        if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
        {
            GD.Print($"[InputManager] Context changed: {oldContext} -> {newContext}");

            if (currentActiveControl != null)
                GD.Print($"[InputManager] Active control: {currentActiveControl.GetType().Name} ({currentActiveControl.Name})");
        }

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

        if (@event.IsActionPressed("ui_up") || IsKeyPressed(@event, Key.W)) 
            direction = new Vector2I(0, -1);
        else if (@event.IsActionPressed("ui_down") || IsKeyPressed(@event, Key.S)) 
            direction = new Vector2I(0, 1);
        else if (@event.IsActionPressed("ui_left") || IsKeyPressed(@event, Key.A)) 
            direction = new Vector2I(-1, 0);
        else if (@event.IsActionPressed("ui_right") || IsKeyPressed(@event, Key.D)) 
            direction = new Vector2I(1, 0);
        else if (@event.IsActionPressed("ui_accept") || IsKeyPressed(@event, Key.Space) || IsKeyPressed(@event, Key.Enter))
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

    private bool IsKeyPressed(InputEvent @event, Key key)
    {
        return @event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == key;
    }

    private void HandleHexInput(HexControls hex, InputEvent @event)
    {
        // TODO: Implement hex input handling when needed
    }

    private void HandleNovelInput(NovelControls novel, InputEvent @event)
    {
        if (@event.IsActionPressed("ui_accept") ||
            (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left))
        {
            if (novel.IsShowingChoices)
            {
                // TODO: Handle choice selection when implemented
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
        AutoDiscoverControls();
        
        // Ensure we have the highest processing priority
        ProcessPriority = -10000;
        
        // Make sure we're in the scene tree early
        SetProcessInput(true);
        SetProcessUnhandledInput(true);
        
        if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
        {
            GD.Print($"[InputManager] Initialized - ProcessPriority: {ProcessPriority}");
            GD.Print($"[InputManager] ProcessInput: {IsProcessingInput()}, ProcessUnhandledInput: {IsProcessingUnhandledInput()}");
            GD.Print($"[InputManager] Debug tab cycling enabled: {enableDebugTabCycling}");
        }
    }

    private void AutoDiscoverControls()
    {
        var sceneRoot = GetTree().CurrentScene;
        if (sceneRoot == null) return;

        DiscoverControlsRecursively(sceneRoot);
        BuildMenuSpatialGrid();

        if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
            GD.Print($"[InputManager] Auto-discovered: {menuControls.Count} menus, {hexControls.Count} hex, {novelControls.Count} novels");
    }
    
    private void BuildMenuSpatialGrid()
    {
        if (menuControls.Count == 0) return;
        
        // Get all menu positions and sort them spatially
        var menuPositions = menuControls
            .Select(menu => new { Menu = menu, Position = menu.GlobalPosition })
            .OrderBy(mp => mp.Position.Y) // Top to bottom first
            .ThenBy(mp => mp.Position.X)  // Left to right second
            .ToList();

        // Group menus by Y position (with tolerance for slight misalignment)
        const float yTolerance = 50f; // Adjust based on your UI layout
        var rows = new List<List<MenuControls>>();
        
        foreach (var menuPos in menuPositions)
        {
            var existingRow = rows.FirstOrDefault(row => 
                Mathf.Abs(row[0].GlobalPosition.Y - menuPos.Position.Y) <= yTolerance);
            
            if (existingRow != null)
            {
                existingRow.Add(menuPos.Menu);
            }
            else
            {
                rows.Add(new List<MenuControls> { menuPos.Menu });
            }
        }

        // Sort each row by X position (left to right)
        foreach (var row in rows)
        {
            row.Sort((a, b) => a.GlobalPosition.X.CompareTo(b.GlobalPosition.X));
        }

        // Calculate grid dimensions
        gridHeight = rows.Count;
        gridWidth = rows.Max(row => row.Count);
        
        // Create 2D grid and position mapping
        menuSpatialGrid = new MenuControls[gridWidth, gridHeight];
        menuGridPositions.Clear();
        
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < rows[y].Count; x++)
            {
                var menu = rows[y][x];
                menuSpatialGrid[x, y] = menu;
                menuGridPositions[menu] = new Vector2I(x, y);
            }
        }

        // Debug output (throttled)
        if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
        {
            GD.Print($"[InputManager] Built {gridWidth}x{gridHeight} menu spatial grid:");
            for (int y = 0; y < gridHeight; y++)
            {
                var rowMenus = new List<string>();
                for (int x = 0; x < gridWidth; x++)
                {
                    var menu = menuSpatialGrid[x, y];
                    rowMenus.Add(menu?.Name ?? "null");
                }
                GD.Print($"  Row {y}: [{string.Join(", ", rowMenus)}]");
            }
        }
        
        // Start at the first available position
        currentGridPosition = Vector2I.Zero;
        EnsureValidGridPosition();
    }
    
    private MenuControls GetMenuAtGridPosition(Vector2I pos)
    {
        if (pos.X < 0 || pos.X >= gridWidth || pos.Y < 0 || pos.Y >= gridHeight)
            return null;
        return menuSpatialGrid[pos.X, pos.Y];
    }
    
    private void EnsureValidGridPosition()
    {
        // Make sure we're pointing to a valid menu, wrap around if needed
        int attempts = 0;
        int maxAttempts = gridWidth * gridHeight;
        
        while (GetMenuAtGridPosition(currentGridPosition) == null && attempts < maxAttempts)
        {
            MoveToNextGridPosition();
            attempts++;
        }
        
        if (attempts >= maxAttempts)
        {
            GD.PrintErr("[InputManager] No valid grid positions found!");
            currentGridPosition = Vector2I.Zero;
        }
    }
    
    private void MoveToNextGridPosition()
    {
        currentGridPosition.X++;
        if (currentGridPosition.X >= gridWidth)
        {
            currentGridPosition.X = 0;
            currentGridPosition.Y++;
            if (currentGridPosition.Y >= gridHeight)
            {
                currentGridPosition.Y = 0;
            }
        }
    }
    
    private void CycleDebugMenuFocus()
    {
        if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
            GD.Print("=== CycleDebugMenuFocus CALLED ===");
        
        if (menuSpatialGrid == null || gridWidth == 0 || gridHeight == 0) 
        {
            GD.PrintErr("[InputManager] No menu spatial grid available!");
            return;
        }
        
        if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
        {
            GD.Print($"[InputManager] BEFORE - Active menus:");
            foreach (var menu in menuControls)
            {
                if (menu.IsActive)
                    GD.Print($"  - {menu.Name} is ACTIVE");
            }
            
            GD.Print($"[InputManager] Current grid position: {currentGridPosition}");
        }
        
        // Deactivate all menus
        foreach (var menu in menuControls)
        {
            if (menu.IsActive)
            {
                if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                    GD.Print($"[InputManager] Deactivating menu: {menu.Name}");
                menu.SetActive(false);
            }
        }
        
        // Move to next position in grid
        var oldPosition = currentGridPosition;
        MoveToNextGridPosition();
        EnsureValidGridPosition();
        
        if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
            GD.Print($"[InputManager] Moved from {oldPosition} to {currentGridPosition}");
        
        // Activate the new menu and reset it to 0,0 button
        var newMenu = GetMenuAtGridPosition(currentGridPosition);
        if (newMenu != null)
        {
            if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                GD.Print($"[InputManager] Activating menu: {newMenu.Name} at grid position {currentGridPosition}");
            
            newMenu.SetActive(true);
            
            // Reset the menu to its 0,0 button position (if method exists)
            if (newMenu.HasMethod("ResetToFirstButton"))
            {
                newMenu.Call("ResetToFirstButton");
                if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                    GD.Print($"[InputManager] Reset {newMenu.Name} to first button");
            }
            else
            {
                if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                    GD.Print($"[InputManager] WARNING: {newMenu.Name} does not have ResetToFirstButton method");
            }
            
            UpdateCursorTarget(); // Update cursor to new position
        }
        else
        {
            GD.PrintErr($"[InputManager] CRITICAL: No menu found at grid position {currentGridPosition}!");
        }
        
        if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
        {
            GD.Print($"[InputManager] AFTER - Active menus:");
            foreach (var menu in menuControls)
            {
                if (menu.IsActive)
                    GD.Print($"  - {menu.Name} is NOW ACTIVE");
            }
            
            GD.Print("=== CycleDebugMenuFocus COMPLETE ===");
        }
    }
    
    public void RefreshUILayout()
    {
        if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
            GD.Print("[InputManager] Refreshing UI layout...");
        
        BuildMenuSpatialGrid();
        
        // If there was an active menu, try to keep it active
        var currentActiveMenu = currentActiveControl as MenuControls;
        if (currentActiveMenu != null && menuGridPositions.ContainsKey(currentActiveMenu))
        {
            currentGridPosition = menuGridPositions[currentActiveMenu];
            if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                GD.Print($"[InputManager] Maintained focus on {currentActiveMenu.Name} at position {currentGridPosition}");
        }
        else
        {
            // Start fresh at first valid position
            currentGridPosition = Vector2I.Zero;
            EnsureValidGridPosition();
        }
    }
    
    // Call this when a MenuControls changes its button focus
    public void NotifyButtonFocusChanged()
    {
        // ALWAYS output debug info for this critical method
        GD.Print($"[CURSOR] ═══ NotifyButtonFocusChanged() CALLED ═══");
        GD.Print($"[CURSOR] Current context: {currentContext}");
        GD.Print($"[CURSOR] Active control: {currentActiveControl?.GetType().Name} ({currentActiveControl?.Name})");
        
        // Force immediate cursor update
        UpdateCursorTarget();
        
        GD.Print($"[CURSOR] Target position: {targetCursorPosition}");
        if (cursorDisplay != null)
        {
            GD.Print($"[CURSOR] Cursor display position: {cursorDisplay.Position}");
            GD.Print($"[CURSOR] Cursor offset: {cursorOffset}");
        }
        else
        {
            GD.PrintErr("[CURSOR] ❌ cursorDisplay is NULL!");
        }
        
        GD.Print($"[CURSOR] ═══ NotifyButtonFocusChanged() COMPLETE ═══");
    }

    // Initial cursor positioning when scene loads
    private void InitialCursorPositioning()
    {
        // Scan for active controls first
        ScanForActiveControls();
        
        // If we found an active control, position cursor there
        if (currentActiveControl != null)
        {
            var actionPos = GetCurrentActionPosition();
            if (actionPos != Vector2.Zero)
            {
                targetCursorPosition = actionPos;
                if (cursorDisplay != null)
                {
                    // Set cursor immediately without smoothing for initial positioning
                    cursorDisplay.Position = targetCursorPosition.Round() - cursorOffset;
                }
                
                if (debugCursorPositioning && ShouldDebugThisFrame(ref lastDebugFrame))
                    GD.Print($"[CURSOR] Initial positioning: {currentActiveControl.GetType().Name} ({currentActiveControl.Name}) at {targetCursorPosition}");
            }
        }
        else
        {
            // No active control found initially - try again after a short delay
            // This handles cases where MenuControls haven't finished initializing yet
            GetTree().CreateTimer(0.1f).Connect("timeout", new Callable(this, nameof(DelayedInitialPositioning)));
            
            if (debugCursorPositioning && ShouldDebugThisFrame(ref lastDebugFrame))
                GD.Print("[CURSOR] Initial positioning: No active control found, retrying in 0.1s");
        }
    }
    
    private void DelayedInitialPositioning()
    {
        ScanForActiveControls();
        
        if (currentActiveControl != null)
        {
            var actionPos = GetCurrentActionPosition();
            if (actionPos != Vector2.Zero)
            {
                targetCursorPosition = actionPos;
                if (cursorDisplay != null)
                {
                    cursorDisplay.Position = targetCursorPosition.Round() - cursorOffset;
                }
                
                if (debugCursorPositioning && ShouldDebugThisFrame(ref lastDebugFrame))
                    GD.Print($"[CURSOR] Delayed initial positioning: {currentActiveControl.GetType().Name} ({currentActiveControl.Name}) at {targetCursorPosition}");
            }
        }
        else
        {
            // Still no active control, position at mouse
            targetCursorPosition = GetViewport().GetMousePosition();
            if (cursorDisplay != null)
            {
                cursorDisplay.Position = targetCursorPosition.Round() - cursorOffset;
            }
            
            if (debugCursorPositioning && ShouldDebugThisFrame(ref lastDebugFrame))
                GD.Print("[CURSOR] Delayed initial positioning: No active control, using mouse position");
        }
    }

    private void DiscoverControlsRecursively(Node node)
    {
        if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
            GD.Print($"[InputManager] Scanning node: {node.Name} ({node.GetType().Name})");
        
        switch (node)
        {
            case MenuControls menu:
                RegisterMenuControl(menu);
                if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                    GD.Print($"[InputManager] ✓ FOUND MenuControls: {menu.Name} ({menu.GetType().Name}) at {menu.GlobalPosition}");
                break;
            case HexControls hex:
                RegisterHexControl(hex);
                if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                    GD.Print($"[InputManager] ✓ FOUND HexControls: {hex.Name} ({hex.GetType().Name})");
                break;
            case NovelControls novel:
                RegisterNovelControl(novel);
                if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                    GD.Print($"[InputManager] ✓ FOUND NovelControls: {novel.Name} ({novel.GetType().Name})");
                break;
            default:
                // Check if this node has the script attached but isn't being detected
                if (node.HasMethod("SetActive") && node.HasMethod("IsActive"))
                {
                    if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                        GD.Print($"[InputManager] ⚠️  Node {node.Name} ({node.GetType().Name}) has menu methods but isn't MenuControls type!");
                }
                break;
        }

        foreach (Node child in node.GetChildren())
        {
            DiscoverControlsRecursively(child);
        }
    }

    private void SetupCursorDisplay()
    {
        if (!enableCursor || cursorTexture == null) return;

        CreateCursorDisplay();
        ConfigureCursorProperties();
    }

    private void CreateCursorDisplay()
    {
        // Try to find UI layer first
        var uiLayer = GetTree().CurrentScene.GetNodeOrNull<CanvasLayer>("UI");
        
        cursorDisplay = new TextureRect
        {
            Texture = cursorTexture,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 2000
        };

        if (uiLayer != null)
        {
            uiLayer.AddChild(cursorDisplay);
            if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                GD.Print("[InputManager] Created cursor on UI layer");
        }
        else
        {
            AddChild(cursorDisplay);
            if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                GD.Print("[InputManager] Created cursor as direct child");
        }

        targetCursorPosition = GetViewport().GetMousePosition();
    }

    private void ConfigureCursorProperties()
    {
        if (cursorDisplay == null) return;

        cursorDisplay.PivotOffset = cursorOffset;
        cursorDisplay.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
    }

    private void UpdateCursorPosition(double delta)
    {
        if (cursorDisplay == null) return;

        var actionPos = GetCurrentActionPosition();
        if (actionPos != Vector2.Zero)
        {
            targetCursorPosition = actionPos;
        }
        else
        {
            targetCursorPosition = GetViewport().GetMousePosition();
        }

        var currentPos = cursorDisplay.Position + cursorOffset;
        Vector2 newPos;

        if (enableCursorSnapping)
        {
            // Instant positioning for pixel-perfect games
            newPos = targetCursorPosition;
        }
        else
        {
            // Fast physics-based movement with guaranteed timing
            var distance = currentPos.DistanceTo(targetCursorPosition);
            
            if (distance <= SNAP_DISTANCE)
            {
                // Snap when very close - imperceptible to user
                newPos = targetCursorPosition;
            }
            else
            {
                // Move at constant speed towards target
                var direction = (targetCursorPosition - currentPos).Normalized();
                var moveDistance = CURSOR_SPEED * (float)delta;
                
                // Don't overshoot the target
                if (moveDistance >= distance)
                {
                    newPos = targetCursorPosition;
                }
                else
                {
                    newPos = currentPos + (direction * moveDistance);
                }
            }
        }

        // Apply pixel-perfect positioning
        cursorDisplay.Position = newPos.Round() - cursorOffset;
    }

    private Vector2 GetCurrentActionPosition()
    {
        var position = currentActiveControl switch
        {
            MenuControls menu => GetMenuScreenPosition(menu),
            HexControls hex => GetHexScreenPosition(hex),
            NovelControls novel => novel.CurrentActionPosition,
            _ => Vector2.Zero
        };
        
        return position;
    }

    private Vector2 GetMenuScreenPosition(MenuControls menu)
    {
        Vector2 finalPosition = Vector2.Zero;
        string positionSource = "none";
        
        // NEW APPROACH: Find the actually focused button in the scene instead of relying on MenuControls
        var focusedButton = GetCurrentlyFocusedButton();
        
        if (focusedButton != null && focusedButton is Control buttonControl)
        {
            var buttonRect = buttonControl.GetGlobalRect();
            // Use center of button for consistent positioning
            finalPosition = buttonRect.Position + (buttonRect.Size * 0.5f) + uiCursorOffset;
            positionSource = $"scene-focused button '{buttonControl.Name}' center";
            
            // Enhanced debug output to track button changes
            if (debugCursorPositioning && ShouldDebugThisFrame(ref lastDebugFrame))
            {
                GD.Print($"[CURSOR] Found focused button: {buttonControl.Name}");
                GD.Print($"  Button rect: {buttonRect}");
                GD.Print($"  Final position: {finalPosition}");
            }
        }
        else
        {
            // Fallback: try to get current button from MenuControls (original approach)
            var currentButton = menu.CurrentButton;
            if (currentButton != null && currentButton is Control fallbackControl)
            {
                var buttonRect = fallbackControl.GetGlobalRect();
                finalPosition = buttonRect.Position + (buttonRect.Size * 0.5f) + uiCursorOffset;
                positionSource = $"menucontrols button '{fallbackControl.Name}' center";
            }
            else if (menu is Control menuControl)
            {
                var menuRect = menuControl.GetGlobalRect();
                finalPosition = menuRect.Position + (menuRect.Size * 0.5f) + uiCursorOffset;
                positionSource = "menu container center";
            }
            else
            {
                finalPosition = menu.GlobalPosition + uiCursorOffset;
                positionSource = "global position";
            }
            
            if (debugCursorPositioning && ShouldDebugThisFrame(ref lastDebugFrame))
            {
                GD.Print($"[CURSOR] No focused button found, using fallback: {positionSource}");
                GD.Print($"  Final position: {finalPosition}");
            }
        }
        
        return finalPosition;
    }
    
    // Find the currently focused button in the entire scene
    private BaseButton GetCurrentlyFocusedButton()
    {
        var focusOwner = GetViewport().GuiGetFocusOwner();
        if (focusOwner is BaseButton button)
        {
            return button;
        }
        return null;
    }

    private Vector2 GetHexScreenPosition(HexControls hex)
    {
        var hexLocalPos = hex.GetCursorWorldPosition();
        if (hexLocalPos == Vector2.Zero) return Vector2.Zero;

        var globalWorldPos = hex.ToGlobal(hexLocalPos);
        var camera = GetViewport().GetCamera2D();
        if (camera == null) return Vector2.Zero;

        var viewportSize = GetViewport().GetVisibleRect().Size;
        var screenPos = globalWorldPos - camera.GlobalPosition + viewportSize * 0.5f;

        return screenPos;
    }

    private void UpdateCursorTarget()
    {
        var actionPos = GetCurrentActionPosition();
        if (actionPos != Vector2.Zero)
        {
            targetCursorPosition = actionPos;
        }
    }

    #endregion
}