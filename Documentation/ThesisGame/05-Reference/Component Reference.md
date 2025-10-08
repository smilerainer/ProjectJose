Complete reference of all C# classes in the battle system.

---

## Core Battle Components

### BattleManager.cs
**Role**: Central coordinator for the battle system

**Responsibilities**:
- Initializes all subsystems
- Loads battle map dynamically
- Loads configuration files
- Handles UI event callbacks
- Manages battle lifecycle
- Stores battle results

**Key Methods**:
- `OnActionRequested(actionType, actionName)` - User pressed main menu button
- `OnSubmenuSelection(actionName)` - User selected action from submenu
- `OnTargetSelected(targetCell)` - User confirmed target
- `OnActionCancelled()` - User cancelled action
- `OnBattleEnded(playerWon)` - Battle concluded

**Dependencies**: All other managers

**Scene**: Attached to BattleManager node in BaseBattle scene

---

### BattleStateManager.cs
**Role**: Entity tracking and battlefield state

**Responsibilities**:
- Manages all entities (players, enemies, NPCs)
- Tracks entity positions
- Applies combat effects (damage, healing, status)
- Checks battle end conditions
- Provides entity queries

**Key Methods**:
- `AddEntity(entity)` - Add entity to battle
- `MoveEntity(entity, position)` - Move entity on grid
- `ApplyDamageToEntity(position, damage)` - Deal damage
- `ApplyHealingToEntity(position, healing)` - Heal entity
- `GetEntityAt(position)` - Query entity at cell
- `CheckBattleEndConditions()` - Victory/defeat check

**Key Properties**:
- `GetPlayer()` - Returns player entity
- `GetAllEntities()` - Returns all entities
- `GetAliveEntities()` - Returns only living entities

**Dependencies**: HexGrid, Entity

---

### BattleActionHandler.cs
**Role**: Action targeting and execution

**Responsibilities**:
- Calculates valid target cells
- Calculates AOE affected cells
- Validates targets against filters
- Executes actions on entities
- Supports both player and NPC actions

**Key Methods**:
- `ProcessActionRequest(actionType, actionName)` - Begin action
- `ProcessSubmenuSelection(actionName)` - Load action config
- `ProcessTargetSelection(targetCell)` - Execute action
- `CalculateValidTargets(config)` - Get targetable cells
- `CalculateAffectedCells(target, config)` - Get AOE cells
- `CalculateValidTargetsFromPosition(position, config)` - For NPCs

**Hex Math Features**:
- Cube coordinate conversion
- Hex distance calculation
- Line-of-sight checking
- Line drawing for line-type AOE
- Radius pattern generation

**Dependencies**: BattleStateManager, BattleConfigurationLoader

---

### TurnManager.cs
**Role**: Turn order and battle flow

**Responsibilities**:
- Calculates initiative-based turn order
- Manages round progression
- Tracks current actor
- Processes turn-end effects
- Detects battle end

**Key Methods**:
- `StartBattle()` - Begin battle loop
- `EndPlayerTurn()` - Player finishes turn
- `GetCurrentActor()` - Who's acting now
- `IsPlayerTurn()` - Check if player's turn

**Turn Order Algorithm**:
```csharp
OrderByDescending(initiative)
  .ThenByDescending(speed)
```

**Events**:
- `OnBattleEnded` - Fired when battle concludes

**Dependencies**: BattleStateManager, BattleUIController, NPCBehaviorManager

---

### BattleUIController.cs
**Role**: UI visibility and coordination

**Responsibilities**:
- Shows/hides menus
- Manages target selection mode
- Creates dynamic submenus
- Coordinates with input manager

**Key Methods**:
- `ShowMainMenu()` - Display action menu
- `HideMainMenu()` - Hide action menu
- `ShowSubmenu(options)` - Dynamic submenu
- `StartTargetSelection(cells, config)` - Enter targeting
- `EndTargetSelection()` - Exit targeting

**Dependencies**: MenuControls, HexControls, HexGrid, CentralInputManager

---

### BattleConfigurationLoader.cs
**Role**: JSON configuration loading

**Responsibilities**:
- Loads battle_config.json
- Builds action lookup dictionaries
- Provides filtered action lists
- Validates configuration

**Key Methods**:
- `LoadConfiguration(filePath)` - Load JSON
- `GetActionConfig(actionName)` - Get action by name
- `GetSkillNames()` - All skill names
- `GetItemNames()` - All item names
- `GetSkillsForEntity(type)` - Filtered skills
- `GetMoveOptionsForEntity(type)` - Filtered moves

