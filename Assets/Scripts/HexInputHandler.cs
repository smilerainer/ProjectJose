// HexInputHandler.cs - Handles hex grid input through the layer system
using Godot;

public partial class HexInputHandler : Node, IInputHandler
{
    [Export] private HexGrid hexGrid;
    [Export] private RichTextLabel debugInfo;
    
    private Vector2I currentCursorPos = new Vector2I(5, 5);
    private bool showingMovement = false;
    private bool showingAttack = false;
    private bool isActive = false;
    
    public override void _Ready()
    {
        if (hexGrid == null)
            hexGrid = GetNode<HexGrid>("../HexGrid");
        if (debugInfo == null)
            debugInfo = GetNodeOrNull<RichTextLabel>("../DebugInfo");
            
        // Register with the input layer manager
        if (InputLayerManager.Instance != null)
        {
            InputLayerManager.Instance.RegisterHandler(InputLayerManager.InputLayer.HexGrid, this);
        }
        else
        {
            // If manager isn't ready yet, try again next frame
            CallDeferred(nameof(RegisterDelayed));
        }
        
        LogDebug("HexInputHandler ready!");
        LogDebug("Controls:");
        LogDebug("- Arrow Keys: Move cursor");
        LogDebug("- M: Toggle movement range");
        LogDebug("- A: Toggle attack range");  
        LogDebug("- S: Show support range");
        LogDebug("- C: Clear highlights");
        LogDebug("- Enter/Space: Show options menu");
        
        // Show initial cursor
        hexGrid?.ShowCursor(currentCursorPos);
        UpdateDebugInfo();
    }
    
    private void RegisterDelayed()
    {
        InputLayerManager.Instance?.RegisterHandler(InputLayerManager.InputLayer.HexGrid, this);
    }
    
    public bool HandleInput(InputEvent inputEvent)
    {
        if (!isActive) return false;
        
        if (inputEvent is InputEventKey keyEvent && keyEvent.Pressed)
        {
            return HandleKeyInput(keyEvent);
        }
        
        if (inputEvent is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            return HandleMouseInput(mouseEvent);
        }
        
        return false;
    }
    
    private bool HandleKeyInput(InputEventKey keyEvent)
    {
        Vector2I moveDir = Vector2I.Zero;
        
        switch (keyEvent.Keycode)
        {
            // Movement
            case Key.Right:
                moveDir = new Vector2I(1, 0);
                break;
            case Key.Left:
                moveDir = new Vector2I(-1, 0);
                break;
            case Key.Down:
                moveDir = new Vector2I(0, 1);
                break;
            case Key.Up:
                moveDir = new Vector2I(0, -1);
                break;
                
            // Diagonal movement for hex
            case Key.Pageup:
                moveDir = new Vector2I(1, -1);
                break;
            case Key.Pagedown:
                moveDir = new Vector2I(0, 1);
                break;
            case Key.Home:
                moveDir = new Vector2I(0, -1);
                break;
            case Key.End:
                moveDir = new Vector2I(-1, 1);
                break;
                
            // Actions
            case Key.M:
                ToggleMovementRange();
                return true;
            case Key.A:
                ToggleAttackRange();
                return true;
            case Key.S:
                ShowSupportRange();
                return true;
            case Key.C:
                ClearAll();
                return true;
                
            // Show options menu
            case Key.Enter:
            case Key.Space:
                ShowOptionsMenu();
                return true;
                
            // Test ranges
            case Key.Key1:
                TestRange(1);
                return true;
            case Key.Key2:
                TestRange(2);
                return true;
            case Key.Key3:
                TestRange(3);
                return true;
        }
        
        if (moveDir != Vector2I.Zero)
        {
            return MoveCursor(moveDir);
        }
        
        return false;
    }
    
    private bool HandleMouseInput(InputEventMouseButton mouseEvent)
    {
        if (mouseEvent.ButtonIndex == MouseButton.Left)
        {
            var worldPos = GetViewport().GetScreenTransform().Inverse() * mouseEvent.Position;
            var hexCoord = hexGrid.GetHexFromWorld(worldPos);
            
            if (hexGrid.IsValidHex(hexCoord))
            {
                currentCursorPos = hexCoord;
                hexGrid.ShowCursor(currentCursorPos);
                LogDebug($"Cursor moved to: ({hexCoord.X}, {hexCoord.Y})");
                UpdateDebugInfo();
                return true;
            }
        }
        else if (mouseEvent.ButtonIndex == MouseButton.Right)
        {
            ShowOptionsMenu();
            return true;
        }
        
        return false;
    }
    
