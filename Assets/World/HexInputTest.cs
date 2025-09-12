// HexInputTest.cs - Simple input testing for HexGrid
using Godot;

public partial class HexInputTest : Node
{
    [Export] private HexGrid hexGrid;
    [Export] private RichTextLabel debugInfo;
    
    private Vector2I currentCursorPos = new Vector2I(5, 5);
    private bool showingMovement = false;
    private bool showingAttack = false;
    
    public override void _Ready()
    {
        if (hexGrid == null)
            hexGrid = GetNode<HexGrid>("../HexGrid");
        if (debugInfo == null)
            debugInfo = GetNode<RichTextLabel>("../DebugInfo");
            
        LogDebug("HexInputTest ready!");
        LogDebug("Controls:");
        LogDebug("- Arrow Keys: Move cursor");
        LogDebug("- M: Toggle movement range (blue)");
        LogDebug("- A: Toggle attack range (red)");
        LogDebug("- S: Show support range (green)");
        LogDebug("- C: Clear all highlights");
        LogDebug("- Click: Move cursor to hex");
        
        // Show initial cursor
        hexGrid.ShowCursor(currentCursorPos);
        UpdateDebugInfo();
    }
    
    public override void _Input(InputEvent @event)
    {
        // Arrow key movement
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            Vector2I moveDir = Vector2I.Zero;
            
            switch (keyEvent.Keycode)
            {
                // Basic movement
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
                    
                // Diagonal movement (for hex grids)
                case Key.Pageup: // Northeast
                    moveDir = new Vector2I(1, -1);
                    break;
                case Key.Pagedown: // Southeast
                    moveDir = new Vector2I(0, 1);
                    break;
                case Key.Home: // Northwest
                    moveDir = new Vector2I(0, -1);
                    break;
                case Key.End: // Southwest
                    moveDir = new Vector2I(-1, 1);
                    break;
                    
                // Test highlighting functions
                case Key.M:
                    ToggleMovementRange();
                    break;
                case Key.A:
                    ToggleAttackRange();
                    break;
                case Key.S:
                    ShowSupportRange();
                    break;
                case Key.C:
                    ClearAll();
                    break;
                    
                // Test different ranges
                case Key.Key1:
                    TestRange(1);
                    break;
                case Key.Key2:
                    TestRange(2);
                    break;
                case Key.Key3:
                    TestRange(3);
                    break;
            }
            
            if (moveDir != Vector2I.Zero)
            {
                MoveCursor(moveDir);
            }
        }
        
        // Mouse input
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
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
                }
            }
        }
    }
    
    private void MoveCursor(Vector2I direction)
    {
        var newPos = currentCursorPos + direction;
        
        if (hexGrid.IsValidHex(newPos))
        {
            currentCursorPos = newPos;
            hexGrid.ShowCursor(currentCursorPos);
            LogDebug($"Cursor moved to: ({newPos.X}, {newPos.Y})");
            UpdateDebugInfo();
            
            // Update any active highlights
            if (showingMovement)
            {
                hexGrid.ShowMovementRange(currentCursorPos, 3);
            }
            if (showingAttack)
            {
                hexGrid.ShowTargetArea(currentCursorPos, 2, HexGrid.HighlightType.Attack);
            }
        }
        else
        {
            LogDebug($"Cannot move to ({newPos.X}, {newPos.Y}) - invalid hex");
        }
    }
    
    private void ToggleMovementRange()
    {
        showingMovement = !showingMovement;
        
        if (showingMovement)
        {
            hexGrid.ShowMovementRange(currentCursorPos, 3);
            LogDebug("Showing movement range (3 hexes)");
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
            LogDebug("Showing attack range (2 hexes)");
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
        LogDebug("Showing support range (1 hex)");
        showingAttack = false;
        UpdateDebugInfo();
    }
    
    private void TestRange(int range)
    {
        hexGrid.ShowMovementRange(currentCursorPos, range);
        LogDebug($"Testing range: {range} hexes");
        showingMovement = true;
        UpdateDebugInfo();
    }
    
    private void ClearAll()
    {
        hexGrid.ClearAllHighlights();
        hexGrid.ShowCursor(currentCursorPos); // Keep cursor visible
        showingMovement = false;
        showingAttack = false;
        LogDebug("Cleared all highlights");
        UpdateDebugInfo();
    }
    
    private void UpdateDebugInfo()
    {
        if (debugInfo == null) return;
        
        debugInfo.Clear();
        debugInfo.AppendText("[b]Hex Grid Test[/b]\n");
        debugInfo.AppendText("================\n");
        debugInfo.AppendText($"[color=yellow]Cursor Position: ({currentCursorPos.X}, {currentCursorPos.Y})[/color]\n");
        debugInfo.AppendText($"World Position: {hexGrid.GetHexWorldPosition(currentCursorPos)}\n");
        debugInfo.AppendText($"Is Valid: {hexGrid.IsValidHex(currentCursorPos)}\n");
        debugInfo.AppendText($"Is Walkable: {hexGrid.IsWalkable(currentCursorPos)}\n");
        debugInfo.AppendText($"Is Occupied: {hexGrid.IsOccupied(currentCursorPos)}\n");
        debugInfo.AppendText("\n[b]Neighbors:[/b]\n");
        
        var neighbors = hexGrid.GetNeighbors(currentCursorPos);
        foreach (var n in neighbors)
        {
            debugInfo.AppendText($"  ({n.X}, {n.Y})\n");
        }
        
        debugInfo.AppendText($"\n[b]Active Highlights:[/b]\n");
        debugInfo.AppendText($"Movement: {(showingMovement ? "ON" : "OFF")}\n");
        debugInfo.AppendText($"Attack: {(showingAttack ? "ON" : "OFF")}\n");
        
        debugInfo.AppendText("\n[b]Controls:[/b]\n");
        debugInfo.AppendText("[color=gray]Arrows: Move cursor\n");
        debugInfo.AppendText("M: Toggle movement\n");
        debugInfo.AppendText("A: Toggle attack\n");
        debugInfo.AppendText("S: Show support\n");
        debugInfo.AppendText("C: Clear all\n");
        debugInfo.AppendText("1-3: Test ranges[/color]\n");
    }
    
    private void LogDebug(string message)
    {
        GD.Print($"[HexTest] {message}");
    }
}