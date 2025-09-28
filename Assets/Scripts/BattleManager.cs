// BattleManager.cs - Main coordinator for battle system
using Godot;
using System.Collections.Generic;
using CustomJsonSystem;
using System.Linq;

public partial class BattleManager : Node
{
    #region Dependencies
    
    private BattleStateManager stateManager;
    private BattleUIController uiController;
    private BattleActionHandler actionHandler;
    private BattleConfigurationLoader configLoader;
    private TurnManager turnManager;
    
    #endregion
    
    #region Helper Methods for UI Controller
    
    // Called by BattleUIController via CallDeferred
    public void CallEnsureMenuFocus()
    {
        uiController?.CallEnsureMenuFocus();
    }
    
    public void CallConnectToDynamicMenu()
    {
        uiController?.CallConnectToDynamicMenu();
    }
    
    #endregion
    
    #region Configuration
    
    [Export] private string configFilePath = "res://data/battle_config.json";
    
    #endregion
    
    #region Initialization
    
    public override void _Ready()
    {
        InitializeComponents();
        SetupBattle();
    }
    
    private void InitializeComponents()
    {
        // Initialize all battle subsystems
        configLoader = new BattleConfigurationLoader();
        stateManager = new BattleStateManager();
        uiController = new BattleUIController();
        actionHandler = new BattleActionHandler();
        turnManager = new TurnManager();
        
        // Setup dependencies between components
        SetupComponentDependencies();
    }
    
    private void SetupComponentDependencies()
    {
        // Wire up component references
        uiController.Initialize(this);
        actionHandler.Initialize(stateManager, configLoader);
        turnManager.Initialize(stateManager, uiController);
        stateManager.Initialize(this);
    }
    
    private void SetupBattle()
    {
        if (configLoader.LoadConfiguration(configFilePath))
        {
            stateManager.SetupInitialBattleState();
            uiController.SetupUI();
            turnManager.StartBattle();
        }
        else
        {
            GD.PrintErr("[BattleManager] Failed to load battle configuration");
        }
    }
    
    #endregion
    
    #region Public API - Events from UI
    
    public void OnActionRequested(string actionType, string actionName)
    {
        actionHandler.ProcessActionRequest(actionType, actionName);
        
        // Show submenu through UI controller
        var availableActions = GetAvailableActionsForType(actionType);
        if (availableActions.Length > 0)
        {
            uiController.ShowSubmenu(availableActions);
        }
    }
    
    public void OnSubmenuSelection(string actionName)
    {
        actionHandler.ProcessSubmenuSelection(actionName);
        
        // Start target selection if action config is valid
        var actionConfig = configLoader.GetActionConfig(actionName);
        if (actionConfig != null)
        {
            var validTargets = actionHandler.GetValidTargetsForCurrentAction();
            var aoePattern = actionConfig.AoePattern.Select(p => p.ToVector2I()).ToList();
            
            uiController.StartTargetSelection(validTargets, aoePattern, actionConfig.TargetType);
        }
    }
    
    public void OnTargetSelected(Vector2I targetCell)
    {
        actionHandler.ProcessTargetSelection(targetCell);
        uiController.EndTargetSelection();
        turnManager.EndPlayerTurn();
    }
    
    public void OnActionCancelled()
    {
        actionHandler.CancelCurrentAction();
        uiController.EndTargetSelection();
        turnManager.ReturnToActionSelection();
    }
    
    #endregion
    
    #region Component Access (for dependencies)
    
    public BattleStateManager GetStateManager() => stateManager;
    public BattleUIController GetUIController() => uiController;
    public BattleActionHandler GetActionHandler() => actionHandler;
    public BattleConfigurationLoader GetConfigLoader() => configLoader;
    public TurnManager GetTurnManager() => turnManager;
    
    #endregion
    
    #region Helper Methods
    
    private string[] GetAvailableActionsForType(string actionType)
    {
        return actionType switch
        {
            "move" => configLoader.GetMoveOptionNames(),
            "skill" => configLoader.GetSkillNames(),
            "item" => configLoader.GetItemNames(),
            "talk" => configLoader.GetTalkOptionNames(),
            _ => new string[0]
        };
    }
    
    #endregion
}