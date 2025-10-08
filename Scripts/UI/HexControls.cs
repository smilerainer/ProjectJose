// HexControls.cs - UI interface that uses HexGrid for math operations
using Godot;
using System.Linq;
using System.Collections.Generic;
using CustomJsonSystem;

public partial class HexControls : Node2D
{
    #region Signals
    [Signal] public delegate void InteractionCancelledEventHandler();
    [Signal] public delegate void CursorMovedEventHandler(Vector2I coord);
    [Signal] public delegate void CellActivatedEventHandler(Vector2I coord);
    #endregion

    #region Configuration
    [Export] private bool enableDebugLogs = false;
    [Export] private Camera2D camera;
    [Export] private float cameraSpeed = 8f;
    [Export] private bool instantCameraMove = false;
    #endregion

    #region Dependencies
    private HexGrid hexGrid; // Uses HexGrid for all math operations
    #endregion

    #region State
    private Vector2I cursorPosition = Vector2I.Zero;
    private bool isActive = false;
    private bool cameraFollowsEnabled = true;
    private bool interactionModeActive = false;
    private HashSet<Vector2I> validCells = new();
    private HashSet<Vector2I> adjacentCells = new();
    private bool canTargetSelf = false;
    private bool justEntered = false;
    private Tween cameraTween;
    private CustomJsonSystem.ActionConfig currentActionConfig;
    private BattleActionHandler actionHandler;
    #endregion

    #region Properties
    public Vector2I CursorPosition => cursorPosition;
    public bool IsActive => isActive;
    public bool IsInInteractionMode => interactionModeActive;
    #endregion

    #region Initialization
    public override void _Ready()
    {
        // Don't require HexGrid parent immediately
        TryInitializeHexGrid();
        
        camera ??= GetViewport().GetCamera2D();
        SetActive(false);
        
        if (enableDebugLogs) 
            GD.Print("[HexControls] UI interface initialized");
    }

    private bool TryInitializeHexGrid()
    {
        if (hexGrid != null) return true;
        
        hexGrid = GetParent<HexGrid>();
        
        if (hexGrid == null && enableDebugLogs)
        {
            GD.Print("[HexControls] HexGrid parent not found yet - will retry when needed");
        }
        
        return hexGrid != null;
    }

    public void FinalizeSetup()
    {
        if (TryInitializeHexGrid() && enableDebugLogs)
        {
            GD.Print("[HexControls] Finalized setup with HexGrid parent");
        }
    }
    #endregion

    #region Public API

    public void SetActive(bool active)
    {
        isActive = active;
        if (enableDebugLogs) GD.Print($"[HexControls] SetActive: {active}");

        if (!active && !interactionModeActive)
        {
            hexGrid?.ClearLayer(CellLayer.Cursor);
        }
    }
    public void EnterInteractionMode()
    {
        // Try to initialize if not done yet
        if (!TryInitializeHexGrid())
        {
            GD.PrintErr("[HexControls] Cannot enter interaction mode - no HexGrid parent!");
            return;
        }

        if (enableDebugLogs) GD.Print("[HexControls] EnterInteractionMode");

        interactionModeActive = true;
        justEntered = true;
        SetActive(true);
        SetCameraFollow(true);

        cursorPosition = FindPlayerPosition();
        CalculateAdjacentCells();
        DrawCursor();

        if (enableDebugLogs) GD.Print($"[HexControls] Interaction mode active - cursor at {cursorPosition}");
    }

    public void ExitInteractionMode(Vector2I focusPosition)
    {
        if (hexGrid == null) return;
        
        if (enableDebugLogs) GD.Print("[HexControls] ExitInteractionMode");
        
        interactionModeActive = false;
        validCells.Clear();
        adjacentCells.Clear();
        
        hexGrid.ClearAllHighlights();
        
        cursorPosition = focusPosition;
        SetActive(false);
        SetCameraFollow(false);
    }
    public void SetActionConfig(ActionConfig config, BattleActionHandler handler)
    {
        currentActionConfig = config;
        actionHandler = handler;
        canTargetSelf = hexGrid?.CanTargetSelf(config.TargetType) ?? false;
        if (enableDebugLogs) GD.Print($"[HexControls] Action config set: {config.Name}");
    }

    public void StartUIOnlyMode(Vector2I focusPosition)
    {
        interactionModeActive = false;
        SetActive(false);
        SetCameraFollow(false);
        FocusOnPosition(focusPosition);
    }

