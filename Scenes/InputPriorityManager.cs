// InputPriorityManager.cs - Central input routing with strict priority
using Godot;
using System.Collections.Generic;

public partial class InputPriorityManager : Node
{
    [Signal] public delegate void TileSelectedEventHandler(Vector2I tileCoord);
    [Signal] public delegate void TileCursorMovedEventHandler(Vector2I tileCoord);
    
    [Export] private HexGrid hexGrid;
    [Export] private Node menuContainer; // Parent of all menus
    [Export] private bool debugMode = true;
    
    private Vector2I cursorPosition = new Vector2I(5, 5);
    private bool inputLocked = false;
    
    // Singleton pattern for global access
    private static InputPriorityManager instance;
    public static InputPriorityManager Instance => instance;
    
    public override void _Ready()
    {
        instance = this;
        
        // Set high priority to intercept all input first
        ProcessPriority = -1000;
        SetProcessInput(true);
        SetProcessUnhandledInput(false); // We use _Input for priority
        
        if (hexGrid != null)
        {
            hexGrid.ShowCursor(cursorPosition);
        }
        
        LogDebug("InputPriorityManager initialized");
    }
    
    public override void _Input(InputEvent @event)
    {
        // Check if ANY menu has focus
        if (IsAnyMenuActive())
        {
            // Let the menu system handle it
            // We don't mark as handled so menu can process it
            LogDebug("Menu active - passing input through");
            return;
        }
        
        // No menu active - handle world input
        if (!inputLocked)
        {
            HandleWorldInput(@event);
        }
    }
    
    private bool IsAnyMenuActive()
    {
        // Check the static menu tree in Menu class
        var focusOwner = GetViewport().GuiGetFocusOwner();
        
        // If any Control has focus, it's likely a menu
        if (focusOwner != null && focusOwner is BaseButton)
        {
            LogDebug($"Focus owner: {focusOwner.Name}");
            return true;
        }
        
        // Check all Menu nodes
        if (menuContainer != null)
        {
            foreach (var child in menuContainer.GetChildren())
            {
                if (child is Menu menu && menu.Visible && menu.MenuIsFocused())
                {
                    LogDebug($"Menu {menu.Name} has focus");
                    return true;
                }
            }
        }
        
        // Also check the entire tree for any visible Menu
        foreach (var menu in GetTree().GetNodesInGroup("menus"))
        {
            if (menu is Menu m && m.Visible && m.MenuIsFocused())
            {
                LogDebug($"Menu {m.Name} has focus (from group)");
                return true;
            }
        }
        
        return false;
    }
    
    private void HandleWorldInput(InputEvent @event)
    {
        if (hexGrid == null) return;
        
        // Only handle world input when no menu is active
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            Vector2I moveDir = Vector2I.Zero;
            bool handled = false;
            
            switch (keyEvent.Keycode)
            {
                // Movement
                case Key.Right:
                case Key.D:
                    moveDir = new Vector2I(1, 0);
                    handled = true;
                    break;
                case Key.Left:
                case Key.A:
                    moveDir = new Vector2I(-1, 0);
                    handled = true;
                    break;
                case Key.Down:
                case Key.S:
                    moveDir = new Vector2I(0, 1);
                    handled = true;
                    break;
                case Key.Up:
                case Key.W:
                    moveDir = new Vector2I(0, -1);
                    handled = true;
                    break;
                    
                // Selection
                case Key.Enter:
                case Key.KpEnter:
                case Key.Space:
                    SelectTile();
                    handled = true;
                    break;
            }
            
            if (moveDir != Vector2I.Zero)
            {
                MoveCursor(moveDir);
            }
            
            if (handled)
            {
                GetViewport().SetInputAsHandled();
            }
        }
        
        // Mouse input
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                var worldPos = GetGlobalMousePosition();
                var tileCoord = hexGrid.GetHexFromWorld(worldPos);
                
                if (hexGrid.IsValidHex(tileCoord))
                {
                    cursorPosition = tileCoord;
                    hexGrid.ShowCursor(cursorPosition);
                    SelectTile();
                    GetViewport().SetInputAsHandled();
                }
            }
        }
    }
    
    private Vector2 GetGlobalMousePosition()
    {
        var viewport = GetViewport();
        var camera = viewport.GetCamera2D();
        
        if (camera != null)
            return camera.GetGlobalMousePosition();
            
        return viewport.GetScreenTransform().Inverse() * viewport.GetMousePosition();
    }
    
    private void MoveCursor(Vector2I direction)
    {
        var newPos = cursorPosition + direction;
        
        if (hexGrid.IsValidHex(newPos))
        {
            cursorPosition = newPos;
            hexGrid.ShowCursor(cursorPosition);
            EmitSignal(SignalName.TileCursorMoved, cursorPosition);
            LogDebug($"Cursor moved to ({cursorPosition.X}, {cursorPosition.Y})");
        }
    }
    
    private void SelectTile()
    {
        LogDebug($"Tile selected: ({cursorPosition.X}, {cursorPosition.Y})");
        EmitSignal(SignalName.TileSelected, cursorPosition);
    }
    
    // Public API
    public void LockInput(bool locked)
    {
        inputLocked = locked;
        LogDebug($"Input locked: {locked}");
    }
    
    public bool IsInputLocked() => inputLocked || IsAnyMenuActive();
    
    public Vector2I GetCursorPosition() => cursorPosition;
    
    private void LogDebug(string message)
    {
        if (debugMode)
            GD.Print($"[InputPriority] {message}");
    }
}

// HexCursorController.cs - Simplified hex cursor that doesn't compete for input
public partial class HexCursorController : Node
{
    [Export] private HexGrid hexGrid;
    [Export] private InputPriorityManager inputManager;
    
