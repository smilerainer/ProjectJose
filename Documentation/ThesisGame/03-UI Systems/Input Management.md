# Input Management

Complete guide to CentralInputManager and context-aware input routing.

---

## Overview

**CentralInputManager.cs** is the single source of truth for input handling. It detects which UI is active and routes input accordingly, preventing conflicts between systems.

**Core Concept**: "One input router to rule them all"

---

## Input Contexts

### Context Enum

```csharp
public enum InputContext
{
    None,      // No active UI
    Menu,      // MenuControls active
    HexGrid,   // HexControls active (targeting)
    Dialogue,  // Dialogic timeline active
    Mixed      // Multiple active (rare)
}
```

### Context Priority

When multiple UIs are active:
```
1. Dialogue (highest - blocks everything)
2. HexGrid (targeting takes precedence)
3. Menu (default UI)
4. None (fallback)
```

---

## Architecture

### Component Structure

```
CentralInputManager
    ├── Control Discovery
    │   ├── Finds MenuControls instances
    │   └── Finds HexControls instances
    │
    ├── Context Detection
    │   ├── Checks Dialogic status
    │   ├── Checks HexControls.IsActive
    │   └── Checks MenuControls.IsActive
    │
    ├── Input Routing
    │   ├── Route to Dialogic (dialogue context)
    │   ├── Route to HexControls (hex context)
    │   └── Route to MenuControls (menu context)
    │
    └── Cursor Management
        ├── Virtual cursor display
        ├── Cursor positioning
        └── Cursor animation
```

---

## Initialization

### _Ready() Sequence

```csharp
public override void _Ready()
{
    ProcessPriority = -10000;  // Process before everything else
    SetProcessInput(true);
    SetProcessUnhandledInput(true);
    
    AutoDiscoverControls();
    SetupDialogic();
    SetupCursorDisplay();
    CallDeferred(nameof(InitialCursorPositioning));
}
```

**Process Priority**: -10000 ensures input is handled first

### Control Discovery

```csharp
private void AutoDiscoverControls()
{
    var sceneRoot = GetTree().CurrentScene;
    if (sceneRoot == null) return;
    
    DiscoverControlsRecursively(sceneRoot);
}

private void DiscoverControlsRecursively(Node node)
{
    switch (node)
    {
        case MenuControls menu:
            RegisterControl(menu);
            break;
        case HexControls hex:
            RegisterControl(hex);
            break;
    }
    
    foreach (Node child in node.GetChildren())
    {
        DiscoverControlsRecursively(child);
    }
}
```

**Discovery Process**:
1. Start at scene root
2. Recursively traverse tree
3. Register any MenuControls or HexControls found
4. Store in internal lists

---

## Context Detection

### UpdateActiveContext()

```csharp
private void UpdateActiveContext()
{
    var oldContext = currentContext;
    var oldControl = currentActiveControl;
    
    DetectActiveControl();
    
    if (currentContext != oldContext || currentActiveControl != oldControl)
    {
        OnContextChanged();
    }
}
```

**Called Every Frame**: Continuously monitors for context changes

### DetectActiveControl()

```csharp
private void DetectActiveControl()
{
    // Priority 1: Check Dialogic
    if (dialogicAvailable && IsDialogicActive())
    {
        if (currentContext != InputContext.Dialogue)
        {
            contextBeforeDialogue = currentContext;
            controlBeforeDialogue = currentActiveControl;
        }
        
        currentContext = InputContext.Dialogue;
        currentActiveControl = null;
        return;
    }
    
    // Priority 2: Find active controls
    var activeMenus = menuControls.Where(m => m.IsActive).ToList();
    var activeHex = hexControls.Where(h => h.IsActive && h.IsInInteractionMode).ToList();
    
    int totalActive = activeMenus.Count + activeHex.Count;
    
    // Priority 3: Determine context
    if (totalActive == 0)
    {
        currentContext = InputContext.None;
        currentActiveControl = null;
    }
    else if (totalActive == 1)
    {
        if (activeMenus.Count > 0)
        {
            currentContext = InputContext.Menu;
            currentActiveControl = activeMenus[0];
        }
        else
        {
            currentContext = InputContext.HexGrid;
            currentActiveControl = activeHex[0];
        }
    }
    else
    {
        // Multiple active: Prioritize HexGrid
        if (activeHex.Count > 0)
        {
            currentContext = InputContext.HexGrid;
            currentActiveControl = activeHex[0];
        }
        else
        {
            currentContext = InputContext.Menu;
            currentActiveControl = activeMenus[0];
        }
    }
}
```