    public void SetActive(bool active)
    {
        isActive = active;
        LogDebug($"Set active: {active}");
        
        if (active)
        {
            // Refresh cursor visibility when becoming active
            hexGrid?.ShowCursor(currentCursorPos);
        }
    }
    
    private bool MoveCursor(Vector2I direction)
    {
        var newPos = currentCursorPos + direction;
        
        if (hexGrid.IsValidHex(newPos))
        {
            currentCursorPos = newPos;
            hexGrid.ShowCursor(currentCursorPos);
            LogDebug($"Cursor moved to: ({newPos.X}, {newPos.Y})");
            UpdateDebugInfo();
            
            // Update highlights if active
            if (showingMovement)
                hexGrid.ShowMovementRange(currentCursorPos, 3);
            if (showingAttack)
                hexGrid.ShowTargetArea(currentCursorPos, 2, HexGrid.HighlightType.Attack);
                
            return true;
        }
        
        LogDebug($"Cannot move to ({newPos.X}, {newPos.Y}) - invalid hex");
        return false;
    }
    
    private void ShowOptionsMenu()
    {
        LogDebug("Showing options menu");
        InputLayerManager.Instance?.ShowPopupMenu();
        
        // You can emit a signal here to tell the UI to show the popup
        // GetTree().CallGroup("battle_ui", "show_options_popup", currentCursorPos);
    }
    
    private void ToggleMovementRange()
    {
        showingMovement = !showingMovement;
        
        if (showingMovement)
        {
            hexGrid.ShowMovementRange(currentCursorPos, 3);
            LogDebug("Showing movement range");
        }
        else
        {
            hexGrid.ClearMovementHighlights();
            LogDebug("Hiding movement range");
        }
        UpdateDebugInfo();
    }
    
    private void ToggleAttackRange()
    {
        showingAttack = !showingAttack;
        
        if (showingAttack)
        {
            hexGrid.ShowTargetArea(currentCursorPos, 2, HexGrid.HighlightType.Attack);
            LogDebug("Showing attack range");
        }
        else
        {
            hexGrid.ClearTargetHighlights();
            LogDebug("Hiding attack range");
        }
        UpdateDebugInfo();
    }
    
    private void ShowSupportRange()
    {
        hexGrid.ShowTargetArea(currentCursorPos, 1, HexGrid.HighlightType.Support);
        LogDebug("Showing support range");
        showingAttack = false;
        UpdateDebugInfo();
    }
    
    private void TestRange(int range)
    {
        hexGrid.ShowMovementRange(currentCursorPos, range);
        LogDebug($"Testing range: {range}");
        showingMovement = true;
        UpdateDebugInfo();
    }
    
    private void ClearAll()
    {
        hexGrid.ClearAllHighlights();
        hexGrid.ShowCursor(currentCursorPos);
        showingMovement = false;
        showingAttack = false;
        LogDebug("Cleared all highlights");
        UpdateDebugInfo();
    }
    
    private void UpdateDebugInfo()
    {
        if (debugInfo == null) return;
        
        debugInfo.Clear();
        debugInfo.AppendText("[b]Hex Grid[/b]\n");
        debugInfo.AppendText($"Active: {(isActive ? "[color=green]YES[/color]" : "[color=red]NO[/color]")}\n");
        debugInfo.AppendText($"Layer: {InputLayerManager.Instance?.CurrentLayer}\n");
        debugInfo.AppendText("================\n");
        debugInfo.AppendText($"[color=yellow]Cursor: ({currentCursorPos.X}, {currentCursorPos.Y})[/color]\n");
        debugInfo.AppendText($"World: {hexGrid?.GetHexWorldPosition(currentCursorPos)}\n");
        debugInfo.AppendText($"Valid: {hexGrid?.IsValidHex(currentCursorPos)}\n");
        debugInfo.AppendText($"Walkable: {hexGrid?.IsWalkable(currentCursorPos)}\n");
        debugInfo.AppendText($"Occupied: {hexGrid?.IsOccupied(currentCursorPos)}\n");
        
        debugInfo.AppendText($"\n[b]Highlights:[/b]\n");
        debugInfo.AppendText($"Movement: {(showingMovement ? "ON" : "OFF")}\n");
        debugInfo.AppendText($"Attack: {(showingAttack ? "ON" : "OFF")}\n");
    }
    
    private void LogDebug(string message)
    {
        GD.Print($"[HexInput] {message}");
    }
    
    public override void _ExitTree()
    {
        InputLayerManager.Instance?.UnregisterHandler(InputLayerManager.InputLayer.HexGrid);
    }
    
    // Public accessors for other systems
    public Vector2I GetCursorPosition() => currentCursorPos;
    public HexGrid GetHexGrid() => hexGrid;
}