**Dependencies**: CustomJsonLoader

---

### NPCBehaviorManager.cs
**Role**: AI decision-making coordinator

**Responsibilities**:
- Manages AI behavior types
- Routes decisions to correct behavior
- Executes NPC decisions

**Key Methods**:
- `RegisterBehavior(behavior)` - Add new AI type
- `GetDecisionForEntity(entity)` - Get NPC decision
- `ExecuteDecision(entity, decision)` - Perform action

**Registered Behaviors**:
- AggressiveBehavior
- DefensiveBehavior
- SupportBehavior
- BalancedBehavior
- CowardlyBehavior

**Dependencies**: All other managers

---

## AI Behavior Components

### INPCBehavior (Interface)
**Purpose**: Contract for AI behaviors

**Required Method**:
```csharp
NPCDecision DecideAction(
    Entity actor,
    BattleStateManager stateManager,
    BattleConfigurationLoader configLoader,
    BattleActionHandler actionHandler
)
```

---

### AggressiveBehavior.cs
**Strategy**: Always attack nearest enemy with highest damage skill

**Decision Tree**:
1. Find skills with damage > 0
2. Get valid targets
3. Pick nearest enemy
4. Return attack decision

---

### DefensiveBehavior.cs
**Strategy**: Heal if low → Retreat if threatened → Attack if safe

**Decision Tree**:
1. If HP < 30%, try to heal self
2. If enemies within 2 cells, retreat
3. Otherwise attack with long-range skills

---

### SupportBehavior.cs
**Strategy**: Heal wounded allies → Buff allies → Attack

**Decision Tree**:
1. Find most wounded ally (< 80% HP)
2. Heal them if possible
3. If no wounded, apply buffs
4. If no support needed, attack

---

### BalancedBehavior.cs
**Strategy**: Adaptive based on situation

**Decision Tree**:
1. If HP < 40%, heal/retreat
2. If good attack opportunity (priority ≥ 8), attack
3. Try to reposition to optimal range (range 2)
4. Attack even if not ideal

**Most Complex**: Uses priority scoring for decisions

---

### CowardlyBehavior.cs
**Strategy**: Always run away from enemies

**Decision Tree**:
1. Find nearest enemy
2. Move as far as possible from them
3. Skip turn if can't move

---

## Data Model Components

### Entity.cs
**Role**: Core data model for all combatants

**Properties**:
```csharp
// Identity
string Id
string Name
EntityType Type  // Player, Ally, Enemy, NPC, Neutral
Vector2I Position

// Combat Stats
float CurrentHP
float MaxHP
int Initiative
int Speed

// AI Config
string BehaviorType
NPCBehaviorConfig BehaviorConfig

// Available Actions
List<string> AvailableSkills
List<string> AvailableItems
List<string> AvailableMoveOptions
List<string> AvailableTalkOptions

// State
List<StatusEffect> ActiveStatuses
bool HasActedThisTurn
```

**Key Methods**:
- `TakeDamage(amount)` - Reduce HP
- `Heal(amount)` - Restore HP
- `AddStatus(status)` - Apply status effect
- `ProcessTurnEnd()` - Handle turn-end effects
- `IsEnemyOf(other)` - Check relationship
- `IsAllyOf(other)` - Check relationship

**Static Factory**:
- `FromDefinition(EntityDefinition)` - Create from JSON

---

### ActionConfig.cs
**Role**: Defines a single action (skill/item/move/talk)

**Properties**:
```csharp
// Basic
string Id
string Name
string Description
int Cost
string TargetType  // Self, Single, Area, Enemy, Ally, Movement

// Effects
int Damage
int HealAmount
string StatusEffect
int StatusDuration

// Targeting
int Range
bool UseRadiusRange
List<PatternCell> RangePattern
List<PatternCell> AoePattern
int AoeRadius

// AOE Types
string AoeType  // "", "radius", "line"
int AoeWidth
int AoeOvershoot

// Filters
bool ExcludeSelf
bool ExcludeOccupied
bool TargetEmptyCellsOnly
bool TargetSelfOnly
bool ExcludeOrigin
List<string> ExcludeTypes

// Advanced
bool InverseAOE
bool RequiresLineOfSight
bool AllTilesValid
List<TargetPattern> Whitelist
List<TargetPattern> Blacklist
```

---

### StatusEffect.cs
**Role**: Buffs, debuffs, and conditions

**Properties**:
```csharp
string Name
int RemainingDuration
float DamagePerTurn
float HealPerTurn
```

