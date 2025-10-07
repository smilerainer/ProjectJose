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
    private NPCBehaviorManager npcBehaviorManager;

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
        npcBehaviorManager = new NPCBehaviorManager();

        // Setup dependencies between components
        SetupComponentDependencies();
        InitializeSceneManager();
    }

    // In the SetupComponentDependencies() method, add:
    private void SetupComponentDependencies()
    {
        // Wire up component references
        uiController.Initialize(this);
        actionHandler.Initialize(stateManager, configLoader);
        turnManager.Initialize(stateManager, uiController);
        stateManager.Initialize(this);

        // Initialize NPCBehaviorManager BEFORE connecting it to TurnManager
        npcBehaviorManager.Initialize(stateManager, configLoader, actionHandler);

        // Connect NPC manager to turn manager (AFTER initialization)
        turnManager.SetNPCBehaviorManager(npcBehaviorManager);
        
        // Connect to battle end event ← ADD THIS
        turnManager.OnBattleEnded += OnBattleEnded;
    }

    private void SetupBattle()
    {
        if (configLoader.LoadConfiguration(configFilePath))
        {
            // Use this instead of SetupInitialBattleState()
            stateManager.SetupBattleFromConfig(configLoader.GetBattleConfig());

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

        var actionConfig = actionHandler.GetCurrentActionConfig();
        if (actionConfig != null)
        {
            var validTargets = actionHandler.GetValidTargetsForCurrentAction();
            uiController.StartTargetSelection(validTargets, actionConfig);
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

    #region SceneManager Integration

    private SceneManager sceneManager;

    // Add this to InitializeComponents():
    private void InitializeSceneManager()
    {
        sceneManager = GetNodeOrNull<SceneManager>("/root/SceneManager");

        if (sceneManager != null)
        {
            // Check if we have custom battle config from SceneManager
            if (sceneManager.HasSceneParameter("battle_config"))
            {
                string customConfig = sceneManager.GetSceneParameter("battle_config").AsString();
                if (!string.IsNullOrEmpty(customConfig))
                {
                    configFilePath = customConfig;
                    GD.Print($"[BattleManager] Using config from SceneManager: {configFilePath}");
                }
            }

            // Sync P value to battle state if needed
            int pValue = sceneManager.GetP();
            GD.Print($"[BattleManager] P value from SceneManager: {pValue}");
        }
    }

    // Call this when battle ends
    private void OnBattleEnded(bool playerWon)
    {
        if (sceneManager == null) return;

        // Create and store battle results
        var results = new Dictionary<string, Variant>
        {
            ["victory"] = playerWon,
            ["player_hp"] = stateManager.GetPlayer()?.CurrentHP ?? 0f,
            ["turns_taken"] = turnManager.GetCurrentRound(),
            ["enemies_defeated"] = GetEnemiesDefeatedCount(),
            ["p_earned"] = CalculatePEarned(playerWon)
        };

        sceneManager.StoreBattleResults(results);
        sceneManager.AddP(results["p_earned"].AsInt32());

        GD.Print($"[BattleManager] Battle ended - Victory: {playerWon}");

        // Just advance to next scene in sequence
        sceneManager.LoadNextInSequence();
    }

    private int GetEnemiesDefeatedCount()
    {
        return stateManager.GetAllEntities()
            .Count(e => e.Type == EntityType.Enemy && !e.IsAlive);
    }

    private int CalculatePEarned(bool victory)
    {
        if (!victory) return 0;

        // Base P for victory
        int baseP = 10;

        // Bonus for efficiency (fewer turns)
        int turnBonus = Mathf.Max(0, 10 - turnManager.GetCurrentRound());

        return baseP + turnBonus;
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