    private Entity selectedEntity;
    private List<Vector2I> validMoves = new();
    private List<Vector2I> validTargets = new();
    
    public override void _Ready()
    {
        if (inputManager == null)
            inputManager = InputPriorityManager.Instance;
            
        if (inputManager != null)
        {
            inputManager.TileSelected += OnTileSelected;
            inputManager.TileCursorMoved += OnCursorMoved;
        }
    }
    
    private void OnTileSelected(Vector2I tile)
    {
        GD.Print($"[HexCursor] Processing tile selection: {tile}");
        
        // Check what's at this tile
        if (validMoves.Contains(tile))
        {
            // Execute move
            MoveSelectedEntity(tile);
        }
        else if (validTargets.Contains(tile))
        {
            // Execute attack/skill
            TargetSelected(tile);
        }
        else
        {
            // Just selection
            CheckTileContents(tile);
        }
    }
    
    private void OnCursorMoved(Vector2I tile)
    {
        // Update any preview effects
    }
    
    public void ShowMovementRange(Entity entity, int range)
    {
        selectedEntity = entity;
        var origin = new Vector2I(entity.CoordinateX, entity.CoordinateY);
        validMoves = hexGrid.GetReachableHexes(origin, range);
        
        hexGrid.ClearAllHighlights();
		foreach (var move in validMoves)
		{
			if (move != origin) { 
			// hexGrid.DrawHighlight(move, new Color(0, 0.5f, 1, 0.3f));
			}
        }
    }
    
    public void ShowTargetRange(Vector2I origin, int range, HexGrid.HighlightType type)
    {
        validTargets = hexGrid.GetHexesInRange(origin, range);
        
        hexGrid.ClearTargetHighlights();
        hexGrid.ShowTargetArea(origin, range, type);
    }
    
    public void ClearAllHighlights()
    {
        validMoves.Clear();
        validTargets.Clear();
        hexGrid.ClearAllHighlights();
    }
    
    private void MoveSelectedEntity(Vector2I target)
    {
        if (selectedEntity == null) return;
        
        GD.Print($"Moving {selectedEntity.Name} to {target}");
        hexGrid.SetOccupied(new Vector2I(selectedEntity.CoordinateX, selectedEntity.CoordinateY), false);
        selectedEntity.CoordinateX = target.X;
        selectedEntity.CoordinateY = target.Y;
        hexGrid.SetOccupied(target, true);
        
        ClearAllHighlights();
    }
    
    private void TargetSelected(Vector2I target)
    {
        GD.Print($"Target selected at {target}");
        // Emit signal or call method to handle skill execution
        ClearAllHighlights();
    }
    
    private void CheckTileContents(Vector2I tile)
    {
        if (hexGrid.IsOccupied(tile))
        {
            GD.Print($"Tile {tile} is occupied");
            // Could show entity info or menu
        }
    }
}

// MenuCoordinator.cs - Ensures menus properly communicate their state
public partial class MenuCoordinator : Node
{
    [Export] private Menu skillMenu;
    [Export] private Menu itemMenu;
    [Export] private Menu actionMenu;
    [Export] private HexCursorController hexCursor;
    
    private Menu activeMenu;
    
    public override void _Ready()
    {
        ConnectMenu(skillMenu);
        ConnectMenu(itemMenu);
        ConnectMenu(actionMenu);
    }
    
    private void ConnectMenu(Menu menu)
    {
        if (menu == null) return;
        
        menu.Activated += () => OnMenuActivated(menu);
        menu.Closed += () => OnMenuClosed(menu);
        menu.ButtonPressed += (button, index) => OnMenuAction(menu, button, index);
        
        // Add to group for easy finding
        menu.AddToGroup("menus");
    }
    
    private void OnMenuActivated(Menu menu)
    {
        activeMenu = menu;
        GD.Print($"[Coordinator] Menu activated: {menu.Name}");
        
        // Ensure input manager knows
        InputPriorityManager.Instance?.LockInput(false); // Let the check handle it
    }
    
    private void OnMenuClosed(Menu menu)
    {
        if (activeMenu == menu)
            activeMenu = null;
            
        GD.Print($"[Coordinator] Menu closed: {menu.Name}");
    }
    
    private void OnMenuAction(Menu menu, BaseButton button, int index)
    {
        // Use button.Name since BaseButton doesn't have Text property
        string buttonName = button.Name;
        
        // Try to get text if it's a Button type
        string buttonText = buttonName;
        if (button is Button btn)
        {
            buttonText = btn.Text;
        }
        else if (button is LinkButton linkBtn)
        {
            buttonText = linkBtn.Text;
        }
        
        GD.Print($"[Coordinator] Action from {menu.Name}: {buttonText}");
        
        // Route to appropriate handler
        switch (menu.Name)
        {
            case "SkillMenu":
                HandleSkillSelection(buttonText, index);
                break;
            case "ActionMenu":
                HandleActionSelection(buttonText, index);
                break;
        }
    }
    
    private void HandleSkillSelection(string skillName, int index)
    {
        // Close menu
        activeMenu?.Close();
        
        // Start targeting
        if (hexCursor != null)
        {
            hexCursor.ShowTargetRange(
                InputPriorityManager.Instance.GetCursorPosition(),
                3, // range
                HexGrid.HighlightType.Attack
            );
        }
    }
    
    private void HandleActionSelection(string actionName, int index)
    {
        switch (actionName)  // Fixed: was 'action', should be 'actionName'
        {
            case "Move":
                // Show movement range
                break;
            case "Attack":
                // Show attack range
                break;
            case "Items":
                // Open item menu
                itemMenu?.ButtonFocus(0);
                break;
            case "End Turn":
                // End turn
                break;
        }
    }
}