**Critical Check for HexGrid**:
```csharp
h.IsActive && h.IsInInteractionMode
```
Both conditions must be true!

---

## Input Routing

### _Input() Method

```csharp
public override void _Input(InputEvent @event)
{
    // Dialogue context: Block all navigation, allow dialogic actions
    if (currentContext == InputContext.Dialogue)
    {
        if (@event.IsActionPressed("ui_up") || @event.IsActionPressed("ui_down") ||
            @event.IsActionPressed("ui_left") || @event.IsActionPressed("ui_right") ||
            @event.IsActionPressed("ui_accept") || @event.IsActionPressed("ui_cancel"))
        {
            if (!@event.IsActionPressed("dialogic_default_action"))
            {
                GetViewport().SetInputAsHandled();
                return;
            }
        }
        return; // Let Dialogic handle
    }
    
    // Tab cycling disabled (optional feature)
    if (enableTabCycling && @event.IsActionPressed("ui_focus_next"))
    {
        GetViewport().SetInputAsHandled();
        return;
    }
    
    // No context: Ignore input
    if (currentContext == InputContext.None) return;
    
    // Route to appropriate handler
    switch (currentActiveControl)
    {
        case MenuControls menu:
            HandleMenuInput(menu, @event);
            break;
        case HexControls hex:
            // HexControls handles its own input
            break;
    }
}
```

### HandleMenuInput()

```csharp
private void HandleMenuInput(MenuControls menu, InputEvent @event)
{
    // Cancel handling
    if (TryHandleCancel(@event, menu)) return;
    
    // Navigation
    var direction = GetDirectionInput(@event);
    if (direction != Vector2I.Zero)
    {
        menu.Navigate(direction);
        UpdateCursorTarget();
        GetViewport().SetInputAsHandled();
    }
    // Accept
    else if (IsAcceptInput(@event))
    {
        if (isShowingSubmenu && menu == GetDynamicMenu())
        {
            NotifySubmenuSelection(menu);
        }
        else
        {
            menu.ActivateCurrentButton();
        }
        GetViewport().SetInputAsHandled();
    }
}
```

**Key Features**:
- Handles WASD + Arrow keys
- Space/Enter for accept
- Escape for cancel
- Updates cursor after navigation
- Marks input as handled

### Input Detection Methods

```csharp
private Vector2I GetDirectionInput(InputEvent @event)
{
    // UI actions (includes arrow keys)
    if (@event.IsActionPressed("ui_up")) return Vector2I.Up;
    if (@event.IsActionPressed("ui_down")) return Vector2I.Down;
    if (@event.IsActionPressed("ui_left")) return Vector2I.Left;
    if (@event.IsActionPressed("ui_right")) return Vector2I.Right;
    
    // WASD fallback
    if (IsKey(@event, Key.W)) return Vector2I.Up;
    if (IsKey(@event, Key.S)) return Vector2I.Down;
    if (IsKey(@event, Key.A)) return Vector2I.Left;
    if (IsKey(@event, Key.D)) return Vector2I.Right;
    
    return Vector2I.Zero;
}

private bool IsAcceptInput(InputEvent @event)
{
    return @event.IsActionPressed("ui_accept") || 
           IsKey(@event, Key.Space) || 
           IsKey(@event, Key.Enter) || 
           IsKey(@event, Key.KpEnter);
}

private bool IsKey(InputEvent @event, Key key)
{
    return @event is InputEventKey keyEvent && 
           keyEvent.Pressed && 
           keyEvent.Keycode == key;
}
```

