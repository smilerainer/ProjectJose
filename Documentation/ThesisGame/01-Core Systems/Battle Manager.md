
The central coordinator for the entire battle system.

---

## Overview

**BattleManager.cs** is the entry point and orchestrator for all battle functionality. It initializes subsystems, loads configurations, and routes events between UI and game logic.

**Philosophy**: "The conductor of the orchestra" - doesn't play instruments itself, but ensures everyone plays together.

---

## Core Responsibilities

### 1. Initialization
- Load battle map dynamically
- Create and wire up all manager instances
- Initialize SceneManager integration
- Setup battle from configuration

### 2. Event Routing
- Receive UI events (button presses, target selection)
- Route to appropriate subsystem
- Coordinate between managers

### 3. Lifecycle Management
- Handle battle start
- Process battle end
- Transition to next scene

### 4. Dependency Injection
- Provide managers access to each other
- Act as service locator for subsystems

---

## Architecture

```
BattleManager (Coordinator)
    │
    ├── BattleStateManager (entity tracking)
    ├── BattleUIController (UI display)
    ├── BattleActionHandler (action execution)
    ├── TurnManager (turn order)
    ├── NPCBehaviorManager (AI decisions)
    └── BattleConfigurationLoader (JSON loading)
```

---

## Initialization Flow

### `_Ready()` Sequence

```csharp
public override void _Ready()
{
    CallDeferred(nameof(DeferredInitialize));
}
```

**Why deferred?** Ensures scene tree is fully ready.

### `DeferredInitialize()` Steps

```
1. LoadBattleMap()
   ├─ Get map path from SceneManager
   ├─ Load PackedScene
   ├─ Instantiate as HexGrid
   ├─ Add to scene
   └─ Reparent HexControls to HexGrid

2. RediscoverControls()
   └─ Tell CentralInputManager to find controls

3. InitializeComponents()
   ├─ Create manager instances
   ├─ SetupComponentDependencies()
   └─ InitializeSceneManager()

4. InitializeInventoryControls()
   └─ Validate inventory UI exports

5. SetupBattle()
   ├─ Load configuration
   ├─ Setup entities
   ├─ Setup UI
   └─ Start battle
```

---

## Component Initialization

### Manager Creation

```csharp
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
```

### Dependency Wiring

```csharp
private void SetupComponentDependencies()
{
    // Order matters! Components with no dependencies first
    
    actionHandler.Initialize(stateManager, configLoader);
    turnManager.Initialize(stateManager, uiController);
    npcBehaviorManager.Initialize(stateManager, configLoader, actionHandler);
    turnManager.SetNPCBehaviorManager(npcBehaviorManager);
    turnManager.OnBattleEnded += OnBattleEnded;
    
    // These need HexGrid, so initialize after map loads
    stateManager.Initialize(this);
    uiController.Initialize(this);
}
```

**Dependency Chain**:
1. actionHandler (needs: stateManager, configLoader)
2. turnManager (needs: stateManager, uiController)
3. npcBehaviorManager (needs: all above)
4. stateManager (needs: HexGrid via this)
5. uiController (needs: HexGrid via this)

---

## Map Loading

### Dynamic Map System

```csharp
private void LoadBattleMap()
{
    // Get SceneManager
    sceneManager = GetNodeOrNull<SceneManager>("/root/SceneManager");
    
    // Get map path from parameters
    string mapPath = sceneManager.GetSceneParameter("map").AsString();
    
    // Load and instantiate
    var mapScene = GD.Load<PackedScene>(mapPath);
    var mapInstance = mapScene.Instantiate<HexGrid>();
    
    // Add to scene
    mapInstance.Name = "HexGrid";
    GetParent().AddChild(mapInstance);
    GetParent().MoveChild(mapInstance, 0); // First in tree
    
    // Reparent HexControls
    var hexControls = GetParent().GetNodeOrNull<HexControls>("HexControls");
    if (hexControls != null)
    {
        hexControls.GetParent().RemoveChild(hexControls);
        mapInstance.AddChild(hexControls);
        hexControls.FinalizeSetup();
    }
}
```

**Why reparent HexControls?**
- HexControls needs to be child of HexGrid
- Can't be in map scene (different instances)
- Solution: Move it after load

---

## Event Handling

### UI Event Callbacks

BattleManager provides public methods for UI events:

