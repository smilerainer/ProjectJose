// HexControls.cs - Simple cursor control with input handling and camera following
using Godot;
using System.Linq;

public partial class HexControls : Node2D
{
    [Signal] public delegate void CursorMovedEventHandler(Vector2I coord);
    [Signal] public delegate void CursorActivatedEventHandler(Vector2I coord);

    [Export] private TileMapLayer cursorLayer;
    [Export] private int hoverCursorTileId = 0;  // Debug: follows mouse
    [Export] private int clickCursorTileId = 1;  // Debug: shows clicked position
    [Export] private Camera2D camera;
    [Export] private bool followWithCamera = true;
    [Export] private float cameraSpeed = 8f;
    [Export] private bool instantCameraMove = false;
    [Export] public bool enableDebugHover = true; // Debug mode toggle
    [Export] public bool enableDebugWASD = false; // Debug: WASD moves cursor/camera

    private Vector2I cursorPosition = Vector2I.Zero;  // Clicked position
    private Vector2I hoverPosition = Vector2I.Zero;   // Mouse hover position
    private bool isActive = false;
    private Tween cameraTween;

    public Vector2I CursorPosition => cursorPosition;
    public bool IsActive => isActive;

    public override void _Ready()
    {
        InitializeComponents();
        SetActive(true); // Start active by default
    }

    public override void _Input(InputEvent @event)
    {
        // Skip internal input processing if CentralInputManager is handling it
        var inputManager = GetViewport().GetChildren().OfType<CentralInputManager>().FirstOrDefault();
        if (inputManager != null && inputManager.CurrentContext != CentralInputManager.InputContext.None)
        {
            // CentralInputManager is active, let it handle all input
            return;
        }
        
        // Fallback: handle input directly (for when CentralInputManager is not present)
        if (!isActive) return;

        HandleKeyboardInputInternal(@event);
        HandleMouseInputInternal(@event);
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

        // Convert mouse position to world coordinates accounting for camera position
        var viewport = GetViewport();
        var viewportSize = viewport.GetVisibleRect().Size;

        // Convert viewport mouse position to world position accounting for camera
        var worldPos = camera.GlobalPosition + (globalMousePos - viewportSize * 0.5f);

        // Convert world position to hex coordinates using the cursor layer
        var localPos = cursorLayer.ToLocal(worldPos);
        var hexCoord = cursorLayer.LocalToMap(localPos);

        return hexCoord;
    }

    public void HandleKeyboardInput(InputEvent @event)
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
                case Key.Space: ActivateCursor(); return;
            }

            if (direction != Vector2I.Zero)
            {
                var newPos = cursorPosition + direction;
                MoveCursor(newPos);
            }
        }
    }

    public void HandleMouseInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
            {
                var coord = WorldToHex(mouseButton.GlobalPosition);
                MoveCursor(coord);
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && enableDebugHover)
        {
            var coord = WorldToHex(mouseMotion.GlobalPosition);
            if (coord != hoverPosition)
            {
                hoverPosition = coord;
                UpdateCursorVisual();
            }
        }
    }

    #endregion

    #region Internal Implementation

    private void HandleKeyboardInputInternal(InputEvent @event)
    {
        // Debug WASD camera movement (only if enabled)
        if (!enableDebugWASD) return;

        Vector2I direction = Vector2I.Zero;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            switch (keyEvent.Keycode)
            {
                case Key.W:
                    direction = new Vector2I(0, -1);
                    GD.Print("[HexControls] Debug W pressed");
                    break;
                case Key.S:
                    direction = new Vector2I(0, 1);
                    GD.Print("[HexControls] Debug S pressed");
                    break;
                case Key.A:
                    direction = new Vector2I(-1, 0);
                    GD.Print("[HexControls] Debug A pressed");
                    break;
                case Key.D:
                    direction = new Vector2I(1, 0);
                    GD.Print("[HexControls] Debug D pressed");
                    break;
                case Key.Space:
                    GD.Print("[HexControls] Debug SPACE pressed");
                    ActivateCursor();
                    return;
            }
        }

        if (direction != Vector2I.Zero)
        {
            var newPos = cursorPosition + direction;
            MoveCursor(newPos);
        }
    }

    private void HandleMouseInputInternal(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
            {
                var coord = WorldToHex(mouseButton.GlobalPosition);
                GD.Print($"[HexControls] Click: GlobalPos {mouseButton.GlobalPosition} -> Hex {coord}");
                MoveCursor(coord);
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && enableDebugHover)
        {
            var coord = WorldToHex(mouseMotion.GlobalPosition);
            if (coord != hoverPosition)
            {
                GD.Print($"[HexControls] Hover: GlobalPos {mouseMotion.GlobalPosition} -> Hex {coord}");
                hoverPosition = coord;
                UpdateCursorVisual();
            }
        }
    }

    private void ActivateCursor()
    {
        EmitSignal(SignalName.CursorActivated, cursorPosition);
    }

    private void InitializeComponents()
    {
        cursorLayer ??= GetNodeOrNull<TileMapLayer>("Cursor");
        camera ??= GetViewport().GetCamera2D();

        if (camera != null)
            GD.Print($"[HexControls] Found camera: {camera.Name}");
    }

    private void UpdateCursorVisual()
    {
        if (cursorLayer == null) return;

        cursorLayer.Clear();

        // Draw click cursor (cursor 1)
        cursorLayer.SetCell(cursorPosition, 0, Vector2I.Zero, clickCursorTileId);

        // Draw hover cursor (cursor 0) if different from click position
        if (enableDebugHover && hoverPosition != cursorPosition)
        {
            cursorLayer.SetCell(hoverPosition, 0, Vector2I.Zero, hoverCursorTileId);
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