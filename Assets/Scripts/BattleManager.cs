using Godot;
using System.Linq;
using System.Collections.Generic;
using CustomJsonSystem;

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
    private bool targetSelectionModeActive = false;
    private bool isInSubmenu = false;
    private string currentSubmenuType = "";
    private string selectedActionOption = "";
    
    // Current action flow state
    private enum ActionPhase
    {
        MainMenu,           // Show Move/Skill/Item/Talk buttons
        SubmenuSelection,   // Show specific options (Fireball/Ice Thorn/etc)
        TargetSelection,    // Use hex grid to select target
        ActionComplete      // Process action and return to main menu
    }
    
    private ActionPhase currentPhase = ActionPhase.MainMenu;
    
    #endregion
    
    #region Configuration Data
    
    [Export] private string configFilePath = "res://data/battle_config.json";
    private BattleConfigData battleConfig;
    private Dictionary<string, ActionConfig> allActions = new();
    
    #endregion
    
    #region Initialization
    
    public override void _Ready()
    {
        LoadConfiguration();
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
    
    private void LoadConfiguration()
    {
        GD.Print($"[Battle] Loading configuration from: {configFilePath}");
        
        battleConfig = CustomJsonLoader.LoadBattleConfig(configFilePath);
        
        // Build lookup dictionary for all actions
        allActions.Clear();
        
        foreach (var skill in battleConfig.Skills)
            allActions[skill.Id] = skill;
        foreach (var item in battleConfig.Items)
            allActions[item.Id] = item;
        foreach (var talk in battleConfig.TalkOptions)
            allActions[talk.Id] = talk;
        foreach (var move in battleConfig.MoveOptions)
            allActions[move.Id] = move;
            
        GD.Print($"[Battle] Loaded {allActions.Count} total actions:");
        GD.Print($"  Skills: {battleConfig.Skills.Count}");
        GD.Print($"  Items: {battleConfig.Items.Count}");
        GD.Print($"  Talk Options: {battleConfig.TalkOptions.Count}");
        GD.Print($"  Move Options: {battleConfig.MoveOptions.Count}");
    }
    
    private void DelayedStartPlayerTurn()
    {
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
            hexGrid.SetTileWithCoords(playerPosition, CellLayer.Entity, new Vector2I(0, 0));
            hexGrid.SetCellOccupied(playerPosition, true);
            
            hexGrid.SetTileWithCoords(enemyPosition, CellLayer.Entity, new Vector2I(1, 0));
            hexGrid.SetCellOccupied(enemyPosition, true);
            
            // Set metadata for entities using the new system
            hexGrid.SetCellMetadata(playerPosition, "description", "Player Character");
            hexGrid.SetCellMetadata(enemyPosition, "description", "Enemy Warrior");
            
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
            hexControls.InteractionCancelled += OnCellInteractionCancelled;
        
        if (inputManager != null)
            inputManager.DynamicMenuSelection += OnDynamicMenuSelection;
    }
    
    #endregion
    
    #region Turn Management
    
    private void StartPlayerTurn()
    {
        currentPhase = ActionPhase.MainMenu;
        targetSelectionModeActive = false;
        isInSubmenu = false;
        currentSubmenuType = "";
        selectedActionOption = "";
        
        if (menuControls != null)
        {
            menuControls.SetActive(true);
            menuControls.ResetToFirstButton();
            
            GetTree().CreateTimer(0.05).Connect("timeout", 
                new Callable(this, nameof(EnsureMenuFocus)), (uint)ConnectFlags.OneShot);
        }
        
        GD.Print("[Battle] Player turn started - select action type");
    }
    
    private void EnsureMenuFocus()
    {
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
        GD.Print($"[Battle] OnButtonPressed called - Index: {index}, Button: {button.Name}, Phase: {currentPhase}");
        
        switch (currentPhase)
        {
            case ActionPhase.MainMenu:
                HandleMainMenuSelection(button);
                break;
            case ActionPhase.SubmenuSelection:
                HandleSubmenuSelection(index, button);
                break;
        }
    }
    
    private void HandleMainMenuSelection(BaseButton button)
    {
        string buttonText = button.Name.ToString().ToLower();
        
        if (buttonText.Contains("move"))
        {
            currentSubmenuType = "move";
            ShowSubmenu("move", GetActionNames(battleConfig.MoveOptions));
        }
        else if (buttonText.Contains("skill"))
        {
            currentSubmenuType = "skill";
            ShowSubmenu("skill", GetActionNames(battleConfig.Skills));
        }
        else if (buttonText.Contains("item"))
        {
            currentSubmenuType = "item";
            ShowSubmenu("item", GetActionNames(battleConfig.Items));
        }
        else if (buttonText.Contains("talk"))
        {
            currentSubmenuType = "talk";
            ShowSubmenu("talk", GetActionNames(battleConfig.TalkOptions));
        }
        else
        {
            GD.Print($"[Battle] Button '{button.Name}' pressed - no action defined");
            StartPlayerTurn();
        }
    }
    
    private string[] GetActionNames(List<ActionConfig> actions)
    {
        return actions.Select(a => a.Name).ToArray();
    }
    
    private void HandleSubmenuSelection(int index, BaseButton button)
    {
        selectedActionOption = GetSubmenuOption(index);
        
        GD.Print($"[Battle] {currentSubmenuType.ToUpper()} selected: {selectedActionOption} (Index: {index})");
        
        CloseSubmenu();
        StartCellInteraction();
    }
    
    private string GetSubmenuOption(int index)
    {
        List<ActionConfig> currentOptions = currentSubmenuType switch
        {
            "move" => battleConfig.MoveOptions,
            "skill" => battleConfig.Skills,
            "item" => battleConfig.Items,
            "talk" => battleConfig.TalkOptions,
            _ => new List<ActionConfig>()
        };
        
        return index < currentOptions.Count ? currentOptions[index].Name : "Unknown";
    }
    
    private void OnDynamicMenuSelection(int index, string buttonText)
    {
        if (currentPhase == ActionPhase.SubmenuSelection)
        {
            selectedActionOption = buttonText;
            GD.Print($"[Battle] Dynamic menu selection - {currentSubmenuType.ToUpper()}: {buttonText} (Index: {index})");
            
            CloseSubmenu();
            StartCellInteraction();
        }
    }
    
    #endregion
    
    #region Submenu Management
    
    private void ShowSubmenu(string submenuType, string[] options)
    {
        GD.Print($"[Battle] Opening {submenuType} submenu");
        
        currentPhase = ActionPhase.SubmenuSelection;
        isInSubmenu = true;
        
        if (inputManager != null)
        {
            inputManager.SetMenuButtonArray(options);
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
        GetTree().CreateTimer(0.1).Connect("timeout", 
            new Callable(this, nameof(AttemptDynamicMenuConnection)), (uint)ConnectFlags.OneShot);
    }
    
    private void AttemptDynamicMenuConnection()
    {
        var dynamicMenu = GetDynamicMenuFromInputManager();
        if (dynamicMenu != null)
        {
            GD.Print($"[Battle] Found dynamic menu: {dynamicMenu.Name}");
            
            if (dynamicMenu.IsConnected(MenuControls.SignalName.ButtonActivated, 
                new Callable(this, nameof(OnButtonPressed))))
            {
                dynamicMenu.ButtonActivated -= OnButtonPressed;
            }
            
            dynamicMenu.ButtonActivated += OnButtonPressed;
            GD.Print("[Battle] Connected to dynamic menu successfully");
        }
        else
        {
            GD.PrintErr("[Battle] Failed to find dynamic menu - retrying...");
            GetTree().CreateTimer(0.1).Connect("timeout", 
                new Callable(this, nameof(AttemptDynamicMenuConnection)), (uint)ConnectFlags.OneShot);
        }
    }
    
    private void CloseSubmenu()
    {
        GD.Print("[Battle] Closing submenu");
        
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
        
        if (inputManager != null)
        {
            inputManager.ClearDynamicMenu();
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
        var ui = GetTree().CurrentScene.GetNodeOrNull<CanvasLayer>("UI");
        if (ui != null)
        {
            var dynamicRoot = ui.GetNodeOrNull<Control>("DynamicMenuRoot");
            if (dynamicRoot != null) return dynamicRoot;
        }
        
        var control = GetTree().CurrentScene.GetNodeOrNull<Control>("Control");
        if (control != null)
        {
            var dynamicRoot = control.GetNodeOrNull<Control>("DynamicMenuRoot");
            if (dynamicRoot != null) return dynamicRoot;
        }
        
        return FindDynamicMenuRootRecursive(GetTree().CurrentScene);
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

    #region Cell Interaction System

    private void StartCellInteraction()
    {
        GD.Print($"[Battle] Starting cell interaction for {currentSubmenuType}: {selectedActionOption}");

        currentPhase = ActionPhase.TargetSelection;
        
        if (menuControls != null)
        {
            menuControls.ReleaseFocus();
            menuControls.SetActive(false);
        }

        targetSelectionModeActive = true;
        
        var actionConfig = GetActionConfig(selectedActionOption);
        if (actionConfig == null)
        {
            GD.PrintErr($"[Battle] Action config not found for: {selectedActionOption}");
            StartPlayerTurn();
            return;
        }
        
        GD.Print($"[Battle] Action config loaded - Range: {actionConfig.Range}, TargetType: {actionConfig.TargetType}");
        GD.Print($"[Battle] Range pattern: {actionConfig.RangePattern.Count} cells, AOE pattern: {actionConfig.AoePattern.Count} cells");
        
        var validTargets = CalculateValidTargets(actionConfig);
        
        if (hexGrid != null)
        {
            hexGrid.ShowRangeHighlight(validTargets);
        }
        
        if (hexControls != null)
        {
            hexControls.SetActive(true);
            hexControls.SetValidCells(validTargets.ToHashSet());
            
            var aoePattern = actionConfig.AoePattern.Select(p => p.ToVector2I()).ToList();
            hexControls.SetTargetingInfo(actionConfig.TargetType, aoePattern);
            
            hexControls.EnterInteractionMode();
        }
        
        GD.Print($"[Battle] Cell interaction ready - {validTargets.Count} valid targets");
    }
    
    private List<Vector2I> CalculateValidTargets(ActionConfig actionConfig)
    {
        var validTargets = new List<Vector2I>();
        var rangePattern = actionConfig.RangePattern.Select(p => p.ToVector2I()).ToList();
        
        GD.Print($"[Battle] Calculating valid targets from player position {playerPosition}");
        
        if (rangePattern.Count == 0)
        {
            GD.Print("[Battle] No range pattern defined, using default adjacent pattern");
            rangePattern = new List<Vector2I>
            {
                new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, -1), new(-1, -1)
            };
        }
        
        foreach (var offset in rangePattern)
        {
            var targetCell = playerPosition + offset;
            
            if (hexGrid != null && hexGrid.IsValidCell(targetCell))
            {
                bool canTarget = CanTargetCell(targetCell, actionConfig.TargetType);
                
                if (canTarget)
                {
                    validTargets.Add(targetCell);
                    GD.Print($"[Battle] Valid target: {targetCell} (offset {offset})");
                }
                else
                {
                    GD.Print($"[Battle] Invalid target: {targetCell} (blocked by targeting rules)");
                }
            }
            else
            {
                GD.Print($"[Battle] Invalid target: {targetCell} (off grid or invalid)");
            }
        }
        
        if (hexGrid != null && hexGrid.CanTargetSelf(actionConfig.TargetType))
        {
            if (!validTargets.Contains(playerPosition))
            {
                validTargets.Add(playerPosition);
                GD.Print($"[Battle] Added self-target: {playerPosition}");
            }
        }
        
        return validTargets;
    }
    
    private bool CanTargetCell(Vector2I cell, string targetType)
    {
        switch (targetType.ToLower())
        {
            case "self":
                return cell == playerPosition;
                
            case "ally":
                return cell == playerPosition;
                
            case "enemy":
                return cell == enemyPosition;
                
            case "movement":
                return hexGrid != null && !hexGrid.IsOccupiedCell(cell);
                
            case "area":
            case "any":
                return true;
                
            default:
                GD.Print($"[Battle] Unknown target type: {targetType}");
                return true;
        }
    }
    
    private void OnCellInteractionCancelled()
    {
        GD.Print("[Battle] Cell interaction cancelled by player");
        ExitCellInteraction();
        StartPlayerTurn();
    }
    
    private void OnCellSelected(Vector2I cell)
    {
        if (!targetSelectionModeActive) return;
        
        GD.Print($"[Battle] Cell selected: {cell}");
        
        PrintCellContents(cell);
        
        var actionConfig = GetActionConfig(selectedActionOption);
        if (actionConfig != null && actionConfig.AoePattern.Count > 0)
        {
            ShowAoeEffect(cell, actionConfig);
        }
        
        ExecuteAction(cell);
        
        ExitCellInteraction();
        EndPlayerTurn();
    }
    
    private void ShowAoeEffect(Vector2I targetCell, ActionConfig actionConfig)
    {
        GD.Print($"=== AOE EFFECT AT {targetCell} ===");
        
        var aoePattern = actionConfig.AoePattern.Select(p => p.ToVector2I()).ToList();
        
        foreach (var offset in aoePattern)
        {
            var affectedCell = targetCell + offset;
            GD.Print($"  AOE affects cell: {affectedCell} (offset {offset})");
            
            if (affectedCell == playerPosition)
                GD.Print($"    - Player affected!");
            if (affectedCell == enemyPosition)
                GD.Print($"    - Enemy affected!");
        }
        
        GD.Print("========================");
    }
    
    private void PrintCellContents(Vector2I cell)
    {
        GD.Print($"=== CELL CONTENTS AT {cell} ===");
        
        foreach (CellLayer layer in System.Enum.GetValues<CellLayer>())
        {
            var tileLayer = hexGrid.GetLayer(layer);
            if (tileLayer != null)
            {
                var sourceId = tileLayer.GetCellSourceId(cell);
                var atlasCoords = tileLayer.GetCellAtlasCoords(cell);
                
                if (sourceId != -1)
                {
                    string description = GetTileDescription(layer, atlasCoords);
                    GD.Print($"  {layer}: {description} (Source: {sourceId}, Atlas: {atlasCoords})");
                }
            }
        }
        
        bool isOccupied = hexGrid.IsOccupiedCell(cell);
        GD.Print($"  Occupied: {isOccupied}");
        
        if (cell == playerPosition)
            GD.Print($"  Entity: Player");
        if (cell == enemyPosition)
            GD.Print($"  Entity: Enemy");
            
        var metadata = hexGrid.GetCellMetadata(cell, "description");
        if (!string.IsNullOrEmpty(metadata))
            GD.Print($"  Description: {metadata}");
            
        GD.Print("========================");
    }
    
    private string GetTileDescription(CellLayer layer, Vector2I atlasCoords)
    {
        return layer switch
        {
            CellLayer.Entity when atlasCoords == Vector2I.Zero => "Player (Yellow)",
            CellLayer.Entity when atlasCoords == new Vector2I(1, 0) => "Enemy (Red)",
            CellLayer.Marker when atlasCoords == new Vector2I(1, 0) => "Movement Marker",
            CellLayer.Cursor => "Cursor",
            _ => $"Tile at {atlasCoords}"
        };
    }
    
    private void ExecuteAction(Vector2I targetCell)
    {
        var actionConfig = GetActionConfig(selectedActionOption);
        if (actionConfig == null)
        {
            GD.PrintErr($"[Battle] Action config not found for: {selectedActionOption}");
            return;
        }
        
        switch (currentSubmenuType)
        {
            case "move":
                ExecuteMove(targetCell, actionConfig);
                break;
            case "skill":
                ExecuteSkill(selectedActionOption, targetCell, actionConfig);
                break;
            case "item":
                UseItem(selectedActionOption, targetCell, actionConfig);
                break;
            case "talk":
                ExecuteTalkAction(selectedActionOption, targetCell, actionConfig);
                break;
        }
    }
    
    private ActionConfig GetActionConfig(string actionName)
    {
        foreach (var kvp in allActions)
        {
            if (kvp.Value.Name == actionName)
                return kvp.Value;
        }
        return null;
    }
    
    private void ExitCellInteraction()
    {
        targetSelectionModeActive = false;
        currentPhase = ActionPhase.ActionComplete;
        
        if (hexGrid != null)
        {
            hexGrid.ClearRangeHighlight();
            hexGrid.ClearAoePreview();
        }
        
        if (hexControls != null)
        {
            hexControls.ExitInteractionMode(playerPosition);
            hexControls.SetActive(false);
        }
        
        GD.Print("[Battle] Exited cell interaction");
    }
    
    #endregion
    
    #region Battle Actions
    
    private void ExecuteMove(Vector2I targetPosition, ActionConfig moveConfig)
    {
        var validMoves = CalculateValidTargets(moveConfig);
        
        if (!validMoves.Contains(targetPosition))
        {
            GD.Print($"[Battle] Cannot move to {targetPosition} - invalid position");
            return;
        }
        
        GD.Print($"[Battle] Moving player from {playerPosition} to {targetPosition}");
        GD.Print($"[Battle] Move type: {moveConfig.Name} - {moveConfig.Description}");
        
        hexGrid.ClearTile(playerPosition, CellLayer.Entity);
        hexGrid.SetCellOccupied(playerPosition, false);
        hexGrid.SetCellMetadata(playerPosition, "description", "");
        
        playerPosition = targetPosition;
        hexGrid.SetTileWithCoords(playerPosition, CellLayer.Entity, new Vector2I(0, 0));
        hexGrid.SetCellOccupied(playerPosition, true);
        hexGrid.SetCellMetadata(playerPosition, "description", "Player Character");
        
        GD.Print($"[Battle] Move action '{moveConfig.Name}' completed");
        
        if (moveConfig.Cost > 0)
        {
            GD.Print($"[Battle] Move cost: {moveConfig.Cost}");
        }
    }
    
    private void ExecuteSkill(string skillName, Vector2I targetCell, ActionConfig skillConfig)
    {
        GD.Print($"[Battle] Executing skill '{skillName}' on target {targetCell}");
        GD.Print($"[Battle] {skillConfig.Description}");
        GD.Print($"[Battle] Target type: {skillConfig.TargetType}");
        
        if (skillConfig.Damage > 0)
        {
            GD.Print($"[Battle] Base damage: {skillConfig.Damage}");
        }
        
        if (!string.IsNullOrEmpty(skillConfig.StatusEffect))
        {
            GD.Print($"[Battle] Applying {skillConfig.StatusEffect} for {skillConfig.StatusDuration} turns!");
        }
        
        if (skillConfig.Cost > 0)
        {
            GD.Print($"[Battle] Skill cost: {skillConfig.Cost}");
        }
        
        GD.Print($"[Battle] Range pattern: {skillConfig.RangePattern.Count} cells");
        GD.Print($"[Battle] AOE pattern: {skillConfig.AoePattern.Count} cells");
    }
    
    private void UseItem(string itemName, Vector2I targetCell, ActionConfig itemConfig)
    {
        GD.Print($"[Battle] Using item '{itemName}' on target {targetCell}");
        GD.Print($"[Battle] {itemConfig.Description}");
        GD.Print($"[Battle] Target type: {itemConfig.TargetType}");
        
        if (itemConfig.HealAmount > 0)
        {
            GD.Print($"[Battle] Healing {itemConfig.HealAmount} HP!");
        }
        
        if (itemConfig.Damage > 0)
        {
            GD.Print($"[Battle] Dealing {itemConfig.Damage} damage!");
        }
        
        if (!string.IsNullOrEmpty(itemConfig.StatusEffect))
        {
            GD.Print($"[Battle] Applying {itemConfig.StatusEffect}!");
        }
        
        if (itemConfig.UsesRemaining > 0)
        {
            itemConfig.UsesRemaining--;
            GD.Print($"[Battle] {itemName} remaining uses: {itemConfig.UsesRemaining}");
        }
        else if (itemConfig.UsesRemaining == 0)
        {
            GD.Print($"[Battle] {itemName} is used up!");
        }
        else
        {
            GD.Print($"[Battle] {itemName} has unlimited uses");
        }
        
        GD.Print($"[Battle] Range pattern: {itemConfig.RangePattern.Count} cells");
        GD.Print($"[Battle] AOE pattern: {itemConfig.AoePattern.Count} cells");
    }
    
    private void ExecuteTalkAction(string action, Vector2I targetCell, ActionConfig talkConfig)
    {
        GD.Print($"[Battle] Executing talk action '{action}' on target {targetCell}");
        GD.Print($"[Battle] Target type: {talkConfig.TargetType}");
        
        if (!string.IsNullOrEmpty(talkConfig.Dialogue))
        {
            GD.Print($"[Battle] Player says: \"{talkConfig.Dialogue}\"");
        }
        
        if (talkConfig.FriendshipChange != 0)
        {
            GD.Print($"[Battle] Friendship change: {(talkConfig.FriendshipChange > 0 ? "+" : "")}{talkConfig.FriendshipChange}");
        }
        
        if (talkConfig.ReputationChange != 0)
        {
            GD.Print($"[Battle] Reputation change: {(talkConfig.ReputationChange > 0 ? "+" : "")}{talkConfig.ReputationChange}");
        }
        
        if (talkConfig.Cost > 0)
        {
            GD.Print($"[Battle] Talk action cost: {talkConfig.Cost}");
        }
        
        GD.Print($"[Battle] Range pattern: {talkConfig.RangePattern.Count} cells");
        GD.Print($"[Battle] AOE pattern: {talkConfig.AoePattern.Count} cells");
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