```csharp
// Main menu button pressed
public void OnActionRequested(string actionType, string actionName)
{
    if (actionType == "item")
    {
        // Special case: open inventory UI
        uiController.HideMainMenu();
        inventoryControls.Open(InventoryManager.ItemContext.Battle);
    }
    else
    {
        // Normal: process with action handler
        actionHandler.ProcessActionRequest(actionType, actionName);
        
        var availableActions = GetAvailableActionsForType(actionType);
        if (availableActions.Length > 0)
        {
            uiController.ShowSubmenu(availableActions);
        }
    }
}

// Submenu action selected
public void OnSubmenuSelection(string actionName)
{
    // Check if inventory item
    if (InventoryManager.Instance != null)
    {
        var item = InventoryManager.Instance.GetItem(actionName);
        if (item != null)
        {
            var itemConfig = ConvertItemToActionConfig(item);
            actionHandler.SetCurrentActionConfig(itemConfig);
            
            var validTargets = actionHandler.GetValidTargetsForCurrentAction();
            uiController.StartTargetSelection(validTargets, itemConfig);
            return;
        }
    }
    
    // Normal action handling
    actionHandler.ProcessSubmenuSelection(actionName);
    
    var actionConfig = actionHandler.GetCurrentActionConfig();
    if (actionConfig != null)
    {
        var validTargets = actionHandler.GetValidTargetsForCurrentAction();
        uiController.StartTargetSelection(validTargets, actionConfig);
    }
}

// Target cell selected
public void OnTargetSelected(Vector2I targetCell)
{
    actionHandler.ProcessTargetSelection(targetCell);
    uiController.EndTargetSelection();
    turnManager.EndPlayerTurn();
}

// Action cancelled
public void OnActionCancelled()
{
    actionHandler.CancelCurrentAction();
    uiController.EndTargetSelection();
    turnManager.ReturnToActionSelection();
}
```

### Event Flow Pattern

```
UI Event
    ↓
BattleManager (routing)
    ↓
Appropriate Manager (processing)
    ↓
Back to BattleManager (if needed)
    ↓
Update UI
```

---

## Battle Lifecycle

### Setup Phase

```csharp
private void SetupBattle()
{
    if (configLoader.LoadConfiguration(configFilePath))
    {
        stateManager.SetupBattleFromConfig(configLoader.GetBattleConfig());
        uiController.SetupUI();
        turnManager.StartBattle();
    }
}
```

**Steps**:
1. Load battle_config.json
2. Create entities from config
3. Setup UI
4. Start turn loop

### Active Phase

```
TurnManager drives the loop:
    1. Calculate turn order
    2. Process each entity's turn
    3. Check for battle end
    4. Repeat
```

BattleManager is mostly passive during this phase.

### End Phase

```csharp
private void OnBattleEnded(bool playerWon)
{
    // Calculate results
    var results = new Dictionary<string, Variant>
    {
        ["victory"] = playerWon,
        ["player_hp"] = stateManager.GetPlayer()?.CurrentHP ?? 0f,
        ["turns_taken"] = turnManager.GetCurrentRound(),
        ["enemies_defeated"] = GetEnemiesDefeatedCount(),
        ["p_earned"] = CalculatePEarned(playerWon)
    };
    
    // Store in SceneManager
    sceneManager.StoreBattleResults(results);
    sceneManager.AddP(results["p_earned"].AsInt32());
    
    // Transition to next scene
    sceneManager.LoadNextInSequence();
}
```

---

## Inventory Integration

### Item System Bridge

BattleManager bridges InventoryManager and action system:

```csharp
private ActionConfig ConvertItemToActionConfig(InventoryManager.InventoryItem item)
{
    return new ActionConfig
    {
        Id = item.Id,
        Name = item.Name,
        Description = item.Description,
        Damage = item.Damage,
        HealAmount = item.HealAmount,
        StatusEffect = item.StatusEffect,
        StatusDuration = item.StatusDuration,
        Range = item.Range > 0 ? item.Range : 2,
        TargetType = !string.IsNullOrEmpty(item.TargetType) ? item.TargetType : "Single",
        Cost = item.Cost,
        UsesRemaining = item.Quantity
    };
}
```

**Why convert?** Unifies item and skill handling through same action system.

### Inventory Flow

