## Overview

The battle system follows a **Manager-based architecture** where specialized managers handle distinct responsibilities. This promotes maintainability and testability.

## Core Architecture Pattern

```
┌─────────────────────────────────────────┐
│         CentralInputManager             │
│     (Context-aware input routing)       │
└──────────────────┬──────────────────────┘
                   ↓
┌──────────────────────────────────────────┐
│           BattleManager                  │
│      (Central coordinator)               │
│                                          │
│  ┌────────────────────────────────────┐  │
│  │    Component Managers:             │  │
│  │                                    │  │
│  │  • BattleStateManager              │  │
│  │  • BattleUIController              │  │
│  │  • BattleActionHandler             │  │
│  │  • TurnManager                     │  │
│  │  • NPCBehaviorManager              │  │
│  │  • BattleConfigurationLoader       │  │
│  └────────────────────────────────────┘  │
└──────────────┬───────────────────────────┘
               ↓
┌──────────────────────────────────────────┐
│            HexGrid                       │
│       (Pure math engine)                 │
│                                          │
│  • Coordinate conversion                 │
│  • Distance calculation                  │
│  • Pathfinding                           │
│  • Display layer management              │
└──────────────────────────────────────────┘
```

## Layer Breakdown

### Layer 1: Input Layer
**CentralInputManager**
- Routes input to appropriate UI context (Menu, HexGrid, Dialogue, None)
- Manages cursor display and movement
- Handles context switching
- Single source of truth for active control

### Layer 2: Coordination Layer
**BattleManager**
- Initializes all subsystems
- Loads battle configuration
- Coordinates between managers
- Handles scene lifecycle
- Provides public API for UI events

### Layer 3: Management Layer

#### BattleStateManager
- Tracks all entities and positions
- Manages entity lifecycle
- Applies damage/healing/status effects
- Checks win/loss conditions
- Provides entity queries

#### BattleUIController
- Shows/hides UI elements
- Manages menu visibility
- Controls target selection mode
- Handles dynamic submenu creation
- Coordinates with input manager

#### BattleActionHandler
- Processes action requests
- Calculates valid targets
- Calculates affected cells (AOE)
- Executes actions on entities
- Handles targeting logic and filters

#### TurnManager
- Calculates turn order by initiative
- Manages round progression
- Tracks current actor
- Processes turn-end effects
- Detects battle end conditions

#### NPCBehaviorManager
- Selects appropriate AI behavior
- Generates action decisions
- Executes NPC actions
- Manages 5 behavior types

#### BattleConfigurationLoader
- Loads JSON configuration
- Builds action lookup tables
- Provides filtered action lists
- Validates configuration

### Layer 4: Math Engine Layer
**HexGrid**
- Pure coordinate mathematics
- Cube ↔ Offset conversion
- Hex neighbor calculation
- Distance/pathfinding algorithms
- TileMap layer management
- No game logic, only math

**HexControls** (UI Interface)
- Cursor movement
- Target selection
- Camera following
- User interaction
- Uses HexGrid for all math

## Key Design Decisions

