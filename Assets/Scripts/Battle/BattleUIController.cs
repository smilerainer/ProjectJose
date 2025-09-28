// BattleUIController.cs - Manages UI interactions and display
using Godot;
using System.Collections.Generic;
using System.Linq;

public class BattleUIController
{
    #region UI Components
    
    private CentralInputManager inputManager;
    private HexGrid hexGrid;
    private MenuControls menuControls;
    private HexControls hexControls;
    private BattleManager battleManager;
    
    #endregion
    
    #region UI State
    
    private bool isInSubmenu = false;
    private bool targetSelectionActive = false;
    
    #endregion
    
    #region Initialization
    
    public void Initialize(BattleManager battleManager)
    {
        this.battleManager = battleManager;
        FindUIComponents(battleManager);
        ConnectSignals();
    }
    
    private void FindUIComponents(BattleManager battleManager)
    {
        hexGrid = battleManager.GetNode<HexGrid>("../HexGrid");
        hexControls = hexGrid?.GetNodeOrNull<HexControls>("HexControls");
        menuControls = FindMenuControlsInTree(battleManager.GetTree().CurrentScene);
        inputManager = FindInputManagerInTree(battleManager.GetTree().CurrentScene);
        
        GD.Print($"[BattleUI] UI Components found - HexGrid: {hexGrid != null}, MenuControls: {menuControls != null}, InputManager: {inputManager != null}");
    }
    
    private void ConnectSignals()
    {
        if (menuControls != null)
            menuControls.ButtonActivated += OnMainMenuButtonPressed;
        if (hexControls != null)
        {
            hexControls.CellActivated += OnCellSelected;
            hexControls.InteractionCancelled += OnInteractionCancelled;
        }
        if (inputManager != null)
            inputManager.DynamicMenuSelection += OnDynamicMenuSelection;
            
        GD.Print("[BattleUI] Signals connected successfully");
    }
    
    public void SetupUI()
    {
        if (hexControls != null)
        {
            hexControls.StartUIOnlyMode(new Vector2I(0, 0));
            GD.Print("[BattleUI] UI setup completed");
        }
    }
    
    #endregion
    
    #region Menu Management
    
    public void ShowMainMenu()
    {
        if (menuControls != null)
        {
            menuControls.SetActive(true);
            menuControls.ResetToFirstButton();
            
            // Ensure focus is set properly with delay
            EnsureMenuFocusDelayed();
        }
        
        GD.Print("[BattleUI] Main menu shown");
    }
    
    public void HideMainMenu()
    {
        if (menuControls != null)
        {
            menuControls.SetActive(false);
            menuControls.ReleaseFocus();
        }
        
        GD.Print("[BattleUI] Main menu hidden");
    }
    
    public void ShowSubmenu(string[] options)
    {
        isInSubmenu = true;
        
        if (inputManager != null)
        {
            inputManager.SetMenuButtonArray(options);
            
            // Connect to dynamic menu with delay
            ConnectToDynamicMenuDelayed();
        }
        
        GD.Print($"[BattleUI] Submenu shown with {options.Length} options");
    }
    
    public void HideSubmenu()
    {
        if (isInSubmenu)
        {
            DisconnectFromDynamicMenu();
            
            if (inputManager != null)
            {
                inputManager.ClearDynamicMenu();
            }
            
            isInSubmenu = false;
            GD.Print("[BattleUI] Submenu hidden");
        }
    }
    
    private void EnsureMenuFocus()
    {
        if (inputManager != null)
        {
            inputManager.NotifyButtonFocusChanged();
        }
    }
    
    #endregion
    
    #region Target Selection UI
    
    public void StartTargetSelection(List<Vector2I> validTargets, List<Vector2I> aoePattern, string targetType)
    {
        targetSelectionActive = true;
        
        HideMainMenu();
        HideSubmenu();
        
        if (hexGrid != null)
        {
            hexGrid.ShowRangeHighlight(validTargets);
        }
        
        if (hexControls != null)
        {
            hexControls.SetActive(true);
            hexControls.SetValidCells(validTargets.ToHashSet());
            hexControls.SetTargetingInfo(targetType, aoePattern);
            hexControls.EnterInteractionMode();
        }
        
        GD.Print($"[BattleUI] Target selection started - {validTargets.Count} valid targets, TargetType: {targetType}");
    }
    
    public void EndTargetSelection()
    {
        if (!targetSelectionActive) return;
        
        targetSelectionActive = false;
        
        if (hexGrid != null)
        {
            hexGrid.ClearRangeHighlight();
            hexGrid.ClearAoePreview();
        }
        
        if (hexControls != null)
        {
            hexControls.ExitInteractionMode(new Vector2I(0, 0));
            hexControls.SetActive(false);
        }
        
        GD.Print("[BattleUI] Target selection ended");
    }
    
    #endregion
    
    #region Dynamic Menu Management
    
    private void ConnectToDynamicMenu()
    {
        var dynamicMenu = GetDynamicMenuFromInputManager();
        if (dynamicMenu != null)
        {
            // Simple disconnection and reconnection
            try
            {
                dynamicMenu.ButtonActivated -= OnDynamicMenuButtonPressed;
                dynamicMenu.ButtonActivated += OnDynamicMenuButtonPressed;
                GD.Print("[BattleUI] Connected to dynamic menu successfully");
            }
            catch (System.Exception e)
            {
                GD.PrintErr($"[BattleUI] Error connecting to dynamic menu: {e.Message}");
                ConnectToDynamicMenuDelayed();
            }
        }
        else
        {
            GD.PrintErr("[BattleUI] Failed to find dynamic menu - retrying...");
            ConnectToDynamicMenuDelayed();
        }
    }
    