```
User presses "Item" button
    ↓
OnActionRequested("item", "")
    ↓
Open InventoryControls
    ↓
User selects item
    ↓
OnSubmenuSelection(itemName)
    ↓
Convert to ActionConfig
    ↓
Normal targeting flow
```

---

## Manager Access Methods

### Public Getters

```csharp
public BattleStateManager GetStateManager() => stateManager;
public BattleUIController GetUIController() => uiController;
public BattleActionHandler GetActionHandler() => actionHandler;
public BattleConfigurationLoader GetConfigLoader() => configLoader;
public TurnManager GetTurnManager() => turnManager;
```

**Purpose**: Allow subsystems to access each other when needed.

**Pattern**: Service Locator

---

## Helper Methods

### Action Availability

```csharp
private string[] GetAvailableActionsForType(string actionType)
{
    return actionType switch
    {
        "move" => configLoader.GetMoveOptionNames(),
        "skill" => configLoader.GetSkillNames(),
        "item" => GetInventoryItemNames(),
        "talk" => configLoader.GetTalkOptionNames(),
        _ => new string[0]
    };
}
```

### Inventory Items

```csharp
private string[] GetInventoryItemNames()
{
    if (InventoryManager.Instance == null)
        return new string[0];
    
    var items = InventoryManager.Instance.GetItemsByContext(
        InventoryManager.ItemContext.Battle
    );
    
    return items.Select(i => i.Name).ToArray();
}
```

### Battle Results

```csharp
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
```

---

## Scene Manager Integration

### Parameter Retrieval

```csharp
private void InitializeSceneManager()
{
    if (sceneManager != null)
    {
        // Get custom config path if specified
        if (sceneManager.HasSceneParameter("battle_config"))
        {
            string customConfig = sceneManager.GetSceneParameter("battle_config").AsString();
            if (!string.IsNullOrEmpty(customConfig))
            {
                configFilePath = customConfig;
            }
        }
        
        // Get P value
        int pValue = sceneManager.GetP();
    }
}
```

### Result Storage

```csharp
sceneManager.StoreBattleResults(results);
sceneManager.AddP(pEarned);
```

---

## Export Variables

```csharp
[Export] private string configFilePath = "res://data/battle_config.json";
[Export] private InventoryControls inventoryControls;
```

**configFilePath**: Default battle configuration
**inventoryControls**: Reference to inventory UI (assigned in editor)

---

## Debugging Methods

### UI Focus Management

```csharp
public void CallEnsureMenuFocus()
{
    uiController?.CallEnsureMenuFocus();
}

public void CallConnectToDynamicMenu()
{
    uiController?.CallConnectToDynamicMenu();
}
```

These are workarounds for UI timing issues, called deferred.

---

## Common Patterns

### Adding New Action Types

To add a new action type (e.g., "guard"):

1. **Add to GetAvailableActionsForType()**:
```csharp
case "guard": return configLoader.GetGuardOptionNames();
```

2. **Handle in OnActionRequested()**:
```csharp
actionHandler.ProcessActionRequest(actionType, actionName);
```

3. **Add execution in BattleActionHandler**

### Custom Battle Setup

```csharp
// In custom scene script
public override void _Ready()
{
    base._Ready();
    
    var battleManager = GetNode<BattleManager>("BattleManager");
    
    // Custom initialization after standard setup
    battleManager.GetStateManager().AddEntity(customEntity);
}
```

---

## Error Handling

### Map Loading Failures

```csharp
if (mapScene == null)
{
    GD.PrintErr($"[BattleManager] Failed to load map: {mapPath}");
    return;
}

if (mapInstance == null)
{
    GD.PrintErr($"[BattleManager] Map instance is not a HexGrid: {mapPath}");
    return;
}
```

### Configuration Loading Failures

```csharp
if (!configLoader.LoadConfiguration(configFilePath))
{
    GD.PrintErr("[BattleManager] Failed to load battle configuration");
    return;
}
```

---

## Performance Considerations

### Deferred Initialization
- Prevents blocking main thread
- Allows scene tree to stabilize
- Smoother startup

### Manager Reuse
- Managers created once per battle
- No per-action allocation
- Efficient for long battles

---

## Related Documentation

- [[System Architecture]] - Overall design
- [[Scene Hierarchy]] - Scene structure
- [[Turn System]] - Turn management details
- [[Component Reference#BattleManager]] - API reference