    public void SetValidCells(HashSet<Vector2I> cells)
    {
        validCells = cells;
        if (interactionModeActive)
        {
            CalculateAdjacentCells();
        }
        if (enableDebugLogs) GD.Print($"[HexControls] Valid cells set: {cells.Count}");
    }

    public void FocusOnPosition(Vector2I position)
    {
        cursorPosition = position;
        UpdateCamera();
    }

    public void SetCameraFollow(bool enabled)
    {
        cameraFollowsEnabled = enabled;
    }
    #endregion

    #region Input Handling
    public override void _Input(InputEvent @event)
    {
        if (!isActive || !interactionModeActive || hexGrid == null) return;
        
        // Skip if menu is handling input
        var inputManager = GetViewport().GetChildren().OfType<CentralInputManager>().FirstOrDefault();
        if (inputManager?.CurrentContext == CentralInputManager.InputContext.Menu) return;
        
        if (@event is InputEventKey key && key.Pressed)
        {
            Vector2I dir = Vector2I.Zero;
            
            switch (key.Keycode)
            {
                case Key.W: dir = new Vector2I(0, -1); break;
                case Key.S: dir = new Vector2I(0, 1); break;
                case Key.A: dir = new Vector2I(-1, 0); break;
                case Key.D: dir = new Vector2I(1, 0); break;
                case Key.Space:
                case Key.Enter:
                    ConfirmSelection();
                    return;
                case Key.Escape:
                    CancelInteraction();
                    return;
            }
            
            if (dir != Vector2I.Zero)
                MoveCursor(dir);
        }
        else if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
        {
            if (!IsMouseOverUI(mouse.GlobalPosition))
            {
                var clicked = hexGrid.WorldToCell(GetGlobalMousePosition());
                var playerPos = FindPlayerPosition();
                
                if (IsValidTarget(clicked))
                {
                    cursorPosition = clicked;
                    DrawCursor();
                    ShowAoeIfNeeded();
                    ConfirmSelection();
                }
                else if (adjacentCells.Contains(clicked))
                {
                    cursorPosition = clicked;
                    DrawCursor();
                    ShowAoeIfNeeded();
                }
            }
        }
    }

    private void MoveCursor(Vector2I dir)
    {
        if (hexGrid == null) return;
        
        justEntered = false;
        var newPos = cursorPosition + dir;
        
        // Use HexGrid for adjacency check
        var neighbors = hexGrid.GetHexNeighbors(cursorPosition);
        if (!neighbors.Contains(newPos)) return;
        
        var playerPos = FindPlayerPosition();
        bool canMove = IsValidTarget(newPos) || adjacentCells.Contains(newPos);
        
        if (canMove)
        {
            cursorPosition = newPos;
            DrawCursor();
            ShowAoeIfNeeded();
            UpdateCamera();
        }
    }

    private void ConfirmSelection()
    {
        if (!IsValidTarget(cursorPosition))
        {
            if (enableDebugLogs) GD.Print($"[HexControls] Invalid selection at {cursorPosition}");
            return;
        }
        
        var playerPos = FindPlayerPosition();
        
        // Special case: cancel if moving to same position
        if (cursorPosition == playerPos && currentActionConfig?.TargetType.ToLower() == "movement")
        {
            if (justEntered)
            {
                justEntered = false;
                return;
            }
            CancelInteraction();
            return;
        }
        
        justEntered = false;
        if (enableDebugLogs) GD.Print($"[HexControls] Confirmed: {cursorPosition}");
        
        // Emit signal instead of calling hexGrid directly
        EmitSignal(SignalName.CellActivated, cursorPosition);
    }

    private void CancelInteraction()
    {
        if (enableDebugLogs) GD.Print("[HexControls] Cancelled");
        
        hexGrid?.ClearAllHighlights();
        
        cursorPosition = FindPlayerPosition();
        DrawCursor();
        
        EmitSignal(SignalName.InteractionCancelled);
    }
    #endregion