    private void DisconnectFromDynamicMenu()
    {
        var dynamicMenu = GetDynamicMenuFromInputManager();
        if (dynamicMenu != null)
        {
            // Simple disconnection without Callable checks
            try
            {
                dynamicMenu.ButtonActivated -= OnDynamicMenuButtonPressed;
                GD.Print("[BattleUI] Disconnected from dynamic menu");
            }
            catch (System.Exception e)
            {
                GD.PrintErr($"[BattleUI] Error disconnecting from dynamic menu: {e.Message}");
            }
        }
    }
    
    private MenuControls GetDynamicMenuFromInputManager()
    {
        var dynamicMenuRoot = GetDynamicMenuRoot();
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
    
    private Control GetDynamicMenuRoot()
    {
        var ui = battleManager.GetTree().CurrentScene.GetNodeOrNull<CanvasLayer>("UI");
        if (ui != null)
        {
            var dynamicRoot = ui.GetNodeOrNull<Control>("DynamicMenuRoot");
            if (dynamicRoot != null) return dynamicRoot;
        }
        
        var control = battleManager.GetTree().CurrentScene.GetNodeOrNull<Control>("Control");
        if (control != null)
        {
            var dynamicRoot = control.GetNodeOrNull<Control>("DynamicMenuRoot");
            if (dynamicRoot != null) return dynamicRoot;
        }
        
        return FindDynamicMenuRootRecursive(battleManager.GetTree().CurrentScene);
    }
    
    private Control FindDynamicMenuRootRecursive(Node node)
    {
        if (node is Control control && node.Name.ToString().Contains("Dynamic"))
        {
            foreach (Node child in control.GetChildren())
            {
                if (child is MarginContainer margin)
                {
                    foreach (Node grandchild in margin.GetChildren())
                    {
                        if (grandchild is MenuControls)
                            return control;
                    }
                }
            }
        }
        
        foreach (Node child in node.GetChildren())
        {
            var result = FindDynamicMenuRootRecursive(child);
            if (result != null) return result;
        }
        
        return null;
    }
    
    #endregion
    
    #region Input Event Handlers
    
    private void OnMainMenuButtonPressed(int index, BaseButton button)
    {
        string buttonText = button.Name.ToString().ToLower();
        
        GD.Print($"[BattleUI] Main menu button pressed: {buttonText}");
        
        if (buttonText.Contains("move"))
            battleManager.OnActionRequested("move", "");
        else if (buttonText.Contains("skill"))
            battleManager.OnActionRequested("skill", "");
        else if (buttonText.Contains("item"))
            battleManager.OnActionRequested("item", "");
        else if (buttonText.Contains("talk"))
            battleManager.OnActionRequested("talk", "");
    }
    
    private void OnDynamicMenuButtonPressed(int index, BaseButton button)
    {
        if (isInSubmenu)
        {
            string buttonText = GetButtonText(button);
            GD.Print($"[BattleUI] Dynamic menu selection: {buttonText}");
            battleManager.OnSubmenuSelection(buttonText);
        }
    }
    
    private void OnDynamicMenuSelection(int index, string buttonText)
    {
        if (isInSubmenu)
        {
            GD.Print($"[BattleUI] Dynamic menu selection via input manager: {buttonText}");
            battleManager.OnSubmenuSelection(buttonText);
        }
    }
    
    private void OnCellSelected(Vector2I cell)
    {
        if (targetSelectionActive)
        {
            GD.Print($"[BattleUI] Cell selected for targeting: {cell}");
            battleManager.OnTargetSelected(cell);
        }
    }
    
    private void OnInteractionCancelled()
    {
        GD.Print("[BattleUI] Target selection cancelled");
        battleManager.OnActionCancelled();
    }
    
    #endregion
    
    #region Helper Methods - Timer Workarounds
    
    private void EnsureMenuFocusDelayed()
    {
        // Use a simple call deferred instead of timer
        battleManager.CallDeferred(nameof(CallEnsureMenuFocus));
    }
    
    private void ConnectToDynamicMenuDelayed()
    {
        // Use a simple call deferred instead of timer
        battleManager.CallDeferred(nameof(CallConnectToDynamicMenu));
    }
    
    // These methods will be called by the BattleManager
    public void CallEnsureMenuFocus()
    {
        EnsureMenuFocus();
    }
    
    public void CallConnectToDynamicMenu()
    {
        ConnectToDynamicMenu();
    }
    
    private string GetButtonText(BaseButton button)
    {
        // Try to get text from different button types
        if (button is Button btn)
            return btn.Text;
        
        // Label is not a BaseButton, so this check won't work
        // Just use the button name as fallback
        return button.Name;
    }
    
    #endregion
    
    #region Helper Methods
    
    private CentralInputManager FindInputManagerInTree(Node node)
    {
        if (node is CentralInputManager im) return im;
        foreach (Node child in node.GetChildren())
        {
            var result = FindInputManagerInTree(child);
            if (result != null) return result;
        }
        return null;
    }
    
    private MenuControls FindMenuControlsInTree(Node node)
    {
        if (node is MenuControls mc) return mc;
        foreach (Node child in node.GetChildren())
        {
            var result = FindMenuControlsInTree(child);
            if (result != null) return result;
        }
        return null;
    }
    
    #endregion
}