using Godot;
using System.Linq;
using System.Collections.Generic;

public partial class BattleManager : Node
{
    #region Node References
    
    private CentralInputManager inputManager;
    private HexGrid hexGrid;
    private MenuControls menuControls;
    private HexControls hexControls;
    
    #endregion
    
    #region Battle State
    
    private Vector2I playerPosition = new Vector2I(0, 0);
    private Vector2I enemyPosition = new Vector2I(3, 0);
    private bool movementModeActive = false;
    private HashSet<Vector2I> validMoves = new();
    private bool isInSubmenu = false;
    private string currentSubmenuType = "";
    
    #endregion
    
    #region Menu Options
    
    private readonly string[] skillOptions = { "Fireball", "Ice Thorn", "Shroud" };
    private readonly string[] itemOptions = { "Potion", "Ether", "Phoenix Down" };
    private readonly string[] talkOptions = { "Negotiate", "Intimidate", "Flee" };
    
    #endregion
    
    #region Initialization
    
    public override void _Ready()
    {
        FindRequiredNodes();
        
        if (ValidateNodes())
        {
            SetupInitialState();
            SetupEntities();
            ConnectSignals();
            
            // Delay the initial player turn to ensure all systems are ready
            CallDeferred(nameof(DelayedStartPlayerTurn));
        }
        else
        {
            GD.PrintErr("[Battle] Failed to find required nodes");
        }
    }
    
    private void DelayedStartPlayerTurn()
    {
        // Add a small delay to ensure CentralInputManager is fully initialized
        GetTree().CreateTimer(0.1).Connect("timeout", 
            new Callable(this, nameof(StartPlayerTurn)), (uint)ConnectFlags.OneShot);
    }
    
    private void FindRequiredNodes()
    {
        hexGrid = GetNode<HexGrid>("../HexGrid");
        hexControls = hexGrid?.GetNodeOrNull<HexControls>("HexControls");
        menuControls = FindMenuControlsInTree(GetTree().CurrentScene);
        inputManager = FindInputManagerInTree(GetTree().CurrentScene);
    }
    
    private bool ValidateNodes()
    {
        return hexGrid != null && 
               menuControls != null && 
               hexControls != null && 
               inputManager != null;
    }
    
    private void SetupInitialState()
    {
        if (hexControls != null)
        {
            hexControls.StartUIOnlyMode(playerPosition);
        }
    }
    
    private void SetupEntities()
    {
        try
        {
            hexGrid.SetTileByCoords(playerPosition, CellLayer.Entity, new Vector2I(0, 0));
            hexGrid.SetOccupied(playerPosition, true);
            
            hexGrid.SetTileByCoords(enemyPosition, CellLayer.Entity, new Vector2I(1, 0));
            hexGrid.SetOccupied(enemyPosition, true);
            
            GD.Print($"[Battle] Entities placed - Player: {playerPosition}, Enemy: {enemyPosition}");
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[Battle] Entity setup failed: {e.Message}");
        }
    }
    
    private void ConnectSignals()
    {
        if (menuControls != null)
            menuControls.ButtonActivated += OnButtonPressed;
        if (hexGrid != null)
            hexGrid.CellSelected += OnCellSelected;
        if (hexControls != null)
            hexControls.MovementCancelled += OnMovementCancelled;
        
        // Connect to input manager's dynamic menu signal
        if (inputManager != null)
            inputManager.DynamicMenuSelection += OnDynamicMenuSelection;
    }
    
    #endregion
    
    #region Turn Management
    
    private void StartPlayerTurn()
    {
        if (menuControls != null)
        {
            menuControls.SetActive(true);
            
            // Ensure the menu is properly focused
            menuControls.ResetToFirstButton();
            
            // Give the input manager a moment to detect the active menu
            GetTree().CreateTimer(0.05).Connect("timeout", 
                new Callable(this, nameof(EnsureMenuFocus)), (uint)ConnectFlags.OneShot);
        }
        
        isInSubmenu = false;
        currentSubmenuType = "";
        GD.Print("[Battle] Player turn started");
    }
    
