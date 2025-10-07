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
    private SceneManager sceneManager;

    #endregion

    #region Configuration

    [Export] private string configFilePath = "res://data/battle_config.json";

    #endregion

    #region Initialization

    public override void _Ready()
    {
        CallDeferred(nameof(DeferredInitialize));
    }

    private void DeferredInitialize()
    {
        LoadBattleMap();
        
        // Force CentralInputManager to rediscover controls now that the map exists
        var inputManager = GetTree().CurrentScene.GetNodeOrNull<CentralInputManager>("CentralInputManager");
        if (inputManager != null)
        {
            inputManager.RediscoverControls();
        }
        
        InitializeComponents();
        SetupBattle();
    }

    private void LoadBattleMap()
    {
        sceneManager = GetNodeOrNull<SceneManager>("/root/SceneManager");
        if (sceneManager == null)
        {
            GD.PrintErr("[BattleManager] SceneManager not found!");
            return;
        }

        if (!sceneManager.HasSceneParameter("map"))
        {
            GD.PrintErr("[BattleManager] No map parameter specified!");
            return;
        }

        string mapPath = sceneManager.GetSceneParameter("map").AsString();
        var mapScene = GD.Load<PackedScene>(mapPath);

        if (mapScene == null)
        {
            GD.PrintErr($"[BattleManager] Failed to load map: {mapPath}");
            return;
        }

        var mapInstance = mapScene.Instantiate<HexGrid>();
        if (mapInstance == null)
        {
            GD.PrintErr($"[BattleManager] Map instance is not a HexGrid: {mapPath}");
            return;
        }

        mapInstance.Name = "HexGrid";

        // Add HexGrid to the scene
        GetParent().AddChild(mapInstance);
        GetParent().MoveChild(mapInstance, 0);

        // Now move HexControls to be a child of HexGrid
        var hexControls = GetParent().GetNodeOrNull<HexControls>("HexControls");
        if (hexControls != null)
        {
            hexControls.GetParent().RemoveChild(hexControls);
            mapInstance.AddChild(hexControls);
            hexControls.FinalizeSetup(); // Initialize now that it has the correct parent
            GD.Print("[BattleManager] Reparented HexControls to HexGrid");
        }
        else
        {
            GD.PrintErr("[BattleManager] HexControls not found - cannot reparent");
        }

        GD.Print($"[BattleManager] Loaded map: {mapPath}");
    }

    private void InitializeComponents()
    {
        configLoader = new BattleConfigurationLoader();
        stateManager = new BattleStateManager();
        uiController = new BattleUIController();
        actionHandler = new BattleActionHandler();
        turnManager = new TurnManager();
        npcBehaviorManager = new NPCBehaviorManager();

        SetupComponentDependencies();
        InitializeSceneManager();
    }

    private void SetupComponentDependencies()
    {
        // Initialize components that DON'T need HexGrid first
        actionHandler.Initialize(stateManager, configLoader);
        turnManager.Initialize(stateManager, uiController);
        npcBehaviorManager.Initialize(stateManager, configLoader, actionHandler);
        turnManager.SetNPCBehaviorManager(npcBehaviorManager);
        turnManager.OnBattleEnded += OnBattleEnded;
        
        // Initialize components that NEED HexGrid last (after map is loaded)
        stateManager.Initialize(this);
        uiController.Initialize(this);
    }

    private void InitializeSceneManager()
    {
        if (sceneManager != null)
        {
            if (sceneManager.HasSceneParameter("battle_config"))
            {
                string customConfig = sceneManager.GetSceneParameter("battle_config").AsString();
                if (!string.IsNullOrEmpty(customConfig))
                {
                    configFilePath = customConfig;
                    GD.Print($"[BattleManager] Using config from SceneManager: {configFilePath}");
                }
            }

            int pValue = sceneManager.GetP();
            GD.Print($"[BattleManager] P value from SceneManager: {pValue}");
        }
    }

    private void SetupBattle()
    {
        if (configLoader.LoadConfiguration(configFilePath))
        {
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

    #region Battle End Handling

    private void OnBattleEnded(bool playerWon)
    {
        if (sceneManager == null) return;

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
        GD.Print($"[BattleManager] P earned: {results["p_earned"].AsInt32()}");

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

        int baseP = 10;
        int turnBonus = Mathf.Max(0, 10 - turnManager.GetCurrentRound());

        return baseP + turnBonus;
    }

    #endregion

    #region Component Access

    public BattleStateManager GetStateManager() => stateManager;
    public BattleUIController GetUIController() => uiController;
    public BattleActionHandler GetActionHandler() => actionHandler;
    public BattleConfigurationLoader GetConfigLoader() => configLoader;
    public TurnManager GetTurnManager() => turnManager;

    #endregion

    #region Helper Methods

    public void CallEnsureMenuFocus()
    {
        uiController?.CallEnsureMenuFocus();
    }

    public void CallConnectToDynamicMenu()
    {
        uiController?.CallConnectToDynamicMenu();
    }

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