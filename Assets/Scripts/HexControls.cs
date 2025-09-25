// HexControls.cs - Hex input handling with proper battle mode support
using Godot;
using System.Linq;
using System.Collections.Generic;

public partial class HexControls : Node2D
{
    [Signal] public delegate void CursorMovedEventHandler(Vector2I coord);
    [Signal] public delegate void CursorActivatedEventHandler(Vector2I coord);

    [Export] private TileMapLayer cursorLayer;
    [Export] private int hoverCursorTileId = 0;  // Debug: follows mouse
    [Export] private int clickCursorTileId = 1;  // Debug: shows clicked position
    [Export] private Camera2D camera;
    [Export] private float cameraSpeed = 8f;
    [Export] private bool instantCameraMove = false;
    [Export] public bool enableDebugHover = false; // Debug mode toggle
    [Export] public bool enableDebugWASD = false; // Debug: WASD moves cursor/camera
    [Export] public bool enableCoordinatePrinting = false; // Debug: Print coordinates

    private Vector2I cursorPosition = Vector2I.Zero;  // Current cursor position
    private Vector2I hoverPosition = Vector2I.Zero;   // Mouse hover position
    private bool isActive = false;
    private bool followWithCamera = true;
    private bool movementModeActive = false;
    private HashSet<Vector2I> validMoves = new();
    private HexGrid hexGrid;
    private Tween cameraTween;

    public Vector2I CursorPosition => cursorPosition;
    public bool IsActive => isActive;

    public override void _Ready()
    {
        InitializeComponents();
        SetActive(false); // Start inactive - BattleManager will control activation
    }

    public override void _Input(InputEvent @event)
    {
        // Skip if not active or if CentralInputManager is handling menu input
        if (!isActive) return;
        
        var inputManager = GetViewport().GetChildren().OfType<CentralInputManager>().FirstOrDefault();
        if (inputManager != null && inputManager.CurrentContext == CentralInputManager.InputContext.Menu)
        {
            return; // Let menu handle its input
        }
        
        // Handle movement mode input
        if (movementModeActive)
        {
            HandleMovementModeInput(@event);
        }
        else
        {
            HandleNormalInput(@event);
        }
    }

    #region Public API
    
    public void SetActive(bool active)
    {
        isActive = active;
        if (!active)
            HideCursor();
        else
            ShowCursor();
    }

    public void MoveCursor(Vector2I coord)
    {
        if (!isActive) return;

        cursorPosition = coord;
        UpdateCursorVisual();
        UpdateCameraPosition();
        EmitSignal(SignalName.CursorMoved, coord);
    }

    public Vector2 GetCursorWorldPosition() => HexToWorld(cursorPosition);

    public Vector2I WorldToHex(Vector2 globalMousePos)
    {
        if (cursorLayer == null || camera == null) return Vector2I.Zero;

        var viewport = GetViewport();
        var viewportSize = viewport.GetVisibleRect().Size;
        var worldPos = camera.GlobalPosition + (globalMousePos - viewportSize * 0.5f);
        var localPos = cursorLayer.ToLocal(worldPos);
        var hexCoord = cursorLayer.LocalToMap(localPos);

        return hexCoord;
    }

    public void SetCameraLocked(bool locked)
    {
        followWithCamera = !locked;
        GD.Print("[HexControls] Camera " + (locked ? "locked" : "free"));
    }

    public void FocusOnPosition(Vector2I position)
    {
        MoveCursor(position);
        GD.Print($"[HexControls] Focused camera on {position}");
    }

    public void EnterMovementMode()
    {
        movementModeActive = true;
        SetCameraLocked(false); // Enable free camera
        SetActive(true); // Enable hex interactions
        cursorPosition = GetPlayerPosition(); // Start cursor at player
        GD.Print($"[HexControls] Entered movement mode - cursor at {cursorPosition}, validMoves count: {validMoves.Count}");
        UpdateCursorVisual();
    }

    public void ExitMovementMode(Vector2I focusPosition)
    {
        movementModeActive = false;
        validMoves.Clear();
        SetActive(false); // Disable hex interactions
        FocusOnPosition(focusPosition); // Focus on final position
        SetCameraLocked(true); // Lock camera
        GD.Print($"[HexControls] Exited movement mode - camera locked on {focusPosition}");
    }

    public void StartUIOnlyMode(Vector2I focusPosition)
    {
        movementModeActive = false;
        SetActive(false); // Disable hex interactions
        SetCameraLocked(true); // Lock camera
        FocusOnPosition(focusPosition); // Focus camera
        GD.Print($"[HexControls] UI-only mode - camera locked on {focusPosition}");
    }

    public void SetValidMoves(HashSet<Vector2I> moves)
    {
        validMoves = moves;
        GD.Print($"[HexControls] SetValidMoves called with {moves.Count} moves: [{string.Join(", ", moves)}]");
    }
    
    #endregion

    #region Movement Mode Input Handling
    
    private void HandleMovementModeInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            Vector2I direction = Vector2I.Zero;
            