    private void EnsureMenuFocus()
    {
        // Notify the input manager that the cursor should be updated
        if (inputManager != null)
        {
            inputManager.NotifyButtonFocusChanged();
        }
    }
    
    private void EndPlayerTurn()
    {
        if (menuControls != null)
        {
            menuControls.SetActive(false);
        }
        
        StartEnemyTurn();
    }
    
    private void StartEnemyTurn()
    {
        GD.Print("[Battle] Enemy turn started - skipping back to player");
        StartPlayerTurn();
    }
    
    #endregion
    
    #region Input Handling
    
    private void OnButtonPressed(int index, BaseButton button)
    {
        GD.Print($"[Battle] OnButtonPressed called - Index: {index}, Button: {button.Name}, IsInSubmenu: {isInSubmenu}");
        
        if (isInSubmenu)
        {
            HandleSubmenuSelection(index, button);
        }
        else
        {
            HandleMainMenuSelection(button);
        }
    }
    
    private void HandleMainMenuSelection(BaseButton button)
    {
        string buttonText = button.Name.ToString().ToLower();
        
        if (buttonText.Contains("move"))
        {
            StartMovementMode();
        }
        else if (buttonText.Contains("skill"))
        {
            ShowSubmenu("skill", skillOptions);
        }
        else if (buttonText.Contains("item"))
        {
            ShowSubmenu("item", itemOptions);
        }
        else if (buttonText.Contains("talk"))
        {
            ShowSubmenu("talk", talkOptions);
        }
        else
        {
            GD.Print($"[Battle] Button '{button.Name}' pressed - no action defined");
            StartPlayerTurn();
        }
    }
    
    private void HandleSubmenuSelection(int index, BaseButton button)
    {
        string selectedOption = GetSubmenuOption(index);
        
        GD.Print($"[Battle] {currentSubmenuType.ToUpper()} selected: {selectedOption} (Index: {index})");
        
        switch (currentSubmenuType)
        {
            case "skill":
                ExecuteSkill(selectedOption);
                break;
            case "item":
                UseItem(selectedOption);
                break;
            case "talk":
                ExecuteTalkAction(selectedOption);
                break;
        }
        
        CloseSubmenu();
        // Don't call EndPlayerTurn here - let the player continue
    }
    
    private string GetSubmenuOption(int index)
    {
        string[] currentOptions = currentSubmenuType switch
        {
            "skill" => skillOptions,
            "item" => itemOptions,
            "talk" => talkOptions,
            _ => new string[0]
        };
        
        return index < currentOptions.Length ? currentOptions[index] : "Unknown";
    }
    
    private void OnDynamicMenuSelection(int index, string buttonText)
    {
        if (isInSubmenu)
        {
            GD.Print($"[Battle] Dynamic menu selection - Index: {index}, Text: {buttonText}");
            HandleSubmenuSelectionByText(index, buttonText);
        }
    }
    
    private void HandleSubmenuSelectionByText(int index, string buttonText)
    {
        GD.Print($"[Battle] {currentSubmenuType.ToUpper()} selected: {buttonText} (Index: {index})");
        
        switch (currentSubmenuType)
        {
            case "skill":
                ExecuteSkill(buttonText);
                break;
            case "item":
                UseItem(buttonText);
                break;
            case "talk":
                ExecuteTalkAction(buttonText);
                break;
        }
        
        CloseSubmenu();
    }
    
    #endregion
    
    #region Submenu Management
    
    private void ShowSubmenu(string submenuType, string[] options)
    {
        GD.Print($"[Battle] Opening {submenuType} submenu");
        
        isInSubmenu = true;
        currentSubmenuType = submenuType;
        
        if (inputManager != null)
        {
            // The CentralInputManager handles everything for us
            inputManager.SetMenuButtonArray(options);
            
            // Connect to the dynamic menu after it's created
            ConnectToDynamicMenu();
        }
        else
        {
            GD.PrintErr("[Battle] Cannot show submenu - CentralInputManager not found");
            StartPlayerTurn();
        }
    }
    
