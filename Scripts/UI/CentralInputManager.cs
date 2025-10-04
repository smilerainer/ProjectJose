using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class CentralInputManager : Node2D
{
    #region Enums
    
    public enum InputContext
    {
        None,
        Menu,
        HexGrid,
        Dialogue,
        Mixed
    }
    
    #endregion
    
    #region Exported Properties
    
    [Export] private Texture2D cursorTexture;
    [Export] private Vector2 cursorOffset = Vector2.Zero;
    [Export] private Vector2 uiCursorOffset = Vector2.Zero;
    [Export] private bool enableCursor = true;
    [Export] private bool enableCursorSnapping = true;
    [Export] private Control dynamicMenuRoot;
    [Signal] public delegate void DynamicMenuSelectionEventHandler(int index, string buttonText);
    
    #endregion

    #region Constants

    private const float CURSOR_SPEED = 1200.0f;
    private const float SNAP_DISTANCE = 3.0f;
    
    #endregion
    
    #region State Fields
    
    private InputContext currentContext = InputContext.None;
    private Node currentActiveControl;
    private MenuControls previousMenu;
    private bool isShowingSubmenu = false;
    private string currentSubmenuContext = "";
    
    #endregion
    
    #region Spatial Navigation Fields
    
    private MenuControls[,] menuSpatialGrid;
    private Dictionary<MenuControls, Vector2I> menuGridPositions = new();
    private int gridWidth = 0;
    private int gridHeight = 0;
    private Vector2I currentGridPosition = Vector2I.Zero;
    [Export] private bool enableTabCycling = false;
    
    #endregion
    
    #region Control Collections
    
    private List<MenuControls> menuControls = new();
    private List<HexControls> hexControls = new();
    
    #endregion
    
    #region Cursor Fields
    
    private TextureRect cursorDisplay;
    private Vector2 targetCursorPosition = Vector2.Zero;
    
    #endregion
    
    #region Public Properties
    
    public InputContext CurrentContext 
    { 
        get => currentContext; 
        set => currentContext = value; 
    }
    public Node ActiveControl => currentActiveControl;
    
    #endregion
    
    #region Initialization
    
    public override void _Ready()
    {
        ProcessPriority = -10000;
        SetProcessInput(true);
        SetProcessUnhandledInput(true);
        
        AutoDiscoverControls();
        SetupCursorDisplay();
        CallDeferred(nameof(InitialCursorPositioning));
    }
    
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
    
    #endregion
    
    #region Control Registration
    
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
    
    #endregion
    
    #region Process Loop
    
    public override void _Process(double delta)
    {
        UpdateActiveContext();
        UpdateCursor(delta);
    }
    
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
    
    private void DetectActiveControl()
    {
        var activeMenus = menuControls.Where(m => m.IsActive).ToList();
        var activeHex = hexControls.Where(h => h.IsActive).ToList();
        
        int totalActive = activeMenus.Count + activeHex.Count;
        
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
            else if (activeHex.Count > 0)
            {
                currentContext = InputContext.HexGrid;
                currentActiveControl = activeHex[0];
            }
        }
        else
        {
            // Prioritize HexGrid when multiple controls are active
            if (activeHex.Count > 0)
            {
                currentContext = InputContext.HexGrid;
                currentActiveControl = activeHex[0];
            }
            else if (activeMenus.Count > 0)
            {
                currentContext = InputContext.Menu;
                currentActiveControl = activeMenus[0];
            }
            else
            {
                currentContext = InputContext.Mixed;
                currentActiveControl = activeMenus.FirstOrDefault();
            }
        }
    }
    
    private void OnContextChanged()
    {
        UpdateCursorTarget();
    }
    
    #endregion
    
    #region Input Handling
    
    public override void _UnhandledInput(InputEvent @event)
    {
        if (enableTabCycling && @event.IsActionPressed("ui_focus_next"))
        {
            GetViewport().SetInputAsHandled();
        }
    }
    
    public override void _Input(InputEvent @event)
    {
        if (enableTabCycling && @event.IsActionPressed("ui_focus_next"))
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        
        if (currentContext == InputContext.None) return;
        
        switch (currentActiveControl)
        {
            case MenuControls menu:
                HandleMenuInput(menu, @event);
                break;
            case HexControls hex:
                HandleHexInput(hex, @event);
                break;
        }
    }
    
    private void HandleMenuInput(MenuControls menu, InputEvent @event)
    {
        if (TryHandleCancel(@event, menu)) return;
        
        var direction = GetDirectionInput(@event);
        if (direction != Vector2I.Zero)
        {
            menu.Navigate(direction);
            UpdateCursorTarget();
            GetViewport().SetInputAsHandled();
        }
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
    
    private void HandleHexInput(HexControls hex, InputEvent @event)
    {
        // HexControls handles its own input
    }
    
    private Vector2I GetDirectionInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_up") || IsKey(@event, Key.W) || IsKey(@event, Key.Up))
            return Vector2I.Up;
        if (@event.IsActionPressed("ui_down") || IsKey(@event, Key.S) || IsKey(@event, Key.Down))
            return Vector2I.Down;
        if (@event.IsActionPressed("ui_left") || IsKey(@event, Key.A) || IsKey(@event, Key.Left))
            return Vector2I.Left;
        if (@event.IsActionPressed("ui_right") || IsKey(@event, Key.D) || IsKey(@event, Key.Right))
            return Vector2I.Right;
        
        return Vector2I.Zero;
    }
    
    private bool IsAcceptInput(InputEvent @event)
    {
        return @event.IsActionPressed("ui_accept") || 
               IsKey(@event, Key.Space) || 
               IsKey(@event, Key.Enter) || 
               IsKey(@event, Key.KpEnter);
    }

    private bool TryHandleCancel(InputEvent @event, MenuControls menu)
    {
        if (@event.IsActionPressed("ui_cancel") || IsKey(@event, Key.Escape))
        {
            var dynamicMenu = GetDynamicMenu();
            if (menu == dynamicMenu && dynamicMenu != null && dynamicMenu.IsActive)
            {
                ClearSubmenu();
                GetViewport().SetInputAsHandled();
                GD.Print("[Input] Cancelled");
                return true;
            }
        }
        return false;
    }
    
    private bool IsKey(InputEvent @event, Key key)
    {
        return @event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == key;
    }
    
    private bool IsMouseClick(InputEvent @event)
    {
        return @event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left;
    }
    
    #endregion
    
    #region Submenu Management
    
    public void ShowSubmenu(string[] options, string context)
    {
        var dynamicMenu = GetDynamicMenu();
        if (dynamicMenu == null)
        {
            GD.PrintErr("[InputManager] No dynamic menu found");
            return;
        }
        
        var currentMenu = currentActiveControl as MenuControls;
        if (currentMenu != null && currentMenu != dynamicMenu)
        {
            previousMenu = currentMenu;
            currentMenu.SetActive(false);
        }
        
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
    
    public void ClearSubmenu()
    {
        var dynamicMenu = GetDynamicMenu();
        if (dynamicMenu != null)
        {
            dynamicMenu.SetActive(false);
            dynamicMenu.ClearAllButtons();
        }
        
        isShowingSubmenu = false;
        currentSubmenuContext = "";
        
        if (previousMenu != null)
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
            currentActiveControl = null;
            currentContext = InputContext.None;
        }
    }
    
    public void SetMenuButtonArray(string[] buttonTexts)
    {
        ShowSubmenu(buttonTexts, "dynamic");
    }
    
    public void ClearDynamicMenu()
    {
        ClearSubmenu();
    }
    
    private void NotifySubmenuSelection(MenuControls menu)
    {
        int index = menu.GetLinearIndex();
        string text = menu.GetCurrentButtonText();
        
        EmitSignal(SignalName.DynamicMenuSelection, index, text);
        menu.ActivateCurrentButton();
    }
    
    private MenuControls GetDynamicMenu()
    {
        if (dynamicMenuRoot == null) return null;
        
        foreach (Node child in dynamicMenuRoot.GetChildren())
        {
            if (child is MarginContainer margin)
            {
                foreach (Node grandchild in margin.GetChildren())
                {
                    if (grandchild is MenuControls menu)
                        return menu;
                }
            }
        }
        return null;
    }
    
    #endregion
    
    #region Cursor Management
    
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
        cursorDisplay.Visible = !(currentActiveControl is HexControls);
    }
    
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
    
    private Vector2 GetHexCursorPosition(HexControls hex)
    {
        var worldPos = hex.GetCursorWorldPosition();
        if (worldPos == Vector2.Zero) return Vector2.Zero;
        
        var globalPos = hex.ToGlobal(worldPos);
        var camera = GetViewport().GetCamera2D();
        if (camera == null) return Vector2.Zero;
        
        var viewportSize = GetViewport().GetVisibleRect().Size;
        return globalPos - camera.GlobalPosition + viewportSize * 0.5f;
    }
    
    private void MoveCursorToTarget(double delta)
    {
        var currentPos = cursorDisplay.Position + cursorOffset;
        Vector2 newPos;
        
        if (enableCursorSnapping)
        {
            newPos = targetCursorPosition;
        }
        else
        {
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
    
    private void InitialCursorPositioning()
    {
        UpdateActiveContext();
        
        if (currentActiveControl != null)
        {
            UpdateCursorTarget();
            if (cursorDisplay != null)
            {
                cursorDisplay.Position = targetCursorPosition.Round() - cursorOffset;
            }
        }
    }
    
    public void NotifyButtonFocusChanged()
    {
        UpdateCursorTarget();
    }
    
    #endregion
}