    #region Cursor Management
    private void DrawCursor()
    {
        if (hexGrid == null) return;
        
        // Clear cursor layer
        hexGrid.ClearLayer(CellLayer.Cursor);
        
        // Determine if current position is valid
        bool isValid = IsValidTarget(cursorPosition);
        
        // If cursor is at invalid position (RED cursor)
        if (!isValid)
        {
            // Clear AOE markers
            hexGrid.ClearAoePreview();
            
            // Reset valid range markers - redraw them to restore blue highlights
            if (validCells.Count > 0)
            {
                var validCellsList = validCells.ToList();
                hexGrid.ShowRangeHighlight(validCellsList);
            }
            
            if (enableDebugLogs) GD.Print($"[HexControls] Invalid position - cleared AOE and reset range markers");
        }
        
        // Draw cursor with appropriate color
        var cursorType = isValid ? CursorType.Valid : CursorType.Invalid;
        hexGrid.SetCursor(cursorPosition, cursorType, CellLayer.Cursor);
        
        if (enableDebugLogs) 
            GD.Print($"[HexControls] Drew {cursorType} cursor at {cursorPosition}");
        
        // Emit cursor moved signal
        EmitSignal(SignalName.CursorMoved, cursorPosition);
    }

    private void ShowAoeIfNeeded()
    {
        if (hexGrid == null || currentActionConfig == null || actionHandler == null) return;
        
        if (IsValidTarget(cursorPosition))
        {
            var affectedCells = actionHandler.CalculateAffectedCells(cursorPosition, currentActionConfig);
            
            hexGrid.ShowAoePreviewAbsolute(affectedCells);
            CallDeferred(nameof(RedrawCursorOnly));
        }
    }
    
    private void RedrawCursorOnly()
    {
        if (hexGrid == null) return;

        // Only redraw the cursor without clearing AOE
        var cursorType = IsValidTarget(cursorPosition) ? CursorType.Valid : CursorType.Invalid;
        hexGrid.SetCursor(cursorPosition, cursorType, CellLayer.Cursor);
    }
    #endregion

    #region Helper Methods
    private bool IsValidTarget(Vector2I position)
    {
        var playerPos = FindPlayerPosition();
        return validCells.Contains(position) || (canTargetSelf && position == playerPos);
    }

    private void CalculateAdjacentCells()
    {
        if (hexGrid == null) return;
        
        adjacentCells.Clear();
        
        if (validCells.Count == 0) return;
        
        foreach (var valid in validCells)
        {
            var neighbors = hexGrid.GetHexNeighbors(valid);
            foreach (var n in neighbors)
            {
                if (!validCells.Contains(n) && hexGrid.IsValidCell(n))
                    adjacentCells.Add(n);
            }
        }
        
        if (canTargetSelf)
        {
            var playerPos = FindPlayerPosition();
            var neighbors = hexGrid.GetHexNeighbors(playerPos);
            foreach (var n in neighbors)
            {
                if (!validCells.Contains(n) && hexGrid.IsValidCell(n))
                    adjacentCells.Add(n);
            }
        }
        
        if (enableDebugLogs) GD.Print($"[HexControls] Adjacent cells: {adjacentCells.Count}");
    }

    private Vector2I FindPlayerPosition()
    {
        if (hexGrid == null) return Vector2I.Zero;
    
        var entityLayer = hexGrid.GetLayer(CellLayer.Entity);
        if (entityLayer == null) return Vector2I.Zero;
    
        // Search for player entity (atlas coords 0,0)
        for (int x = -20; x <= 20; x++)
        {
            for (int y = -20; y <= 20; y++)
            {
                var cell = new Vector2I(x, y);
                if (entityLayer.GetCellTileData(cell) != null &&
                    entityLayer.GetCellAtlasCoords(cell) == Vector2I.Zero)
                {
                    return cell;
                }
            }
        }
        return Vector2I.Zero;
    }

    private void UpdateCamera()
    {
        if (!cameraFollowsEnabled || camera == null || hexGrid == null) return;
        
        var worldPos = hexGrid.CellToWorld(cursorPosition);
        var targetPos = ToGlobal(worldPos);
        
        if (instantCameraMove)
        {
            camera.GlobalPosition = targetPos;
        }
        else
        {
            cameraTween?.Kill();
            cameraTween = CreateTween();
            cameraTween.TweenProperty(camera, "global_position", targetPos, 1f / cameraSpeed);
        }
    }

    private bool IsMouseOverUI(Vector2 pos)
    {
        return GetViewport().GuiGetHoveredControl() != null;
    }

    public Vector2 GetCursorWorldPosition() => hexGrid?.CellToWorld(cursorPosition) ?? Vector2.Zero;
    #endregion
}