### 1. Separation of Math and Logic
**HexGrid** is a pure math engine:
- ✅ Coordinate conversion
- ✅ Distance calculation
- ✅ Neighbor finding
- ✅ Display layer management
- ❌ Entity tracking (that's BattleStateManager)
- ❌ Action execution (that's BattleActionHandler)
- ❌ Turn logic (that's TurnManager)

**Benefit**: HexGrid can be reused for any hex-based system.

### 2. Manager Initialization Order
```csharp
// Order matters!
1. configLoader (no dependencies)
2. stateManager (needs HexGrid)
3. actionHandler (needs stateManager + configLoader)
4. turnManager (needs stateManager + uiController)
5. npcBehaviorManager (needs all above)
6. uiController (needs HexGrid)
```

### 3. Signal-Based Communication
Components communicate via C# events/signals:
- `TurnManager.OnBattleEnded` → BattleManager
- `HexControls.CellActivated` → BattleManager
- `MenuControls.ButtonActivated` → BattleManager
- `InventoryManager.InventoryChanged` → InventoryControls

**Benefit**: Loose coupling, easy to test, clear data flow.

### 4. Context-Aware Input Routing
CentralInputManager detects active context:
```csharp
InputContext.None → No active UI
InputContext.Menu → MenuControls active
InputContext.HexGrid → HexControls active  
InputContext.Dialogue → Dialogic timeline active
InputContext.Mixed → Multiple active (prioritize HexGrid)
```

Routes input accordingly, preventing conflicts.

## Data Flow Examples

### Example 1: Player Uses Skill

```
1. User presses "Skill" button
   ↓
2. CentralInputManager routes to MenuControls
   ↓
3. MenuControls.ButtonActivated signal
   ↓
4. BattleManager.OnActionRequested("skill", "")
   ↓
5. BattleActionHandler.ProcessActionRequest()
   ↓
6. BattleUIController.ShowSubmenu(skillNames)
   ↓
7. User selects "Direct Hit"
   ↓
8. BattleManager.OnSubmenuSelection("Direct Hit")
   ↓
9. BattleActionHandler.ProcessSubmenuSelection()
   ↓
10. BattleActionHandler.CalculateValidTargets()
    ↓
11. BattleUIController.StartTargetSelection()
    ↓
12. HexControls enters interaction mode
    ↓
13. User moves cursor and confirms
    ↓
14. HexControls.CellActivated signal
    ↓
15. BattleManager.OnTargetSelected(cell)
    ↓
16. BattleActionHandler.ProcessTargetSelection()
    ↓
17. BattleActionHandler.ExecuteSkillAction()
    ↓
18. BattleStateManager.ApplyDamageToEntity()
    ↓
19. TurnManager.EndPlayerTurn()
```

### Example 2: NPC Turn

```
1. TurnManager.ProcessNextTurn()
   ↓
2. TurnManager.StartNPCTurn(entity)
   ↓
3. NPCBehaviorManager.GetDecisionForEntity()
   ↓
4. AggressiveBehavior.DecideAction()
   ↓
5. BattleActionHandler.CalculateValidTargetsFromPosition()
   ↓
6. NPCDecision returned (actionType, target)
   ↓
7. NPCBehaviorManager.ExecuteDecision()
   ↓
8. BattleStateManager.ApplyDamageToEntity()
   ↓
9. TurnManager.EndCurrentTurn()
```

## Configuration Flow

```
battle_config.json
    ↓
BattleConfigurationLoader.LoadConfiguration()
    ↓
CustomJsonLoader.LoadBattleConfig()
    ↓
BattleConfigData (C# object)
    ↓
BattleStateManager.SetupBattleFromConfig()
    ↓
Entities added to grid
    ↓
TurnManager.StartBattle()
```

## Scene Lifecycle

```
1. SceneManager loads BaseBattle.tscn
2. BattleManager._Ready()
3. Load map from SceneManager parameters
4. Initialize all managers
5. Load battle_config.json
6. Setup entities on grid
7. TurnManager.StartBattle()
8. Enter turn loop
9. Battle ends (victory/defeat)
10. TurnManager.OnBattleEnded fires
11. BattleManager stores results
12. SceneManager.LoadNextInSequence()
```

## Extension Points

### Adding New Actions
1. Add to `battle_config.json`
2. BattleConfigurationLoader automatically loads it
3. BattleActionHandler.ExecuteAction() handles it
4. No code changes needed (data-driven!)

### Adding New AI Behaviors
1. Create class implementing `INPCBehavior`
2. Implement `DecideAction()` method
3. Register in NPCBehaviorManager
4. Set entity's `behaviorType` in JSON

### Adding New UI Screens
1. Create new Control/CanvasLayer scene
2. Create controller script
3. Register with CentralInputManager (if needed)
4. Connect signals to BattleManager

See [[Common Patterns]] for code templates.

## Related Documentation
- [[Data Flow]] - Detailed sequence diagrams
- [[Scene Hierarchy]] - Scene tree structure
- [[Component Reference]] - All classes explained