    private void ConnectToDynamicMenu()
    {
        // Use a timer to ensure the dynamic menu is fully set up
        GetTree().CreateTimer(0.1).Connect("timeout", 
            new Callable(this, nameof(AttemptDynamicMenuConnection)), (uint)ConnectFlags.OneShot);
    }
    
    private void AttemptDynamicMenuConnection()
    {
        var dynamicMenu = GetDynamicMenuFromInputManager();
        if (dynamicMenu != null)
        {
            GD.Print($"[Battle] Found dynamic menu: {dynamicMenu.Name}");
            
            // Disconnect any existing connection to avoid duplicates
            if (dynamicMenu.IsConnected(MenuControls.SignalName.ButtonActivated, 
                new Callable(this, nameof(OnButtonPressed))))
            {
                dynamicMenu.ButtonActivated -= OnButtonPressed;
            }
            
            // Connect to the dynamic menu
            dynamicMenu.ButtonActivated += OnButtonPressed;
            GD.Print("[Battle] Connected to dynamic menu successfully");
        }
        else
        {
            GD.PrintErr("[Battle] Failed to find dynamic menu - retrying...");
            // Retry a few more times
            GetTree().CreateTimer(0.1).Connect("timeout", 
                new Callable(this, nameof(AttemptDynamicMenuConnection)), (uint)ConnectFlags.OneShot);
        }
    }
    
    private void CloseSubmenu()
    {
        GD.Print("[Battle] Closing submenu");
        
        // Disconnect from dynamic menu
        var dynamicMenu = GetDynamicMenuFromInputManager();
        if (dynamicMenu != null)
        {
            if (dynamicMenu.IsConnected(MenuControls.SignalName.ButtonActivated, 
                new Callable(this, nameof(OnButtonPressed))))
            {
                dynamicMenu.ButtonActivated -= OnButtonPressed;
            }
        }
        
        isInSubmenu = false;
        currentSubmenuType = "";
        
        if (inputManager != null)
        {
            inputManager.ClearDynamicMenu();
        }
        
        StartPlayerTurn();
    }
    
    private MenuControls GetDynamicMenuFromInputManager()
    {
        // Access the dynamic menu through the input manager's method
        // We need to replicate the GetDynamicMenu logic from CentralInputManager
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
        // Look for the dynamic menu root in the scene
        // This should match the dynamicMenuRoot from CentralInputManager
        var ui = GetTree().CurrentScene.GetNodeOrNull<CanvasLayer>("UI");
        if (ui != null)
        {
            var dynamicRoot = ui.GetNodeOrNull<Control>("DynamicMenuRoot");
            if (dynamicRoot != null) return dynamicRoot;
        }
        
        // Fallback: search for it in the main Control node
        var control = GetTree().CurrentScene.GetNodeOrNull<Control>("Control");
        if (control != null)
        {
            var dynamicRoot = control.GetNodeOrNull<Control>("DynamicMenuRoot");
            if (dynamicRoot != null) return dynamicRoot;
        }
        
        // Last resort: search the entire scene for any Control with "Dynamic" in the name
        return FindDynamicMenuRootRecursive(GetTree().CurrentScene);
    }
    