**Built-in Types**: Poison, Regen, Stun (via `CanAct` check)

---

## Hex Grid Components

### HexGrid.cs
**Role**: Pure hex mathematics engine

**Coordinate System**: Offset coordinates (even-q)

**Responsibilities**:
- Coordinate conversion (Offset ↔ Cube)
- Distance calculation
- Neighbor finding
- Pathfinding
- TileMap layer management
- Display markers (cursors, highlights)

**Key Methods**:
```csharp
// Conversion
Vector2 CellToWorld(Vector2I cell)
Vector2I WorldToCell(Vector2 worldPos)

// Hex Math
List<Vector2I> GetHexNeighbors(Vector2I cell)
int GetHexDistance(Vector2I a, Vector2I b)
List<Vector2I> GetCellsInRange(Vector2I origin, int range)

// State Queries
bool IsValidCell(Vector2I cell)
bool IsWalkableCell(Vector2I cell)
bool IsOccupiedCell(Vector2I cell)

// Display
void SetCursor(Vector2I cell, CursorType type, CellLayer layer)
void ShowRangeHighlight(List<Vector2I> cells)
void ShowAoePreview(Vector2I target, List<Vector2I> pattern)
void ClearAllHighlights()
```

**TileMap Layers**:
- Terrain (0)
- WorldMarker (10)
- Obstacle (20)
- Entity (30)
- Marker (40)
- Cursor (50)

**Cursor Types**:
- Valid (green)
- Invalid (red)
- AOE (yellow)
- Range (blue)

---

### HexControls.cs
**Role**: UI interface for hex interaction

**Responsibilities**:
- Cursor movement
- Target selection
- Camera following
- User input handling
- Uses HexGrid for all math

**Key Methods**:
```csharp
void EnterInteractionMode()
void ExitInteractionMode(Vector2I focus)
void SetValidCells(HashSet<Vector2I> cells)
void SetActionConfig(ActionConfig config, BattleActionHandler handler)
```

**Signals**:
- `CellActivated(Vector2I)` - User confirmed target
- `InteractionCancelled()` - User cancelled
- `CursorMoved(Vector2I)` - Cursor position changed

**Dependencies**: HexGrid (parent node)

---

## UI Components

### CentralInputManager.cs
**Role**: Context-aware input routing

**Input Contexts**:
- `None` - No active UI
- `Menu` - MenuControls active
- `HexGrid` - HexControls active
- `Dialogue` - Dialogic timeline active
- `Mixed` - Multiple active (prioritize HexGrid)

**Responsibilities**:
- Detect active UI context
- Route input to correct control
- Manage virtual cursor
- Handle context switching
- Integrate with Dialogic

**Key Methods**:
```csharp
void RegisterControl(MenuControls menu)
void RegisterControl(HexControls hex)
void SetMenuButtonArray(string[] options)
void ClearDynamicMenu()
void NotifyButtonFocusChanged()
```

**Cursor Features**:
- Smooth movement or instant snap
- Follows focused button/hex cell
- Hidden during Dialogic/HexGrid

---

### MenuControls.cs
**Role**: Generic menu navigation system

**Responsibilities**:
- Button discovery
- Linear navigation
- Focus management
- Dynamic button creation

**Key Methods**:
```csharp
void SetActive(bool active)
void Navigate(Vector2I direction)
void ActivateCurrentButton()
void AddButton(string text, string name)
void ClearAllButtons()
void SetButtonsFromArray(string[] options)
void ResetToFirstButton()
```

**Signals**:
- `ButtonSelected(int, BaseButton)` - Focus changed
- `ButtonActivated(int, BaseButton)` - Button pressed
- `MenuActivated()` - Menu shown
- `MenuDeactivated()` - Menu hidden

---

### InventoryControls.cs
**Role**: Inventory screen controller

**Status**: 🚧 Partially implemented

**Responsibilities**:
- Display inventory items
- Handle item selection
- Show item details
- Execute item actions (use/give/discard)

**Key Methods**:
```csharp
void Open(ItemContext context)
void Close()
```

**Dependencies**: InventoryManager, MenuControls

---

### InventoryManager.cs
**Role**: Global inventory singleton

**Status**: 🚧 Framework exists, needs battle integration testing

**Responsibilities**:
- Item storage
- Item usage
- Context filtering
- Persistence (TODO)

**Key Methods**:
```csharp
bool AddItem(string itemId, int quantity)
bool RemoveItem(string itemId, int quantity)
bool HasItem(string itemId, int quantity)
bool UseItem(string itemId, ItemContext context, object target)
List<InventoryItem> GetItemsByContext(ItemContext context)
```

