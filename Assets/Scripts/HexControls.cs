// HexControls.cs - Enhanced hex input and interaction management with debug toggle
using Godot;
using System.Linq;
using System.Collections.Generic;

public partial class HexControls : Node2D
{
    #region Debug Configuration
    
    [Export] private bool enableDebugLogs = true; // Toggle for debug messages
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            GD.Print($"[HexControls] {message}");
    }
    
    #endregion

    #region Signals

    [Signal] public delegate void InteractionCancelledEventHandler();
    [Signal] public delegate void CursorMovedEventHandler(Vector2I coord);
    [Signal] public delegate void CellActivatedEventHandler(Vector2I coord);

    #endregion

    #region Configuration

    [Export] private Camera2D camera;
    [Export] private float cameraSpeed = 8f;
    [Export] private bool instantCameraMove = false;
    [Export] public bool enableDebugHover = false;
    [Export] public bool enableDebugWASD = false;
    [Export] public bool enableCoordinatePrinting = false;

    #endregion

    #region State

    private Vector2I cursorPosition = Vector2I.Zero;
    private Vector2I lastCursorPosition = new Vector2I(-999, -999); // Track previous position
    private Vector2I hoverPosition = Vector2I.Zero;
    private bool isActive = false;
    private bool cameraFollowsEnabled = true;
    private bool interactionModeActive = false;
    private HashSet<Vector2I> validCells = new();
    private HashSet<Vector2I> adjacentToValidCells = new(); // NEW: Track adjacent cells
    private List<Vector2I> currentAoePattern = new();
    private string currentTargetType = "";
    private bool justEnteredInteractionMode = false;
    private bool canTargetSelf = false;

    #endregion

    #region Components

    private HexGrid hexGrid;
    private TileMapLayer cursorLayer;
    private Tween cameraTween;

    #endregion

    #region Properties

    public Vector2I CursorPosition => cursorPosition;
    public bool IsActive => isActive;
    public bool IsInInteractionMode => interactionModeActive;

    #endregion

    #region Initialization

    public override void _Ready()
    {
        InitializeComponents();
        SetActive(false);
        
        // Add tileset debugging
        if (cursorLayer != null)
        {
            var tileSet = cursorLayer.TileSet;
            if (tileSet != null)
            {
                DebugLog($"TileSet found with {tileSet.GetSourceCount()} sources");
                for (int i = 0; i < tileSet.GetSourceCount(); i++)
                {
                    var source = tileSet.GetSource(i);
                    DebugLog($"Source {i}: {source?.GetType().Name}");
                }
            }
            else
            {
                GD.PrintErr("[HexControls] CursorLayer has no TileSet!");
            }
        }
    }

    private void InitializeComponents()
    {
        // First try to get cursor layer from HexControls node structure
        cursorLayer = GetNodeOrNull<TileMapLayer>("Cursor");

        // If not found, try to get it from the parent HexGrid
        if (cursorLayer == null)
        {
            hexGrid = GetParent<HexGrid>();
            if (hexGrid != null)
            {
                cursorLayer = hexGrid.GetLayer(CellLayer.Cursor);
                DebugLog("Using HexGrid's cursor layer");
            }
        }
        else
        {
            DebugLog("Using local cursor layer");
        }

        camera ??= GetViewport().GetCamera2D();
        hexGrid ??= GetParent<HexGrid>();

        if (camera != null && enableCoordinatePrinting)
            DebugLog($"Found camera: {camera.Name}");
        if (hexGrid != null && enableCoordinatePrinting)
            DebugLog($"Found HexGrid parent: {hexGrid.Name}");
        if (cursorLayer == null)
            GD.PrintErr("[HexControls] No cursor layer found!");
    }

    #endregion

    #region Activation Control

    public void SetActive(bool active)
    {
        isActive = active;
        DebugLog($"SetActive called with: {active}");
        UpdateCursorVisibility();
    }

    public void StartUIOnlyMode(Vector2I focusPosition)
    {
        interactionModeActive = false;
        SetActive(false);
        SetCameraFollow(false);
        FocusOnPosition(focusPosition);
        DebugLog($"UI-only mode - cursor locked on {focusPosition}");
    }

    public void EnterInteractionMode()
    {
        DebugLog("EnterInteractionMode called");
        interactionModeActive = true;
        justEnteredInteractionMode = true;
        SetCameraFollow(true);
        SetActive(true);
        cursorPosition = FindPlayerPosition();
        
        // Calculate adjacent cells to valid cells
        CalculateAdjacentToValidCells();
        
        UpdateCursorVisual();
        DebugLog($"Entered interaction mode - cursor at {cursorPosition}, valid cells: {validCells.Count}, adjacent cells: {adjacentToValidCells.Count}");
    }

    public void ExitInteractionMode(Vector2I focusPosition)
    {
        DebugLog("ExitInteractionMode called");
        interactionModeActive = false;
        validCells.Clear();
        adjacentToValidCells.Clear();
        SetActive(false);
        FocusOnPosition(focusPosition);
        SetCameraFollow(false);
        DebugLog($"Exited interaction mode - cursor locked on {focusPosition}");
    }

    #endregion

    #region Cursor Management

    public void MoveCursor(Vector2I coord)
    {
        if (!isActive) return;

        // Only clear old cursor if we're actually moving to a different position
        if (cursorPosition != coord && lastCursorPosition != new Vector2I(-999, -999))
        {
            ClearCursorAtPosition(lastCursorPosition);
        }

        lastCursorPosition = cursorPosition;
        cursorPosition = coord;
        
        UpdateCursorVisual();
        UpdateCameraPosition();
        
        // Show AOE preview if applicable
        if (interactionModeActive && currentAoePattern.Count > 0)
        {
            UpdateAoePreview(cursorPosition);
        }
        
        EmitSignal(SignalName.CursorMoved, coord);
    }

    private void ClearCursorAtPosition(Vector2I position)
    {
        if (cursorLayer != null)
        {
            var sourceId = cursorLayer.GetCellSourceId(position);
            if (sourceId != -1)
            {
                var atlasCoords = cursorLayer.GetCellAtlasCoords(position);
                // Only clear cursor tiles (atlas X 0-3), not AOE (atlas X 4+)
                if (atlasCoords.X < 4)
                {
                    cursorLayer.EraseCell(position);
                    DebugLog($"Cleared cursor at previous position {position}");
                }
            }
        }
    }

    public void FocusOnPosition(Vector2I position)
    {
        MoveCursor(position);
        GD.Print($"[HexControls] Focused camera on {position}");
    }

    public void SetCameraFollow(bool enabled)
    {
        cameraFollowsEnabled = enabled;
        GD.Print("[HexControls] Camera follow " + (enabled ? "enabled" : "disabled"));
    }

    public void SetValidCells(HashSet<Vector2I> cells)
    {
        validCells = cells;
        GD.Print($"[HexControls] SetValidCells called with {cells.Count} cells: [{string.Join(", ", cells)}]");
        
        // Recalculate adjacent cells when valid cells change
        if (interactionModeActive)
        {
            CalculateAdjacentToValidCells();
        }
    }

    public void SetTargetingInfo(string targetType, List<Vector2I> aoePattern)
    {
        currentTargetType = targetType;
        currentAoePattern = aoePattern;
        canTargetSelf = hexGrid?.CanTargetSelf(targetType) ?? false;

        GD.Print($"[HexControls] SetTargetingInfo - TargetType: {targetType}, CanTargetSelf: {canTargetSelf}, AOE: {aoePattern.Count} cells");
    }

    public void UpdateAoePreview(Vector2I targetCell)
    {
        if (hexGrid != null && currentAoePattern.Count > 0)
        {
            hexGrid.ClearAoePreview();
            
            // Only show AOE if on a valid target
            var playerPos = FindPlayerPosition();
            bool isValidTarget = validCells.Contains(targetCell) || (canTargetSelf && targetCell == playerPos);
            
            if (isValidTarget)
            {
                hexGrid.ShowAoePreview(targetCell, currentAoePattern);
                // After AOE preview, redraw the cursor to ensure green overrides yellow
                RedrawCursorOverAoe();
            }
        }
    }

    private void RedrawCursorOverAoe()
    {
        // Redraw the cursor to ensure it appears on top of any yellow AOE previews
        var playerPos = FindPlayerPosition();
        
        if (validCells.Contains(cursorPosition) || (canTargetSelf && cursorPosition == playerPos))
        {
            // Green cursor overrides yellow AOE - use alt_tile 1 for higher priority
            cursorLayer.SetCell(cursorPosition, 0, new Vector2I(2, 0), 1);
        }
        else if (adjacentToValidCells.Contains(cursorPosition))
        {
            // Red cursor overrides yellow AOE - use alt_tile 1 for higher priority
            cursorLayer.SetCell(cursorPosition, 0, new Vector2I(3, 0), 1);
        }
    }

    private void CalculateAdjacentToValidCells()
    {
        adjacentToValidCells.Clear();
        
        if (hexGrid == null || validCells.Count == 0)
        {
            GD.Print("[HexControls] No hexGrid or no valid cells - skipping adjacent calculation");
            return;
        }

        foreach (var validCell in validCells)
        {
            var neighbors = hexGrid.GetAllNeighborsOf(validCell);
            foreach (var neighbor in neighbors)
            {
                // Add if it's not already a valid cell and is on the grid
                if (!validCells.Contains(neighbor) && hexGrid.IsValidCell(neighbor))
                {
                    adjacentToValidCells.Add(neighbor);
                }
            }
        }
        
        // Also check player position adjacency if can target self
        if (canTargetSelf)
        {
            var playerPos = FindPlayerPosition();
            var playerNeighbors = hexGrid.GetAllNeighborsOf(playerPos);
            foreach (var neighbor in playerNeighbors)
            {
                if (!validCells.Contains(neighbor) && hexGrid.IsValidCell(neighbor))
                {
                    adjacentToValidCells.Add(neighbor);
                }
            }
        }

        GD.Print($"[HexControls] Calculated {adjacentToValidCells.Count} adjacent cells: [{string.Join(", ", adjacentToValidCells)}]");
    }

    #endregion

    #region Input Processing

    public override void _Input(InputEvent @event)
    {
        if (!isActive) return;

        // Skip if menu is handling input
        var inputManager = GetViewport().GetChildren().OfType<CentralInputManager>().FirstOrDefault();
        if (inputManager != null && inputManager.CurrentContext == CentralInputManager.InputContext.Menu)
        {
            return;
        }

        if (interactionModeActive)
        {
            HandleInteractionModeInput(@event);
        }
        else
        {
            HandleFreeNavigationInput(@event);
        }
    }

    private void HandleInteractionModeInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            ProcessKeyboardInput(keyEvent);
        }
        else if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            ProcessMouseInput(mouseButton);
        }
    }

    private void ProcessKeyboardInput(InputEventKey keyEvent)
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
                ConfirmCellSelection();
                return;
            case Key.Escape:
                CancelInteraction();
                return;
        }

        if (direction != Vector2I.Zero)
        {
            MoveInteractionCursor(direction);
        }
    }

    private void ProcessMouseInput(InputEventMouseButton mouseButton)
    {
        if (mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (!IsMouseOverUI(mouseButton.GlobalPosition))
            {
                var clickedCell = WorldToHex(mouseButton.GlobalPosition);
                var playerPos = FindPlayerPosition();

                GD.Print($"[HexControls] Mouse clicked on cell: {clickedCell}");

                // Check if clicked cell is valid target
                bool isValidTarget = validCells.Contains(clickedCell) ||
                                   (canTargetSelf && clickedCell == playerPos);

                if (isValidTarget)
                {
                    cursorPosition = clickedCell;
                    UpdateAoePreview(cursorPosition);
                    ConfirmCellSelection();
                }
                else if (adjacentToValidCells.Contains(clickedCell))
                {
                    // Move cursor to adjacent cell but don't confirm
                    cursorPosition = clickedCell;
                    UpdateCursorVisual();
                    UpdateAoePreview(cursorPosition);
                    GD.Print($"[HexControls] Moved cursor to adjacent invalid cell: {clickedCell}");
                }
                else
                {
                    GD.Print($"[HexControls] Invalid cell clicked: {clickedCell} - CanTargetSelf: {canTargetSelf}, PlayerPos: {playerPos}");
                }
            }
        }
    }

    private void MoveInteractionCursor(Vector2I direction)
    {
        justEnteredInteractionMode = false;

        var newPos = cursorPosition + direction;

        if (!IsAdjacentTo(cursorPosition, newPos))
        {
            GD.Print($"[HexControls] Blocked - {newPos} not adjacent to {cursorPosition}");
            return;
        }

        var playerPos = FindPlayerPosition();

        // Allow moving to valid cells, player position (if can target self), OR adjacent cells
        bool canMoveToValid = validCells.Contains(newPos) || (canTargetSelf && newPos == playerPos);
        bool canMoveToAdjacent = adjacentToValidCells.Contains(newPos);

        if (canMoveToValid || canMoveToAdjacent)
        {
            cursorPosition = newPos;
            UpdateCursorVisual();
            UpdateCameraPosition();
            UpdateAoePreview(cursorPosition);
            
            if (canMoveToValid)
                GD.Print($"[HexControls] Interaction cursor moved to valid cell: {cursorPosition}");
            else
                GD.Print($"[HexControls] Interaction cursor moved to adjacent invalid cell: {cursorPosition}");
        }
        else
        {
            GD.Print($"[HexControls] Blocked cursor move to {newPos} - Player at {playerPos}, ValidCells: [{string.Join(", ", validCells)}], AdjacentCells: [{string.Join(", ", adjacentToValidCells)}], CanTargetSelf: {canTargetSelf}");
        }
    }

    private void ConfirmCellSelection()
    {
        var playerPos = FindPlayerPosition();

        // Check if cursor is on an invalid cell (adjacent but not valid)
        bool isValidTarget = validCells.Contains(cursorPosition) || (canTargetSelf && cursorPosition == playerPos);
        if (!isValidTarget)
        {
            GD.Print($"[HexControls] Cannot confirm selection - cursor is on invalid cell {cursorPosition}");
            return;
        }

        // Special case: Movement to same position should cancel
        if (cursorPosition == playerPos && currentTargetType.ToLower() == "movement")
        {
            if (justEnteredInteractionMode)
            {
                GD.Print($"[HexControls] Ignoring immediate cancellation - user must move cursor first");
                justEnteredInteractionMode = false;
                return;
            }

            GD.Print($"[HexControls] Cancelling movement - trying to move to same position {cursorPosition}");
            CancelInteraction();
            return;
        }

        // For non-movement actions, allow targeting self if permitted
        if (cursorPosition == playerPos && !canTargetSelf)
        {
            GD.Print($"[HexControls] Cannot target self with this action - TargetType: {currentTargetType}");
            return;
        }

        justEnteredInteractionMode = false;

        GD.Print($"[HexControls] Confirming cell selection at {cursorPosition}");
        if (hexGrid != null)
        {
            hexGrid.SelectCell(cursorPosition);
        }
    }

    private void CancelInteraction()
    {
        GD.Print("[HexControls] Resetting visual state");

        ClearInteractionVisuals();

        var playerPos = FindPlayerPosition();
        cursorPosition = playerPos;
        UpdateCursorVisual();

        GD.Print("[HexControls] Visual reset complete");
        EmitSignal(SignalName.InteractionCancelled);
    }

    #endregion

    #region Free Navigation Input

    private void HandleFreeNavigationInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left && !IsMouseOverUI(mouseButton.GlobalPosition))
            {
                var coord = WorldToHex(mouseButton.GlobalPosition);
                if (enableCoordinatePrinting)
                    GD.Print($"[HexControls] Free navigation click -> {coord}");
                MoveCursor(coord);

                if (hexGrid != null)
                {
                    hexGrid.SelectCell(coord);
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

        if (enableDebugWASD && @event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            ProcessDebugWASDInput(keyEvent);
        }
    }

    private void ProcessDebugWASDInput(InputEventKey keyEvent)
    {
        Vector2I direction = Vector2I.Zero;

        switch (keyEvent.Keycode)
        {
            case Key.W: direction = new Vector2I(0, -1); break;
            case Key.S: direction = new Vector2I(0, 1); break;
            case Key.A: direction = new Vector2I(-1, 0); break;
            case Key.D: direction = new Vector2I(1, 0); break;
            case Key.Space:
                EmitSignal(SignalName.CellActivated, cursorPosition);
                return;
        }

        if (direction != Vector2I.Zero)
        {
            MoveCursor(cursorPosition + direction);
        }
    }

    #endregion

    #region Coordinate Conversion

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

    private Vector2 HexToWorld(Vector2I hexCoord)
    {
        if (cursorLayer == null) return Vector2.Zero;
        return cursorLayer.MapToLocal(hexCoord);
    }

    #endregion

    #region Visual Updates

    private void UpdateCursorVisual()
    {
        if (cursorLayer == null)
        {
            GD.PrintErr("[HexControls] CursorLayer is null!");
            return;
        }

        DebugLog($"Updating cursor visual - InteractionMode: {interactionModeActive}, Position: {cursorPosition}");
        
        // Force cursor layer to be visible and enabled
        cursorLayer.Visible = true;
        cursorLayer.Enabled = true;

        if (interactionModeActive)
        {
            UpdateInteractionModeCursor();
        }
        else
        {
            UpdateFreeNavigationCursor();
        }
        
        // Verify the cursor was actually set and force a refresh
        var verifySourceId = cursorLayer.GetCellSourceId(cursorPosition);
        var verifyAtlas = cursorLayer.GetCellAtlasCoords(cursorPosition);
        DebugLog($"Cursor verification - SourceId: {verifySourceId}, Atlas: {verifyAtlas}");
        
        // Force the layer to update its rendering
        cursorLayer.QueueRedraw();
        
        // Additional diagnostic info
        DebugLog($"CursorLayer visible: {cursorLayer.Visible}, enabled: {cursorLayer.Enabled}");
        DebugLog($"CursorLayer modulate: {cursorLayer.Modulate}");
        DebugLog($"CursorLayer z_index: {cursorLayer.ZIndex}");
    }

    private void RemoveOldCursor()
    {
        // More precise cursor removal - only remove the specific cursor position
        // Don't iterate through all cells as this can interfere with AOE previews
        
        // Store the previous cursor position if we have one
        if (cursorLayer != null)
        {
            // Only clear the exact cursor position, not all cursor tiles
            // Check if there's a cursor at the current position and what type it is
            var currentTile = cursorLayer.GetCellAtlasCoords(cursorPosition);
            var sourceId = cursorLayer.GetCellSourceId(cursorPosition);
            
            // Only remove if it's actually a cursor tile (not an AOE preview)
            if (sourceId != -1 && currentTile.X < 4) // Cursor tiles are 0-3, AOE is 4+
            {
                cursorLayer.EraseCell(cursorPosition);
                GD.Print($"[HexControls] Removed old cursor at {cursorPosition}");
            }
        }
    }

    private void UpdateInteractionModeCursor()
    {
        var playerPos = FindPlayerPosition();

        // Always set a cursor, even if there's an AOE preview underneath
        if (validCells.Contains(cursorPosition) || (canTargetSelf && cursorPosition == playerPos))
        {
            // Valid cell or self-target allowed - green cursor (atlas coords 2,0)
            // Use alternative tile to ensure it renders on top
            cursorLayer.SetCell(cursorPosition, 0, new Vector2I(2, 0), 1);
            DebugLog($"Set GREEN cursor at {cursorPosition} (valid target)");
        }
        else if (adjacentToValidCells.Contains(cursorPosition))
        {
            // Adjacent to valid but not valid itself - red cursor (atlas coords 3,0)
            // Use alternative tile to ensure it renders on top
            cursorLayer.SetCell(cursorPosition, 0, new Vector2I(3, 0), 1);
            DebugLog($"Set RED cursor at {cursorPosition} (adjacent to valid)");
        }
        else
        {
            // Fallback - use red cursor for any invalid position
            cursorLayer.SetCell(cursorPosition, 0, new Vector2I(3, 0), 1);
            DebugLog($"Set RED cursor at {cursorPosition} (fallback - invalid)");
        }
    }

    private void UpdateFreeNavigationCursor()
    {
        // Standard cursor for free navigation - GREEN (atlas coords 2,0)
        cursorLayer.SetCell(cursorPosition, 0, new Vector2I(2, 0), 0);
        DebugLog($"Set GREEN cursor at {cursorPosition} (free navigation)");

        // Debug hover cursor
        if (enableDebugHover && hoverPosition != cursorPosition)
        {
            cursorLayer.SetCell(hoverPosition, 0, new Vector2I(2, 0), 0);
            DebugLog($"Set GREEN hover cursor at {hoverPosition} (debug hover)");
        }
    }

    private void UpdateCursorVisibility()
    {
        GD.Print($"[HexControls] UpdateCursorVisibility - isActive: {isActive}");
        if (!isActive)
            HideCursor();
        else
            ShowCursor();
    }

    private void ShowCursor()
    {
        if (cursorLayer == null) return;
        GD.Print($"[HexControls] ShowCursor called - updating visual and camera");
        UpdateCursorVisual();
        if (cameraFollowsEnabled && camera != null)
        {
            var cursorWorldPos = HexToWorld(cursorPosition);
            var targetGlobalPos = ToGlobal(cursorWorldPos);
            camera.GlobalPosition = targetGlobalPos;
        }
    }

    private void HideCursor()
    {
        if (cursorLayer == null) return;
        GD.Print($"[HexControls] HideCursor called");
        RemoveOldCursor(); // Only remove cursor tiles, not AOE
    }

    private void ClearInteractionVisuals()
    {
        if (hexGrid != null)
        {
            var markerLayer = hexGrid.GetLayer(CellLayer.Marker);
            markerLayer?.Clear();

            var cursorLayer = hexGrid.GetLayer(CellLayer.Cursor);
            cursorLayer?.Clear();
        }
    }

    #endregion

    #region Camera Control

    private void UpdateCameraPosition()
    {
        if (!cameraFollowsEnabled || camera == null) return;

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

    #endregion

    #region Helper Methods

    private Vector2I FindPlayerPosition()
    {
        if (hexGrid == null) return Vector2I.Zero;

        var entityLayer = hexGrid.GetLayer(CellLayer.Entity);
        if (entityLayer == null) return Vector2I.Zero;

        // Search for player entity (assumes player is at atlas coords 0,0)
        for (int x = -10; x <= 10; x++)
        {
            for (int y = -10; y <= 10; y++)
            {
                var cell = new Vector2I(x, y);
                var tileData = entityLayer.GetCellTileData(cell);
                if (tileData != null)
                {
                    var atlasCoords = entityLayer.GetCellAtlasCoords(cell);
                    if (atlasCoords == Vector2I.Zero)
                    {
                        return cell;
                    }
                }
            }
        }

        return Vector2I.Zero;
    }

    private List<Vector2I> GetHexNeighbors(Vector2I center)
    {
        var neighbors = new List<Vector2I>();
        var directions = GetDirectionsFor(center);

        foreach (var dir in directions)
        {
            neighbors.Add(center + dir);
        }

        return neighbors;
    }

    private Vector2I[] GetDirectionsFor(Vector2I cell)
    {
        // Vertical offset hex coordinates
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

        bool isEvenCol = cell.X % 2 == 0;
        return isEvenCol ? evenColDirections : oddColDirections;
    }

    private bool IsAdjacentTo(Vector2I from, Vector2I to)
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

    #endregion

    #region Legacy Interface (Backward Compatibility)

    [System.Obsolete("Use EnterInteractionMode instead")]
    public void EnterMovementMode() => EnterInteractionMode();

    [System.Obsolete("Use ExitInteractionMode instead")]
    public void ExitMovementMode(Vector2I focusPosition) => ExitInteractionMode(focusPosition);

    [System.Obsolete("Use SetValidCells instead")]
    public void SetValidMoves(HashSet<Vector2I> moves) => SetValidCells(moves);

    [System.Obsolete("Use SetCameraFollow instead")]
    public void SetCameraLocked(bool locked) => SetCameraFollow(!locked);

    [System.Obsolete("Use InteractionCancelled signal instead")]
    public delegate void MovementCancelledEventHandler();

    [System.Obsolete("Use CellActivated signal instead")]
    public delegate void CursorActivatedEventHandler(Vector2I coord);

    #endregion
}