    private Control FindDynamicMenuRootRecursive(Node node)
    {
        if (node is Control control && node.Name.ToString().Contains("Dynamic"))
        {
            // Check if this control contains MenuControls
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

#region Movement System

    private void StartMovementMode()
    {
        GD.Print("[Battle] Starting movement mode");

        // Deactivate menu first and clear any submenu state
        if (menuControls != null)
        {
            menuControls.ReleaseFocus();
            menuControls.SetActive(false);
        }
        
        // Clear any dynamic menu state
        if (inputManager != null)
        {
            inputManager.ClearDynamicMenu();
        }

        movementModeActive = true;
        validMoves = GetAdjacentWalkableTiles(playerPosition);
        UpdateMovementVisuals();

        if (hexControls != null)
        {
            hexControls.SetActive(true);
            hexControls.SetValidMoves(validMoves);
            hexControls.EnterMovementMode();
            
            GD.Print($"[Battle] Movement mode ready - InputManager will auto-detect HexGrid context");
        }
    }
    
    private void ExitMovementMode()
    {
        movementModeActive = false;
        ClearMovementVisuals();
        
        if (hexControls != null)
        {
            hexControls.ExitMovementMode(playerPosition);
            hexControls.SetActive(false); // Deactivate hex controls
        }
        
        GD.Print("[Battle] Exited movement mode");
        StartPlayerTurn();
    }
    
    private void OnMovementCancelled()
    {
        GD.Print("[Battle] Movement cancelled by player");
        ExitMovementMode();
    }
    
    private void OnCellSelected(Vector2I cell)
    {
        if (!movementModeActive) return;
        
        if (validMoves.Contains(cell))
        {
            ExecuteMove(cell);
        }
        else
        {
            GD.Print($"[Battle] Invalid move to {cell}");
        }
    }
    
    private void ExecuteMove(Vector2I targetPosition)
    {
        if (!validMoves.Contains(targetPosition))
        {
            GD.Print($"[Battle] Cannot move to {targetPosition} - invalid");
            return;
        }
        
        GD.Print($"[Battle] Moving player from {playerPosition} to {targetPosition}");
        
        hexGrid.ClearTile(playerPosition, CellLayer.Entity);
        hexGrid.SetOccupied(playerPosition, false);
        
        playerPosition = targetPosition;
        hexGrid.SetTileByCoords(playerPosition, CellLayer.Entity, new Vector2I(0, 0));
        hexGrid.SetOccupied(playerPosition, true);
        
        ExitMovementMode();
        EndPlayerTurn();
    }
    
    private HashSet<Vector2I> GetAdjacentWalkableTiles(Vector2I center)
    {
        var adjacent = new HashSet<Vector2I>();
        var neighbors = hexGrid.GetNeighbors(center);
        
        foreach (var neighbor in neighbors)
        {
            adjacent.Add(neighbor);
        }
        
        return adjacent;
    }
    
    private void UpdateMovementVisuals()
    {
        var markerLayer = hexGrid.GetLayer(CellLayer.Marker);
        markerLayer?.Clear();
        
        foreach (var move in validMoves)
        {
            hexGrid.SetTileByCoords(move, CellLayer.Marker, new Vector2I(1, 0));
        }
    }
    
    private void ClearMovementVisuals()
    {
        var markerLayer = hexGrid.GetLayer(CellLayer.Marker);
        markerLayer?.Clear();
        
        var cursorLayer = hexGrid.GetLayer(CellLayer.Cursor);
        cursorLayer?.Clear();
    }
    
    #endregion
    
    #region Battle Actions
    
    private void ExecuteSkill(string skillName)
    {
        switch (skillName)
        {
            case "Fireball":
                GD.Print("[Battle] Casting Fireball!");
                break;
            case "Ice Thorn":
                GD.Print("[Battle] Casting Ice Thorn!");
                break;
            case "Shroud":
                GD.Print("[Battle] Casting Shroud!");
                break;
            default:
                GD.Print($"[Battle] Unknown skill: {skillName}");
                break;
        }
    }
    
    private void UseItem(string itemName)
    {
        switch (itemName)
        {
            case "Potion":
                GD.Print("[Battle] Using Potion!");
                break;
            case "Ether":
                GD.Print("[Battle] Using Ether!");
                break;
            case "Phoenix Down":
                GD.Print("[Battle] Using Phoenix Down!");
                break;
            default:
                GD.Print($"[Battle] Unknown item: {itemName}");
                break;
        }
    }
    
    private void ExecuteTalkAction(string action)
    {
        switch (action)
        {
            case "Negotiate":
                GD.Print("[Battle] Attempting to negotiate!");
                break;
            case "Intimidate":
                GD.Print("[Battle] Attempting to intimidate!");
                break;
            case "Flee":
                GD.Print("[Battle] Attempting to flee!");
                break;
            default:
                GD.Print($"[Battle] Unknown talk action: {action}");
                break;
        }
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