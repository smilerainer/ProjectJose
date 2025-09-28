// HexControls.cs - Fixed timing and draw order
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
    [Export] private bool enableDebugLogs = false;
    [Export] private Camera2D camera;
    [Export] private float cameraSpeed = 8f;
    [Export] private bool instantCameraMove = false;
    #endregion

    #region State
    private Vector2I cursorPosition = Vector2I.Zero;
    private bool isActive = false;
    private bool cameraFollowsEnabled = true;
    private bool interactionModeActive = false;
    private HashSet<Vector2I> validCells = new();
    private HashSet<Vector2I> adjacentCells = new();
    private List<Vector2I> aoePattern = new();
    private string targetType = "";
    private bool canTargetSelf = false;
    private bool justEntered = false;
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
        hexGrid = GetParent<HexGrid>();
        if (hexGrid != null)
        {
            cursorLayer = hexGrid.GetLayer(CellLayer.Cursor);
            if (enableDebugLogs) GD.Print($"[HexControls] Using HexGrid's cursor layer");
        }
        
        camera ??= GetViewport().GetCamera2D();
        SetActive(false);
    }
    #endregion

    #region Core Control
    public void SetActive(bool active)
    {
        isActive = active;
        if (enableDebugLogs) GD.Print($"[HexControls] SetActive: {active}");
        
        if (!active && cursorLayer != null)
        {
            // Don't clear on deactivate - let other systems handle cleanup
            if (!interactionModeActive)
            {
                cursorLayer.Clear();
            }
        }
    }

    public void EnterInteractionMode()
    {
        if (enableDebugLogs) GD.Print("[HexControls] EnterInteractionMode");
        
        interactionModeActive = true;
        justEntered = true;
        SetActive(true);
        SetCameraFollow(true);
        
        cursorPosition = FindPlayerPosition();
        CalculateAdjacentCells();
        
        // Draw cursor immediately
        DrawCursor();
        
        if (enableDebugLogs) GD.Print($"[HexControls] Interaction mode active - cursor at {cursorPosition}");
    }

    public void ExitInteractionMode(Vector2I focusPosition)
    {
        if (enableDebugLogs) GD.Print("[HexControls] ExitInteractionMode");
        
        interactionModeActive = false;
        validCells.Clear();
        adjacentCells.Clear();
        
        if (cursorLayer != null)
            cursorLayer.Clear();
        
        cursorPosition = focusPosition;
        SetActive(false);
        SetCameraFollow(false);
    }

    public void StartUIOnlyMode(Vector2I focusPosition)
    {
        interactionModeActive = false;
        SetActive(false);
        SetCameraFollow(false);
        FocusOnPosition(focusPosition);
    }
    #endregion

    #region Data Management
    public void SetValidCells(HashSet<Vector2I> cells)
    {
        validCells = cells;
        if (interactionModeActive)
        {
            CalculateAdjacentCells();
        }
        if (enableDebugLogs) GD.Print($"[HexControls] Valid cells set: {cells.Count}");
    }

    public void SetTargetingInfo(string type, List<Vector2I> pattern)
    {
        targetType = type;
        aoePattern = pattern;
        canTargetSelf = hexGrid?.CanTargetSelf(type) ?? false;
        if (enableDebugLogs) GD.Print($"[HexControls] Targeting: {type}, CanTargetSelf: {canTargetSelf}");
    }

    private void CalculateAdjacentCells()
    {
        adjacentCells.Clear();
        
        if (hexGrid == null || validCells.Count == 0) return;
        
        foreach (var valid in validCells)
        {
            var neighbors = hexGrid.GetAllNeighborsOf(valid);
            foreach (var n in neighbors)
            {
                if (!validCells.Contains(n) && hexGrid.IsValidCell(n))
                    adjacentCells.Add(n);
            }
        }
        
        if (canTargetSelf)
        {
            var playerPos = FindPlayerPosition();
            var neighbors = hexGrid.GetAllNeighborsOf(playerPos);
            foreach (var n in neighbors)
            {
                if (!validCells.Contains(n) && hexGrid.IsValidCell(n))
                    adjacentCells.Add(n);
            }
        }
        
        if (enableDebugLogs) GD.Print($"[HexControls] Adjacent cells: {adjacentCells.Count}");
    }
    #endregion

    #region Input
    public override void _Input(InputEvent @event)
    {
        if (!isActive || !interactionModeActive) return;
        
        // Skip if menu is handling
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
                var clicked = WorldToHex(mouse.GlobalPosition);
                var playerPos = FindPlayerPosition();
                
                if (validCells.Contains(clicked) || (canTargetSelf && clicked == playerPos))
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
        justEntered = false;
        var newPos = cursorPosition + dir;
        
        if (!IsAdjacent(cursorPosition, newPos)) return;
        
        var playerPos = FindPlayerPosition();
        bool canMove = validCells.Contains(newPos) || 
                      (canTargetSelf && newPos == playerPos) || 
                      adjacentCells.Contains(newPos);
        
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
        var playerPos = FindPlayerPosition();
        bool isValid = validCells.Contains(cursorPosition) || 
                      (canTargetSelf && cursorPosition == playerPos);
        
        if (!isValid)
        {
            if (enableDebugLogs) GD.Print($"[HexControls] Invalid selection at {cursorPosition}");
            return;
        }
        
        // Special case: cancel if moving to same position
        if (cursorPosition == playerPos && targetType.ToLower() == "movement")
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
        hexGrid?.SelectCell(cursorPosition);
    }

    private void CancelInteraction()
    {
        if (enableDebugLogs) GD.Print("[HexControls] Cancelled");
        
        if (hexGrid != null)
        {
            hexGrid.GetLayer(CellLayer.Marker)?.Clear();
            hexGrid.GetLayer(CellLayer.Cursor)?.Clear();
        }
        
        cursorPosition = FindPlayerPosition();
        DrawCursor();
        
        EmitSignal(SignalName.InteractionCancelled);
    }
    #endregion

    #region Drawing
    private void DrawCursor()
    {
        if (cursorLayer == null) return;
        
        // Clear entire cursor layer first to avoid leftover cursors
        cursorLayer.Clear();
        
        // Determine cursor color
        var playerPos = FindPlayerPosition();
        bool isValid = validCells.Contains(cursorPosition) || 
                      (canTargetSelf && cursorPosition == playerPos);
        
        // Use the CORRECT atlas coords from HexGrid
        var atlasCoords = isValid ? new Vector2I(2, 0) : new Vector2I(3, 0);  // GREEN: (2,0), RED: (3,0)
        
        // SetCell requires: position, source_id, atlas_coords
        cursorLayer.SetCell(cursorPosition, 0, atlasCoords);
        
        if (enableDebugLogs) 
            GD.Print($"[HexControls] Drew {(isValid ? "GREEN" : "RED")} cursor at {cursorPosition} with atlas {atlasCoords}");
    }

    private void ShowAoeIfNeeded()
    {
        if (hexGrid != null && aoePattern.Count > 0)
        {
            var playerPos = FindPlayerPosition();
            bool isValid = validCells.Contains(cursorPosition) || 
                          (canTargetSelf && cursorPosition == playerPos);
            
            if (isValid)
            {
                hexGrid.ClearAoePreview();
                hexGrid.ShowAoePreview(cursorPosition, aoePattern);
                // Redraw cursor on top of AOE
                CallDeferred(nameof(DrawCursor));
            }
        }
    }
    #endregion

    #region Helpers
    public void FocusOnPosition(Vector2I position)
    {
        cursorPosition = position;
        UpdateCamera();
    }

    public void SetCameraFollow(bool enabled)
    {
        cameraFollowsEnabled = enabled;
    }

    private void UpdateCamera()
    {
        if (!cameraFollowsEnabled || camera == null) return;
        
        var worldPos = HexToWorld(cursorPosition);
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

    private Vector2I FindPlayerPosition()
    {
        if (hexGrid == null) return Vector2I.Zero;
        
        var entityLayer = hexGrid.GetLayer(CellLayer.Entity);
        if (entityLayer == null) return Vector2I.Zero;
        
        for (int x = -10; x <= 10; x++)
        {
            for (int y = -10; y <= 10; y++)
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

    private bool IsAdjacent(Vector2I from, Vector2I to)
    {
        var dirs = (from.X % 2 == 0) ? 
            new[] { new Vector2I(0,-1), new Vector2I(1,-1), new Vector2I(1,0), 
                   new Vector2I(0,1), new Vector2I(-1,0), new Vector2I(-1,-1) } :
            new[] { new Vector2I(0,-1), new Vector2I(1,0), new Vector2I(1,1), 
                   new Vector2I(0,1), new Vector2I(-1,1), new Vector2I(-1,0) };
        
        foreach (var d in dirs)
        {
            if (from + d == to) return true;
        }
        return false;
    }

    private bool IsMouseOverUI(Vector2 pos)
    {
        return GetViewport().GuiGetHoveredControl() != null;
    }

    public Vector2I WorldToHex(Vector2 globalPos)
    {
        if (cursorLayer == null || camera == null) return Vector2I.Zero;
        var viewport = GetViewport();
        var viewportSize = viewport.GetVisibleRect().Size;
        var worldPos = camera.GlobalPosition + (globalPos - viewportSize * 0.5f);
        var localPos = cursorLayer.ToLocal(worldPos);
        return cursorLayer.LocalToMap(localPos);
    }

    private Vector2 HexToWorld(Vector2I hex)
    {
        if (cursorLayer == null) return Vector2.Zero;
        return cursorLayer.MapToLocal(hex);
    }

    public Vector2 GetCursorWorldPosition() => HexToWorld(cursorPosition);
    #endregion
}