---

## Dialogic Integration

### Setup

```csharp
private void SetupDialogic()
{
    dialogicAutoload = GetNodeOrNull("/root/Dialogic");
    
    if (dialogicAutoload != null)
    {
        dialogicAvailable = true;
        
        if (dialogicAutoload.HasSignal("timeline_started"))
        {
            dialogicAutoload.Connect("timeline_started", 
                new Callable(this, nameof(OnDialogicStarted)));
        }
        
        if (dialogicAutoload.HasSignal("timeline_ended"))
        {
            dialogicAutoload.Connect("timeline_ended", 
                new Callable(this, nameof(OnDialogicEnded)));
        }
    }
}
```

### Dialogic State Check

```csharp
private bool IsDialogicActive()
{
    if (dialogicAutoload == null) return false;
    
    var currentTimeline = dialogicAutoload.Get("current_timeline");
    return currentTimeline.Obj != null;
}
```

### Timeline Events

```csharp
private void OnDialogicStarted()
{
    GD.Print("[InputManager] Dialogic timeline started");
    
    if (dialogueContainer != null && IsInstanceValid(dialogueContainer))
    {
        dialogueContainer.Show();
        
        // Show CanvasLayer children
        foreach (Node child in dialogueContainer.GetChildren())
        {
            if (child is CanvasLayer canvasLayer)
                canvasLayer.Show();
        }
    }
}

private void OnDialogicEnded()
{
    GD.Print("[InputManager] Dialogic timeline ended");
    
    if (dialogueContainer != null && IsInstanceValid(dialogueContainer))
    {
        dialogueContainer.Hide();
        
        // Hide CanvasLayer children
        foreach (Node child in dialogueContainer.GetChildren())
        {
            if (child is CanvasLayer canvasLayer)
                canvasLayer.Hide();
        }
    }
}
```

---

## Virtual Cursor System

### Setup

```csharp
private void SetupCursorDisplay()
{
    if (!enableCursor || cursorTexture == null) return;
    
    cursorDisplay = new TextureRect
    {
        Texture = cursorTexture,
        MouseFilter = Control.MouseFilterEnum.Ignore,
        ZIndex = 2000,
        PivotOffset = cursorOffset,
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest
    };
    
    var uiLayer = GetTree().CurrentScene.GetNodeOrNull<CanvasLayer>("UI");
    if (uiLayer != null)
        uiLayer.AddChild(cursorDisplay);
    else
        AddChild(cursorDisplay);
    
    targetCursorPosition = GetViewport().GetMousePosition();
}
```

### Cursor Update Loop

```csharp
private void UpdateCursor(double delta)
{
    if (cursorDisplay == null) return;
    
    UpdateCursorVisibility();
    if (!cursorDisplay.Visible) return;
    
    UpdateCursorTarget();
    MoveCursorToTarget(delta);
}

private void UpdateCursorVisibility()
{
    if (cursorDisplay == null) return;
    
    // Hide during HexGrid or Dialogue
    cursorDisplay.Visible = currentContext != InputContext.HexGrid && 
                            currentContext != InputContext.Dialogue;
}
```

### Cursor Positioning

```csharp
private void UpdateCursorTarget()
{
    targetCursorPosition = currentActiveControl switch
    {
        MenuControls menu => GetMenuCursorPosition(menu),
        HexControls hex => GetHexCursorPosition(hex),
        _ => GetViewport().GetMousePosition()
    };
}

private Vector2 GetMenuCursorPosition(MenuControls menu)
{
    var button = menu.CurrentButton;
    if (button is Control control)
    {
        var rect = control.GetGlobalRect();
        return rect.Position + (rect.Size * 0.5f) + uiCursorOffset;
    }
    return menu.GlobalPosition + uiCursorOffset;
}
```