**Item Contexts**:
- Battle
- Dialogue
- Menu
- Overworld

**Signals**:
- `InventoryChanged()`
- `ItemUsed(string, bool)`
- `ItemAdded(string, int)`
- `ItemRemoved(string, int)`

---

### DialogueControls.cs
**Role**: Dialogue display controller

**Responsibilities**:
- Disable Dialogic's default text panel
- Position portraits correctly
- Manage dialogue container visibility

**Dependencies**: Dialogic autoload

**Scene**: Part of both BaseBattle and BaseVN

---

## Integration Components

### SceneManager.cs
**Role**: Story sequence progression

**Responsibilities**:
- Load sequence from JSON
- Manage VN ↔ Battle transitions
- Track P (progression) variable
- Store battle results
- Pass parameters between scenes

**Key Methods**:
```csharp
void LoadNextInSequence()
void JumpToSequence(string id)
int GetP()
void AddP(int amount)
void StoreBattleResults(Dictionary results)
Variant GetSceneParameter(string key)
```

**Integrations**:
- Dialogic (timeline start/end)
- BattleManager (parameters, results)
- P variable sync

---

### CustomJsonLoader.cs
**Role**: Generic JSON serialization

**Responsibilities**:
- Load/save JSON files
- Serialize/deserialize C# objects
- Provide type-safe loading

**Key Methods**:
```csharp
T LoadFromFile<T>(string path)
void SaveToFile<T>(T data, string path)
BattleConfigData LoadBattleConfig(string path)
```

**Data Structures**:
- BattleConfigData
- GameSaveData
- Entity Definition structures

---

## Configuration Data Classes

### BattleConfigData
**Purpose**: Root configuration object

```csharp
List<ActionConfig> Skills
List<ActionConfig> Items
List<ActionConfig> TalkOptions
List<ActionConfig> MoveOptions
List<EntityDefinition> Entities
GameSettings Settings
```

---

### EntityDefinition
**Purpose**: JSON entity template

```csharp
string Id
string Name
string EntityType
float MaxHP
int Initiative
int Speed
Vector2IData StartPosition
List<string> AvailableSkills
List<string> AvailableItems
List<string> AvailableMoveOptions
List<string> AvailableTalkOptions
NPCBehaviorConfig BehaviorConfig
```

---

### NPCBehaviorConfig
**Purpose**: AI tuning parameters

```csharp
string BehaviorType
int AggressionLevel  // 0-10
int CautiousnessLevel  // 0-10
float HealthThreshold  // When to panic
int AttackPriority  // Weight for attack actions
int DefendPriority  // Weight for defend actions
int SupportPriority  // Weight for support actions
int MovePriority  // Weight for movement
List<string> PreferredTargets
bool AvoidFriendlyFire
bool PreferGroupedTargets
List<string> PreferredSkills
List<string> EmergencySkills
int MinRangePreference
int MaxRangePreference
```

---

### GameSettings
**Purpose**: Global battle settings

```csharp
int StartingMoney
int MaxPartySize
int DefaultMoveRange
bool EnableFriendlyFire
bool EnableCriticalHits
bool EnableStatusEffects
Dictionary<string, object> CustomSettings
```

---

## Helper Data Structures

### NPCDecision
**Purpose**: AI decision result

```csharp
string ActionType  // "skill", "move", "item", "talk", "skip"
string ActionName  // Specific action name
Vector2I TargetCell  // Where to target
bool IsValid  // Can execute?
int Priority  // For comparing options (higher = better)
```

**Static Helpers**:
- `NPCDecision.Invalid()` - No valid action
- `NPCDecision.Skip()` - Skip turn

---

### PatternCell
**Purpose**: Hex offset in patterns

```csharp
int X
int Y
Vector2I ToVector2I()
```

Used in:
- RangePattern
- AoePattern

---

### TargetPattern
**Purpose**: Complex targeting filters

```csharp
string Type  // "coordinate" or "radius"
int X, Y  // For coordinate type
PatternCell Center  // For radius type
int Radius  // For radius type
```

Used in:
- Whitelist (additional valid cells)
- Blacklist (excluded cells)

---

## Related Documentation

- [[System Architecture]] - How components work together
- [[Data Flow]] - Event sequences
- [[Signal Reference]] - All events/signals
- [[Action System]] - Detailed action mechanics
- [[Configuration Files]] - JSON structure reference