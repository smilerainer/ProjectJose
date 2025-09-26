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
    [Export] private bool enableCursorSnapping = true;

    // Test button creation
    [Export] private Control dynamicMenuRoot; // Assign the Control root - system navigates to Control/MarginContainer/VBoxContainer
    [Export] private bool testAddButtons = false;
    [Export] private bool testDeleteButtons = false;
    [Export] private bool testSetButtonArray = false;

    // Cursor timing constants
    private const float CURSOR_SPEED = 1200.0f;
    private const float SNAP_DISTANCE = 3.0f;

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

    // Store the previously active menu so we can return to it
    private MenuControls previousActiveMenu = null;

    public InputContext CurrentContext => currentContext;
    public Node ActiveControl => currentActiveControl;

    public override void _Ready()
    {
        GD.Print("CENTRALINPUTMANAGER _READY CALLED WITH FLAGS: " + testSetButtonArray);
        ProcessPriority = -10000;

        InitializeInputManager();
        SetupCursorDisplay();
        CallDeferred(nameof(InitialCursorPositioning));

        if (testAddButtons && dynamicMenuRoot != null)
        {
            CallDeferred(nameof(TestAddButtons));
        }

        if (testDeleteButtons && dynamicMenuRoot != null)
        {
            CallDeferred(nameof(TestDeleteButtons));
        }

        if (testSetButtonArray && dynamicMenuRoot != null)
        {
            CallDeferred(nameof(TestSetButtonArray));
        }

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
        // Check for ESC key to clear dynamic menu - check both action and raw key
        if (@event.IsActionPressed("ui_cancel") || IsKeyPressed(@event, Key.Escape))
        {
            // Check if this is the dynamic menu
            var dynamicMenu = FindMenuControlsInStructure(dynamicMenuRoot);
            if (menu == dynamicMenu && dynamicMenu != null && dynamicMenu.IsActive)
            {
                GD.Print("[InputManager] ESC/Cancel pressed - clearing dynamic menu");
                ClearDynamicMenu();
                GetViewport().SetInputAsHandled(); // Mark input as handled
                return;
            }
            else
            {
                GD.Print($"[InputManager] ESC pressed but not on dynamic menu. Current menu: {menu.Name}, Dynamic menu: {dynamicMenu?.Name}");
            }
        }
        
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

    public void NotifyButtonFocusChanged()
    {
        UpdateCursorTarget();

        if (debugCursorPositioning && ShouldDebugThisFrame(ref lastDebugFrame))
        {
            GD.Print($"[CURSOR] Focus changed - Context: {currentContext}, Target: {targetCursorPosition}");
        }
    }

    public void SetMenuButtonArray(string[] buttonTexts)
    {
        GD.Print($"[InputManager] SetMenuButtonArray called with {buttonTexts.Length} buttons");
        
        var targetMenuControls = FindMenuControlsInStructure(dynamicMenuRoot);
        if (targetMenuControls == null)
        {
            GD.PrintErr("[InputManager] No MenuControls found for SetMenuButtonArray");
            return;
        }
        
        // Store the currently active menu so we can return to it later
        previousActiveMenu = currentActiveControl as MenuControls;
        if (previousActiveMenu != null)
        {
            GD.Print($"[InputManager] Storing previous active menu: {previousActiveMenu.Name}");
            previousActiveMenu.SetActive(false);
        }
        
        // Deactivate all other menus
        foreach (var menu in menuControls)
        {
            if (menu != targetMenuControls)
            {
                menu.SetActive(false);
            }
        }
        
        GD.Print($"[InputManager] Setting button array on MenuControls: {targetMenuControls.Name}");
        targetMenuControls.SetButtonsFromArray(buttonTexts);
        
        // Activate the dynamic menu and make it the current focus
        targetMenuControls.SetActive(true);
        currentActiveControl = targetMenuControls;
        currentContext = InputContext.Menu;
        
        // Force the target menu to reset to first button for proper navigation
        if (targetMenuControls.HasMethod("ResetToFirstButton"))
        {
            targetMenuControls.Call("ResetToFirstButton");
            GD.Print($"[InputManager] Reset {targetMenuControls.Name} to first button");
        }
        
        // Update cursor and focus
        UpdateCursorTarget();
        
        // Update the spatial grid to include this menu in navigation
        RefreshUILayout();
        
        GD.Print($"[InputManager] Successfully set button array with {buttonTexts.Length} options and activated menu");
        GD.Print($"[InputManager] Current active control: {currentActiveControl?.GetType().Name} ({currentActiveControl?.Name})");
        GD.Print($"[InputManager] Current context: {currentContext}");
    }

    // Method to clear the dynamic menu and return to previous state
    public void ClearDynamicMenu()
    {
        GD.Print("[InputManager] ClearDynamicMenu called");
        
        var targetMenuControls = FindMenuControlsInStructure(dynamicMenuRoot);
        if (targetMenuControls != null)
        {
            GD.Print($"[InputManager] Found dynamic menu: {targetMenuControls.Name}, IsActive: {targetMenuControls.IsActive}");
            targetMenuControls.SetActive(false);
            targetMenuControls.ClearAllButtons();
            GD.Print($"[InputManager] Cleared dynamic menu: {targetMenuControls.Name}");
        }
        else
        {
            GD.PrintErr("[InputManager] Could not find dynamic menu to clear");
        }
        
        // Restore the previous menu if we had one
        if (previousActiveMenu != null)
        {
            GD.Print($"[InputManager] Restoring previous menu: {previousActiveMenu.Name}");
            previousActiveMenu.SetActive(true);
            currentActiveControl = previousActiveMenu;
            currentContext = InputContext.Menu;
            
            // Reset to first button if the menu has that method
            if (previousActiveMenu.HasMethod("ResetToFirstButton"))
            {
                previousActiveMenu.Call("ResetToFirstButton");
                GD.Print($"[InputManager] Reset {previousActiveMenu.Name} to first button");
            }
            
            UpdateCursorTarget();
            GD.Print($"[InputManager] Successfully restored previous menu: {previousActiveMenu.Name}");
            previousActiveMenu = null; // Clear the reference
        }
        else
        {
            GD.Print("[InputManager] No previous menu stored, scanning for active controls");
            // No previous menu, just scan for any active controls
            currentActiveControl = null;
            currentContext = InputContext.None;
            ScanForActiveControls();
        }
        
        RefreshUILayout();
        GD.Print("[InputManager] ClearDynamicMenu complete");
    }

    private void InitializeInputManager()
    {
        AutoDiscoverControls();
        ProcessPriority = -10000;
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

        var menuPositions = menuControls
            .Select(menu => new { Menu = menu, Position = menu.GlobalPosition })
            .OrderBy(mp => mp.Position.Y)
            .ThenBy(mp => mp.Position.X)
            .ToList();

        const float yTolerance = 50f;
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

        foreach (var row in rows)
        {
            row.Sort((a, b) => a.GlobalPosition.X.CompareTo(b.GlobalPosition.X));
        }

        gridHeight = rows.Count;
        gridWidth = rows.Max(row => row.Count);

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

        foreach (var menu in menuControls)
        {
            if (menu.IsActive)
            {
                if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                    GD.Print($"[InputManager] Deactivating menu: {menu.Name}");
                menu.SetActive(false);
            }
        }

        var oldPosition = currentGridPosition;
        MoveToNextGridPosition();
        EnsureValidGridPosition();

        if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
            GD.Print($"[InputManager] Moved from {oldPosition} to {currentGridPosition}");

        var newMenu = GetMenuAtGridPosition(currentGridPosition);
        if (newMenu != null)
        {
            if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                GD.Print($"[InputManager] Activating menu: {newMenu.Name} at grid position {currentGridPosition}");

            newMenu.SetActive(true);

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

            UpdateCursorTarget();
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

        var currentActiveMenu = currentActiveControl as MenuControls;
        if (currentActiveMenu != null && menuGridPositions.ContainsKey(currentActiveMenu))
        {
            currentGridPosition = menuGridPositions[currentActiveMenu];
            if (enableVerboseDebug && ShouldDebugThisFrame(ref lastVerboseDebugFrame))
                GD.Print($"[InputManager] Maintained focus on {currentActiveMenu.Name} at position {currentGridPosition}");
        }
        else
        {
            currentGridPosition = Vector2I.Zero;
            EnsureValidGridPosition();
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
            newPos = targetCursorPosition;
        }
        else
        {
            var distance = currentPos.DistanceTo(targetCursorPosition);

            if (distance <= SNAP_DISTANCE)
            {
                newPos = targetCursorPosition;
            }
            else
            {
                var direction = (targetCursorPosition - currentPos).Normalized();
                var moveDistance = CURSOR_SPEED * (float)delta;

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

        var focusedButton = GetCurrentlyFocusedButton();

        if (focusedButton != null && focusedButton is Control buttonControl)
        {
            var buttonRect = buttonControl.GetGlobalRect();
            finalPosition = buttonRect.Position + (buttonRect.Size * 0.5f) + uiCursorOffset;
        }
        else
        {
            var currentButton = menu.CurrentButton;
            if (currentButton != null && currentButton is Control fallbackControl)
            {
                var buttonRect = fallbackControl.GetGlobalRect();
                finalPosition = buttonRect.Position + (buttonRect.Size * 0.5f) + uiCursorOffset;
            }
            else if (menu is Control menuControl)
            {
                var menuRect = menuControl.GetGlobalRect();
                finalPosition = menuRect.Position + (menuRect.Size * 0.5f) + uiCursorOffset;
            }
            else
            {
                finalPosition = menu.GlobalPosition + uiCursorOffset;
            }
        }

        if (debugCursorPositioning && ShouldDebugThisFrame(ref lastDebugFrame))
        {
            GD.Print($"[CURSOR] Target: {finalPosition}");
        }

        return finalPosition;
    }

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

    private void InitialCursorPositioning()
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
                    GD.Print($"[CURSOR] Initial positioning: {currentActiveControl.GetType().Name} ({currentActiveControl.Name}) at {targetCursorPosition}");
            }
        }
        else
        {
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
            targetCursorPosition = GetViewport().GetMousePosition();
            if (cursorDisplay != null)
            {
                cursorDisplay.Position = targetCursorPosition.Round() - cursorOffset;
            }

            if (debugCursorPositioning && ShouldDebugThisFrame(ref lastDebugFrame))
                GD.Print("[CURSOR] Delayed initial positioning: No active control, using mouse position");
        }
    }

    private void TestAddButtons()
    {
        GD.Print("=== Testing Button Creation ===");

        if (dynamicMenuRoot == null)
        {
            GD.PrintErr("[InputManager] dynamicMenuRoot is null - assign the Control root node in the editor");
            return;
        }

        GD.Print($"[InputManager] Using Control root: {dynamicMenuRoot.Name}");

        var targetMenuControls = FindMenuControlsInStructure(dynamicMenuRoot);

        if (targetMenuControls == null)
        {
            GD.PrintErr("[InputManager] Could not find MenuControls at expected path: Control/MarginContainer/VBoxContainer");
            return;
        }

        GD.Print($"[InputManager] Found MenuControls: {targetMenuControls.Name}");

        var button1 = targetMenuControls.AddButton("test", "TestButton1");
        GD.Print($"[InputManager] AddButton returned for button1: {(button1 != null ? button1.Name : "NULL")}");

        var button2 = targetMenuControls.AddButton("test2", "TestButton2");
        GD.Print($"[InputManager] AddButton returned for button2: {(button2 != null ? button2.Name : "NULL")}");

        if (button1 != null && button2 != null)
        {
            GD.Print($"[InputManager] ✅ Successfully added 2 buttons to {targetMenuControls.Name}");

            button1.Pressed += () => OnTestButtonPressed("Button 1");
            button2.Pressed += () => OnTestButtonPressed("Button 2");

            GD.Print("[InputManager] Connected button signals for testing");
        }
        else
        {
            GD.PrintErr("[InputManager] ❌ Failed to add buttons - AddButton method returned null");
        }

        GD.Print("=== Button Creation Test Complete ===");
    }

    private void TestDeleteButtons()
    {
        GD.Print("=== Testing Button Deletion ===");

        var targetMenuControls = FindMenuControlsInStructure(dynamicMenuRoot);
        if (targetMenuControls == null)
        {
            GD.PrintErr("[InputManager] No MenuControls found for deletion test");
            return;
        }

        GD.Print($"[InputManager] Testing deletion on MenuControls: {targetMenuControls.Name}");

        // First add some buttons to test deletion
        GD.Print("[InputManager] Adding test buttons for deletion test...");
        var button1 = targetMenuControls.AddButton("DeleteMe1", "TestButton1");
        var button2 = targetMenuControls.AddButton("DeleteMe2", "TestButton2");
        var button3 = targetMenuControls.AddButton("DeleteMe3", "TestButton3");

        if (button1 == null || button2 == null || button3 == null)
        {
            GD.PrintErr("[InputManager] Failed to add test buttons for deletion test");
            return;
        }

        GD.Print("[InputManager] Added 3 test buttons, now testing deletion...");

        // Test deleting by name
        bool deleted1 = targetMenuControls.DeleteButton("TestButton1");
        GD.Print($"[InputManager] Delete 'TestButton1' by name: {(deleted1 ? "SUCCESS" : "FAILED")}");

        // Test deleting by index
        bool deleted2 = targetMenuControls.DeleteButtonAt(0);
        GD.Print($"[InputManager] Delete button at index 0: {(deleted2 ? "SUCCESS" : "FAILED")}");

        // Test clearing all buttons
        targetMenuControls.ClearAllButtons();
        GD.Print("[InputManager] Cleared all remaining buttons");

        GD.Print("=== Button Deletion Test Complete ===");
    }

    private void TestSetButtonArray()
    {
        GD.Print("=== Testing Button Array Creation ===");

        var targetMenuControls = FindMenuControlsInStructure(dynamicMenuRoot);
        if (targetMenuControls == null)
        {
            GD.PrintErr("[InputManager] No MenuControls found for array test");
            return;
        }

        GD.Print($"[InputManager] Testing array creation on MenuControls: {targetMenuControls.Name}");

        // Test with a sample array of button texts
        string[] testButtonTexts = { "Attack", "Defend", "Magic", "Item", "Run Away" };

        GD.Print($"[InputManager] Calling SetButtonsFromArray with {testButtonTexts.Length} buttons...");
        targetMenuControls.SetButtonsFromArray(testButtonTexts);

        GD.Print($"[InputManager] ✅ Set buttons from array with {testButtonTexts.Length} options");

        // Connect signals to test functionality - fixed lambda capture
        var menuButtons = targetMenuControls.GetChildren().OfType<BaseButton>().ToArray();
        GD.Print($"[InputManager] Found {menuButtons.Length} buttons after array creation");

        for (int i = 0; i < menuButtons.Length; i++)
        {
            var button = menuButtons[i];
            var buttonText = testButtonTexts[i];
            var index = i; // Capture the index by value

            // Create a proper closure that captures values, not references
            button.Pressed += () => OnTestButtonPressed($"Array Button [{index}]: {testButtonTexts[index]}");

            GD.Print($"[InputManager] Connected signal for button {index}: '{buttonText}'");
        }

        GD.Print("[InputManager] Connected signals for all array buttons");

        GD.Print("=== Button Array Test Complete ===");
    }

    private MenuControls FindMenuControlsInStructure(Control root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is MarginContainer marginContainer)
            {
                GD.Print($"[InputManager] Found MarginContainer: {marginContainer.Name}");

                foreach (Node grandchild in marginContainer.GetChildren())
                {
                    if (grandchild is MenuControls menuControls)
                    {
                        GD.Print($"[InputManager] Found MenuControls: {menuControls.Name} at expected path");
                        return menuControls;
                    }
                }

                GD.PrintErr("[InputManager] Found MarginContainer but no MenuControls inside");
                GD.PrintErr("[InputManager] Children of MarginContainer:");
                DebugListNodes(marginContainer, "    ");
                break;
            }
        }

        GD.PrintErr("[InputManager] No MarginContainer found as direct child of root");
        return null;
    }

    private void DebugListNodes(Node parent, string indent)
    {
        foreach (Node child in parent.GetChildren())
        {
            GD.Print($"{indent}{child.Name} ({child.GetType().Name})");
            if (child.GetChildCount() > 0 && indent.Length < 8)
            {
                DebugListNodes(child, indent + "  ");
            }
        }
    }

    private void OnTestButtonPressed(string buttonName)
    {
        GD.Print($"[InputManager] Test button pressed: {buttonName}");
    }
}