### Cursor Movement

```csharp
private void MoveCursorToTarget(double delta)
{
    var currentPos = cursorDisplay.Position + cursorOffset;
    Vector2 newPos;
    
    if (enableCursorSnapping)
    {
        // Instant snap
        newPos = targetCursorPosition;
    }
    else
    {
        // Smooth movement
        var distance = currentPos.DistanceTo(targetCursorPosition);
        if (distance <= SNAP_DISTANCE)
        {
            newPos = targetCursorPosition;
        }
        else
        {
            var direction = (targetCursorPosition - currentPos).Normalized();
            var moveDistance = Mathf.Min(CURSOR_SPEED * (float)delta, distance);
            newPos = currentPos + (direction * moveDistance);
        }
    }
    
    cursorDisplay.Position = newPos.Round() - cursorOffset;
}
```

---

## Submenu Management

### Show Submenu

```csharp
public void ShowSubmenu(string[] options, string context)
{
    var dynamicMenu = GetDynamicMenu();
    if (dynamicMenu == null) return;
    
    // Deactivate current menu
    var currentMenu = currentActiveControl as MenuControls;
    if (currentMenu != null && currentMenu != dynamicMenu)
    {
        previousMenu = currentMenu;
        currentMenu.SetActive(false);
    }
    
    // Setup submenu
    isShowingSubmenu = true;
    currentSubmenuContext = context;
    
    dynamicMenu.ClearAllButtons();
    dynamicMenu.SetButtonsFromArray(options);
    dynamicMenu.SetActive(true);
    
    currentActiveControl = dynamicMenu;
    currentContext = InputContext.Menu;
    
    dynamicMenu.ResetToFirstButton();
    UpdateCursorVisibility();
    UpdateCursorTarget();
}
```

### Clear Submenu

```csharp
public void ClearSubmenu(bool restorePreviousMenu = true)
{
    var dynamicMenu = GetDynamicMenu();
    if (dynamicMenu != null)
    {
        dynamicMenu.SetActive(false);
        dynamicMenu.ClearAllButtons();
    }
    
    isShowingSubmenu = false;
    currentSubmenuContext = "";
    
    if (restorePreviousMenu && previousMenu != null)
    {
        previousMenu.SetActive(true);
        currentActiveControl = previousMenu;
        currentContext = InputContext.Menu;
        previousMenu.ResetToFirstButton();
        UpdateCursorTarget();
        previousMenu = null;
    }
    else
    {
        previousMenu = null;
        currentActiveControl = null;
        currentContext = InputContext.None;
    }
}
```

### Dynamic Menu Methods

```csharp
public void SetMenuButtonArray(string[] buttonTexts)
{
    ShowSubmenu(buttonTexts, "dynamic");
}

public void ClearDynamicMenu()
{
    ClearSubmenu(restorePreviousMenu: false);
}
```

---

## Control Registration

### Manual Registration

```csharp
public void RegisterControl(MenuControls menu)
{
    if (!menuControls.Contains(menu))
        menuControls.Add(menu);
}

public void RegisterControl(HexControls hex)
{
    if (!hexControls.Contains(hex))
        hexControls.Add(hex);
}
```

### Rediscover Controls

```csharp
public void RediscoverControls()
{
    AutoDiscoverControls();
    GD.Print($"[InputManager] Rediscovered controls - Menus: {menuControls.Count}, Hex: {hexControls.Count}");
}
```

**Use Case**: After dynamic scene loading

---

## Exported Properties

```csharp
[Export] private Texture2D cursorTexture;
[Export] private Vector2 cursorOffset = Vector2.Zero;
[Export] private Vector2 uiCursorOffset = Vector2.Zero;
[Export] private bool enableCursor = true;
[Export] private bool enableCursorSnapping = true;
[Export] private Control dynamicMenuRoot;
[Export] private Control dialogueContainer;
[Export] private bool enableTabCycling = false;
```

