// HexControls.cs - Hex input and interaction management with clear separation
using Godot;
using System.Linq;
using System.Collections.Generic;

public partial class HexControls : Node2D
{
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
    private Vector2I hoverPosition = Vector2I.Zero;
    private bool isActive = false;
    private bool cameraFollowsEnabled = true;
    private bool interactionModeActive = false;
    private HashSet<Vector2I> validCells = new();
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
                GD.Print("[HexControls] Using HexGrid's cursor layer");
            }
        }
        else
        {
            GD.Print("[HexControls] Using local cursor layer");
        }

        camera ??= GetViewport().GetCamera2D();
        hexGrid ??= GetParent<HexGrid>();

        if (camera != null && enableCoordinatePrinting)
            GD.Print($"[HexControls] Found camera: {camera.Name}");
        if (hexGrid != null && enableCoordinatePrinting)
            GD.Print($"[HexControls] Found HexGrid parent: {hexGrid.Name}");
        if (cursorLayer == null)
            GD.PrintErr("[HexControls] No cursor layer found!");
    }

    #endregion

    #region Activation Control

    public void SetActive(bool active)
    {
        isActive = active;
        UpdateCursorVisibility();
    }

    public void StartUIOnlyMode(Vector2I focusPosition)
    {
        interactionModeActive = false;
        SetActive(false);
        SetCameraFollow(false);
        FocusOnPosition(focusPosition);
        GD.Print($"[HexControls] UI-only mode - cursor locked on {focusPosition}");
    }

    public void EnterInteractionMode()
    {
        interactionModeActive = true;
        justEnteredInteractionMode = true;
        SetCameraFollow(true);
        SetActive(true);
        cursorPosition = FindPlayerPosition();
        UpdateCursorVisual();
        GD.Print($"[HexControls] Entered interaction mode - cursor at {cursorPosition}, valid cells: {validCells.Count}");
    }

    public void ExitInteractionMode(Vector2I focusPosition)
    {
        interactionModeActive = false;
        validCells.Clear();
        SetActive(false);
        FocusOnPosition(focusPosition);
        SetCameraFollow(false);
        GD.Print($"[HexControls] Exited interaction mode - cursor locked on {focusPosition}");
    }

    #endregion

    #region Cursor Management

    public void MoveCursor(Vector2I coord)
    {
        if (!isActive) return;

        cursorPosition = coord;
        UpdateCursorVisual();
        UpdateCameraPosition();
        EmitSignal(SignalName.CursorMoved, coord);
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
            hexGrid.ShowAoePreview(targetCell, currentAoePattern);
        }
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

                // Check if clicked cell is valid target
                bool isValidTarget = validCells.Contains(clickedCell) ||
                                   (canTargetSelf && clickedCell == playerPos);

                if (isValidTarget)
                {
                    cursorPosition = clickedCell;
                    UpdateAoePreview(cursorPosition);
                    ConfirmCellSelection();
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

        // Allow moving to valid cells OR player position (if can target self)
        if (validCells.Contains(newPos) || (canTargetSelf && newPos == playerPos))
        {
            cursorPosition = newPos;
            UpdateCursorVisual();
            UpdateCameraPosition();
            UpdateAoePreview(cursorPosition);
            GD.Print($"[HexControls] Interaction cursor moved to {cursorPosition}");
        }
        else
        {
            GD.Print($"[HexControls] Blocked cursor move to {newPos} - Player at {playerPos}, ValidCells: [{string.Join(", ", validCells)}], CanTargetSelf: {canTargetSelf}");
        }
    }

    private void ConfirmCellSelection()
    {
        var playerPos = FindPlayerPosition();

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

        // Check if target is valid
        if (validCells.Contains(cursorPosition) || (canTargetSelf && cursorPosition == playerPos))
        {
            if (hexGrid != null)
            {
                hexGrid.SelectCell(cursorPosition);
            }
        }
        else
        {
            GD.Print($"[HexControls] Cannot confirm selection at {cursorPosition} - invalid target");
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

        cursorLayer.Clear();

        GD.Print($"[HexControls] Updating cursor visual - InteractionMode: {interactionModeActive}, Position: {cursorPosition}");

        if (interactionModeActive)
        {
            UpdateInteractionModeCursor();
        }
        else
        {
            UpdateFreeNavigationCursor();
        }
    }

    private void UpdateInteractionModeCursor()
    {
        var playerPos = FindPlayerPosition();

        if (cursorPosition == playerPos)
        {
            // At player position - yellow cursor (atlas coords 0,0)
            cursorLayer.SetCell(cursorPosition, 0, new Vector2I(0, 0), 0);
        }
        else if (validCells.Contains(cursorPosition))
        {
            // Valid cell - green cursor (atlas coords 2,0)
            cursorLayer.SetCell(cursorPosition, 0, new Vector2I(2, 0), 0);
        }
        else
        {
            // Invalid cell - red cursor (atlas coords 3,0) or fallback to basic cursor
            cursorLayer.SetCell(cursorPosition, 0, new Vector2I(1, 0), 0);
        }
    }

    private void UpdateFreeNavigationCursor()
    {
        // Standard cursor
        cursorLayer.SetCell(cursorPosition, 0, Vector2I.Zero, 1);

        // Debug hover cursor
        if (enableDebugHover && hoverPosition != cursorPosition)
        {
            cursorLayer.SetCell(hoverPosition, 0, Vector2I.Zero, 0);
        }
    }

    private void UpdateCursorVisibility()
    {
        if (!isActive)
            HideCursor();
        else
            ShowCursor();
    }

    private void ShowCursor()
    {
        if (cursorLayer == null) return;
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
        cursorLayer.Clear();
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