            switch (keyEvent.Keycode)
            {
                case Key.W: direction = new Vector2I(0, -1); break;
                case Key.S: direction = new Vector2I(0, 1); break;
                case Key.A: direction = new Vector2I(-1, 0); break;
                case Key.D: direction = new Vector2I(1, 0); break;
                case Key.Space:
                case Key.Enter:
                    ConfirmMove();
                    return;
                case Key.Escape:
                    CancelMovement();
                    return;
            }
            
            if (direction != Vector2I.Zero)
            {
                MoveMovementCursor(direction);
            }
        }
        else if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (!IsMouseOverUI(mouseButton.GlobalPosition))
                {
                    var clickedCell = WorldToHex(mouseButton.GlobalPosition);
                    if (validMoves.Contains(clickedCell))
                    {
                        cursorPosition = clickedCell;
                        ConfirmMove();
                    }
                    else
                    {
                        GD.Print($"[HexControls] Invalid move target: {clickedCell}");
                    }
                }
            }
        }
    }
    
    private void MoveMovementCursor(Vector2I direction)
    {
        var newPos = cursorPosition + direction;
        
        // Check if the new position is adjacent to current cursor position
        if (!IsAdjacent(cursorPosition, newPos))
        {
            GD.Print($"[HexControls] Blocked - {newPos} not adjacent to {cursorPosition}");
            return;
        }
        
        var playerPos = GetPlayerPosition();
        
        // Allow moving to player position or any valid move
        if (newPos == playerPos || validMoves.Contains(newPos))
        {
            cursorPosition = newPos;
            UpdateCursorVisual();
            UpdateCameraPosition();
            GD.Print($"[HexControls] Movement cursor moved to {cursorPosition}");
        }
        else
        {
            GD.Print($"[HexControls] Blocked cursor move to {newPos} - Player at {playerPos}, ValidMoves: [{string.Join(", ", validMoves)}]");
        }
    }
    
    private void ConfirmMove()
    {
        var playerPos = GetPlayerPosition();
        
        if (cursorPosition == playerPos)
        {
            GD.Print($"[HexControls] Cannot move to current player position {cursorPosition}");
            return;
        }
        
        if (validMoves.Contains(cursorPosition))
        {
            // Notify HexGrid about the selection (which will trigger BattleManager)
            if (hexGrid != null)
            {
                hexGrid.Select(cursorPosition);
            }
        }
        else
        {
            GD.Print($"[HexControls] Cannot confirm move to {cursorPosition} - invalid position");
        }
    }
    
    private void CancelMovement()
    {
        // Move cursor back to player and exit movement mode
        var playerPos = GetPlayerPosition();
        cursorPosition = playerPos;
        UpdateCursorVisual();
        
        // This should trigger BattleManager to exit movement mode
        // For now, we'll just print - BattleManager should handle this via escape key
        GD.Print("[HexControls] Movement cancelled");
    }
    
    #endregion

    #region Normal Input Handling
    
    private void HandleNormalInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left && !IsMouseOverUI(mouseButton.GlobalPosition))
            {
                var coord = WorldToHex(mouseButton.GlobalPosition);
                if (enableCoordinatePrinting)
                    GD.Print($"[HexControls] Normal click -> {coord}");
                MoveCursor(coord);
                
                // Notify HexGrid about selection
                if (hexGrid != null)
                {
                    hexGrid.Select(coord);
                }
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && enableDebugHover)
        {
            if (!IsMouseOverUI(mouseMotion.GlobalPosition))
            {
                var coord = WorldToHex(mouseMotion.GlobalPosition);
                if (coord != hoverPosition)
                {
                    hoverPosition = coord;
                    UpdateCursorVisual();
                }
            }
        }
        
        // Debug WASD movement
        if (enableDebugWASD && @event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            Vector2I direction = Vector2I.Zero;
            
            switch (keyEvent.Keycode)
            {
                case Key.W: direction = new Vector2I(0, -1); break;
                case Key.S: direction = new Vector2I(0, 1); break;
                case Key.A: direction = new Vector2I(-1, 0); break;
                case Key.D: direction = new Vector2I(1, 0); break;
                case Key.Space: ActivateCursor(); return;
            }
            
            if (direction != Vector2I.Zero)
            {
                MoveCursor(cursorPosition + direction);
            }
        }
    }
    
    #endregion

    #region Helper Methods
    
    private Vector2I GetPlayerPosition()
    {
        // Try to find player position from HexGrid entity layer
        if (hexGrid == null) return Vector2I.Zero;
        
        var entityLayer = hexGrid.GetLayer(CellLayer.Entity);
        if (entityLayer == null) return Vector2I.Zero;
        
        // This is a simple search - in a real game you'd track this more efficiently
        for (int x = -10; x <= 10; x++)
        {
            for (int y = -10; y <= 10; y++)
            {
                var cell = new Vector2I(x, y);
                var tileData = entityLayer.GetCellTileData(cell);
                if (tileData != null)
                {
                    var sourceId = entityLayer.GetCellSourceId(cell);
                    var atlasCoords = entityLayer.GetCellAtlasCoords(cell);
                    // Assuming player is at atlas coords (0,0) - yellow tile
                    if (atlasCoords == Vector2I.Zero)
                    {
                        return cell;
                    }
                }
            }
        }
        
        return Vector2I.Zero; // Fallback
    }
    
    private List<Vector2I> GetHexNeighbors(Vector2I center)
    {
        var neighbors = new List<Vector2I>();
        
        // For VERTICAL offset coordinates (hexes stacked vertically)
        // Even columns (X % 2 == 0): hexes are shifted up
        // Odd columns (X % 2 == 1): hexes are shifted down
        
        Vector2I[] evenColDirections = { 
            new(0, -1),  // N
            new(1, -1),  // NE
            new(1, 0),   // SE  
            new(0, 1),   // S
            new(-1, 0),  // SW
            new(-1, -1)  // NW
        };
        
        Vector2I[] oddColDirections = { 
            new(0, -1),  // N
            new(1, 0),   // NE
            new(1, 1),   // SE
            new(0, 1),   // S
            new(-1, 1),  // SW
            new(-1, 0)   // NW
        };
        
        // Check column parity instead of row parity for vertical offset
        bool isEvenCol = center.X % 2 == 0;
        Vector2I[] directions = isEvenCol ? evenColDirections : oddColDirections;
        
        foreach (var dir in directions)
        {
            neighbors.Add(center + dir);
        }
        
        return neighbors;
    }
    
    private bool IsAdjacent(Vector2I from, Vector2I to)
    {
        var neighbors = GetHexNeighbors(from);
        return neighbors.Contains(to);
    }
    
    private bool IsMouseOverUI(Vector2 mousePos)
    {
        var hoveredControl = GetViewport().GuiGetHoveredControl();
        if (hoveredControl != null)
        {
            if (enableCoordinatePrinting)
                GD.Print($"[HexControls] UI detected: {hoveredControl.Name}");
            return true;
        }
        return false;
    }
    
    private void ActivateCursor()
    {
        EmitSignal(SignalName.CursorActivated, cursorPosition);
    }
    
    #endregion

    #region Visual Updates
    
    private void InitializeComponents()
    {
        cursorLayer ??= GetNodeOrNull<TileMapLayer>("Cursor");
        camera ??= GetViewport().GetCamera2D();
        hexGrid = GetParent<HexGrid>();

        if (camera != null && enableCoordinatePrinting)
            GD.Print($"[HexControls] Found camera: {camera.Name}");
        if (hexGrid != null && enableCoordinatePrinting)
            GD.Print($"[HexControls] Found HexGrid parent: {hexGrid.Name}");
    }

    private void UpdateCursorVisual()
    {
        if (cursorLayer == null) return;

        cursorLayer.Clear();

        if (movementModeActive)
        {
            // In movement mode, show different cursor colors based on validity
            var playerPos = GetPlayerPosition();
            
            if (cursorPosition == playerPos)
            {
                // At player position - yellow cursor
                cursorLayer.SetCell(cursorPosition, 0, new Vector2I(0, 0), 0);
            }
            else if (validMoves.Contains(cursorPosition))
            {
                // Valid move - green cursor
                cursorLayer.SetCell(cursorPosition, 0, new Vector2I(2, 0), 0);
            }
            else
            {
                // Invalid position - red cursor
                cursorLayer.SetCell(cursorPosition, 0, new Vector2I(3, 0), 0);
            }
        }
        else
        {
            // Normal mode - standard cursor
            cursorLayer.SetCell(cursorPosition, 0, Vector2I.Zero, clickCursorTileId);
            
            // Debug hover cursor
            if (enableDebugHover && hoverPosition != cursorPosition)
            {
                cursorLayer.SetCell(hoverPosition, 0, Vector2I.Zero, hoverCursorTileId);
            }
        }
    }

    private void UpdateCameraPosition()
    {
        if (!followWithCamera || camera == null) return;

        var cursorWorldPos = HexToWorld(cursorPosition);
        var targetGlobalPos = ToGlobal(cursorWorldPos);

        if (instantCameraMove)
        {
            camera.GlobalPosition = targetGlobalPos;
        }
        else
        {
            cameraTween?.Kill();
            cameraTween = CreateTween();
            cameraTween.TweenProperty(camera, "global_position", targetGlobalPos, 1f / cameraSpeed);
        }
    }

    private void ShowCursor()
    {
        if (cursorLayer == null) return;
        UpdateCursorVisual();
        if (followWithCamera && camera != null)
        {
            var cursorWorldPos = HexToWorld(cursorPosition);
            var targetGlobalPos = ToGlobal(cursorWorldPos);
            camera.GlobalPosition = targetGlobalPos;
        }
    }

    private void HideCursor()
    {
        if (cursorLayer == null) return;
        cursorLayer.Clear();
    }

    private Vector2 HexToWorld(Vector2I hexCoord)
    {
        if (cursorLayer == null) return Vector2.Zero;
        return cursorLayer.MapToLocal(hexCoord);
    }
    
    #endregion
}