**Set in Editor**:
- Cursor texture and offsets
- Dynamic menu root reference
- Dialogue container reference
- Feature toggles

---

## Common Patterns

### Adding New Input Context

```csharp
// 1. Add to enum
public enum InputContext
{
    None,
    Menu,
    HexGrid,
    Dialogue,
    Inventory,  // ← New context
    Mixed
}

// 2. Add detection logic
private void DetectActiveControl()
{
    // Check for inventory
    if (inventoryUI != null && inventoryUI.IsActive)
    {
        currentContext = InputContext.Inventory;
        currentActiveControl = inventoryUI;
        return;
    }
    
    // Existing logic...
}

// 3. Add input handling
public override void _Input(InputEvent @event)
{
    switch (currentActiveControl)
    {
        case InventoryUI inventory:
            HandleInventoryInput(inventory, @event);
            break;
        // Existing cases...
    }
}
```

### Disabling Input Temporarily

```csharp
// Disable all input
SetProcessInput(false);

// Re-enable
SetProcessInput(true);
```

---

## Troubleshooting

### Input Not Working

**Problem**: Controls not responding

**Checks**:
```csharp
// Is input processing enabled?
GD.Print(GetProcessInput());  // Should be true

// Is context detected?
GD.Print(currentContext);  // Should not be None

// Is control active?
GD.Print(menuControl.IsActive);  // Should be true

// For HexGrid:
GD.Print(hexControl.IsActive);  // Both should be true
GD.Print(hexControl.IsInInteractionMode);
```

### Wrong Context Detected

**Problem**: Input goes to wrong system

**Debug**:
```csharp
GD.Print($"Context: {currentContext}");
GD.Print($"Active Control: {currentActiveControl?.GetType().Name}");
GD.Print($"Active Menus: {menuControls.Count(m => m.IsActive)}");
GD.Print($"Active Hex: {hexControls.Count(h => h.IsActive)}");
```

### Cursor Not Visible

**Problem**: Virtual cursor doesn't appear

**Checks**:
```csharp
// Is cursor enabled?
GD.Print(enableCursor);

// Is cursor created?
GD.Print(cursorDisplay != null);

// Is cursor visible?
GD.Print(cursorDisplay.Visible);

// Current context?
GD.Print(currentContext);  // Hidden during HexGrid/Dialogue
```

### Dialogic Blocking Input

**Problem**: Can't navigate after dialogue

**Solution**: Verify dialogue container visibility management:
```csharp
// After timeline ends
dialogueContainer.Visible = false;  // Should be set
```

---

## Performance Considerations

### Process Priority

```csharp
ProcessPriority = -10000;
```

**Why**: Ensures input processed before other nodes

### Efficient Context Detection

```csharp
// ✅ Good: Cache active controls
var activeMenus = menuControls.Where(m => m.IsActive).ToList();

// ❌ Bad: Query every check
if (menuControls.Any(m => m.IsActive))
```

### Minimal Cursor Updates

```csharp
// Only update when visible
if (!cursorDisplay.Visible) return;
```

---

## Integration Points

### With MenuControls

```csharp
// CentralInputManager detects
if (menu.IsActive)
    currentContext = InputContext.Menu;

// Routes input
menu.Navigate(direction);
menu.ActivateCurrentButton();

// Cursor follows
UpdateCursorTarget();
```

### With HexControls

```csharp
// Detection requires both flags
if (hex.IsActive && hex.IsInInteractionMode)
    currentContext = InputContext.HexGrid;

// HexControls handles own input
// CentralInputManager just tracks context
```

### With BattleManager

```csharp
// BattleManager uses helper methods
SetMenuButtonArray(options);
ClearDynamicMenu();

// For submenu notifications
DynamicMenuSelection signal
```

---

## Related Documentation

- [[Menu System]] - MenuControls details
- [[HexControls]] - Hex targeting UI
- [[Dialogic Integration]] - VN system
- [[Scene Hierarchy]] - UI structure
- [[Component Reference#CentralInputManager]] - API reference