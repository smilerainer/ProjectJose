
Initiative-based turn management and battle flow control.

---

## Overview

**TurnManager.cs** controls the flow of battle using an initiative-based turn order system. It determines who acts when, processes turns, and detects battle end conditions.

**Turn Philosophy**: "Fair but dynamic" - turn order based on stats, not predetermined.

---

## Core Concepts

### Initiative System

**Turn Order Formula**:
```csharp
entities
    .OrderByDescending(e => e.Initiative)  // Primary sort
    .ThenByDescending(e => e.Speed)        // Tiebreaker
```

**Example**:

| Entity | Initiative | Speed | Turn Position |
| ------ | ---------- | ----- | ------------- |
| Rogue  | 80         | 7     | 1st           |
| Mage   | 70         | 5     | 2nd           |
| Knight | 50         | 6     | 3rd           |
| Goblin | 50         | 4     | 4th           |

### Round vs Turn

**Round**: Complete cycle where all entities act once
**Turn**: Single entity's action phase

```
Round 1:
  Turn 1: Rogue acts
  Turn 2: Mage acts
  Turn 3: Knight acts
  Turn 4: Goblin acts
  → End Round 1

Round 2:
  (Recalculate turn order)
  Turn 1: ...
```

---

## Turn Order Calculation

### `CalculateTurnOrder()`

```csharp
private List<Entity> CalculateTurnOrder()
{
    return stateManager.GetAliveEntities()
        .Where(e => e.CanAct)           // Skip stunned/dead
        .OrderByDescending(e => e.Initiative)
        .ThenByDescending(e => e.Speed)
        .ToList();
}
```

**Eligibility Requirements**:
- `IsAlive` - HP > 0
- `CanAct` - Not stunned or incapacitated

**Recalculated Every Round**: Turn order can change based on:
- Entity deaths
- Status effects wearing off
- New entities joining

---

## Battle Flow

### Complete Battle Lifecycle

```
StartBattle()
    ↓
StartNextRound()
    ↓
    ├─ Increment round counter
    ├─ Calculate turn order
    └─ ProcessNextTurn()
        ↓
        ├─ Get current actor
        ├─ Check if alive/can act
        ├─ StartPlayerTurn() OR StartNPCTurn()
        │   ↓
        │   [Entity performs action]
        │   ↓
        ├─ EndCurrentTurn()
        │   ↓
        │   ├─ Mark entity as acted
        │   ├─ CheckBattleEndConditions()
        │   └─ currentTurnIndex++
        │
        └─ IF more turns: ProcessNextTurn()
            ELSE: EndRound()
                ↓
                ├─ ProcessTurnEndEffects()
                ├─ CheckBattleEndConditions()
                └─ StartNextRound()
```

---

## Turn Processing

### Player Turn

```csharp
private void StartPlayerTurn()
{
    stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.ActionSelection);
    uiController.ShowMainMenu();
    
    GD.Print($"[TurnManager] Player turn {currentRound} started");
}
```

**Player Turn Flow**:
1. Set phase to ActionSelection
2. Show main menu (Move/Skill/Item/Talk)
3. Wait for player input (async)
4. Player selects action → submenu → target
5. Action executes
6. `EndPlayerTurn()` called by BattleManager

### NPC Turn

```csharp
private void StartNPCTurn(Entity entity)
{
    stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.EnemyTurn);
    
    GD.Print($"[TurnManager] {entity.Name} ({entity.Type}) turn started");
    
    if (npcBehaviorManager != null)
    {
        var decision = npcBehaviorManager.GetDecisionForEntity(entity);
        npcBehaviorManager.ExecuteDecision(entity, decision);
    }
    
    EndCurrentTurn();
}
```

**NPC Turn Flow**:
1. Set phase to EnemyTurn
2. Get AI decision from NPCBehaviorManager
3. Execute decision immediately (synchronous)
4. End turn automatically

**Key Difference**: NPC turns are instant, player turns wait for input.

---

## Turn End Processing

### `EndPlayerTurn()`

```csharp
public void EndPlayerTurn()
{
    stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.TurnEnd);
    uiController.HideMainMenu();
    
    GD.Print($"[TurnManager] Player turn ended");
    
    EndCurrentTurn();
}
```

Called by BattleManager after player action completes.

### `EndCurrentTurn()`

```csharp
private void EndCurrentTurn()
{
    // Mark entity as acted
    if (currentTurnIndex < turnOrder.Count)
    {
        var entity = turnOrder[currentTurnIndex];
        entity.HasActedThisTurn = true;
    }
    
    // Check for battle end
    if (CheckBattleEndConditions())
    {
        EndBattle();
        return;
    }
    
    // Move to next turn
    currentTurnIndex++;
    ProcessNextTurn();
}
```

**Steps**:
1. Flag entity as having acted
2. Check if battle should end
3. Increment turn index
4. Process next turn (or end round)

---

## Round Management

### `StartNextRound()`

```csharp
private void StartNextRound()
{
    currentRound++;
    currentTurnIndex = 0;
    
    // Recalculate turn order
    turnOrder = CalculateTurnOrder();
    
    GD.Print($"[TurnManager] === Round {currentRound} Started ===");
    GD.Print($"[TurnManager] Turn order ({turnOrder.Count} entities):");
    for (int i = 0; i < turnOrder.Count; i++)
    {
        GD.Print($"  {i + 1}. {turnOrder[i].Name} (Initiative: {turnOrder[i].Initiative})");
    }
    
    ProcessNextTurn();
}
```

**Every Round**:
- Turn order recalculated (entities may have died)
- Round counter incremented
- Turn index reset to 0
- Debug logging shows new order

### `EndRound()`

```csharp
private void EndRound()
{
    GD.Print($"[TurnManager] === Round {currentRound} Ended ===");
    
    // Process status effects, regeneration, etc.
    stateManager.ProcessTurnEndEffects();
    
    // Check if battle ended during effect processing
    if (CheckBattleEndConditions())
    {
        EndBattle();
        return;
    }
    
    // Start next round
    StartNextRound();
}
```

**Round End Effects**:
- Status effect durations decrement
- Poison/Regen damage/healing applied
- Expired statuses removed
- Battle end check (entities may have died from poison)

---

## Battle End Detection

### `CheckBattleEndConditions()`

```csharp
private bool CheckBattleEndConditions()
{
    var battleEnded = stateManager.CheckBattleEndConditions();
    
    if (battleEnded)
    {
        GD.Print("[TurnManager] Battle end conditions met");
    }
    
    return battleEnded;
}
```

Delegates to BattleStateManager:

```csharp
// In BattleStateManager
public bool CheckBattleEndConditions()
{
    var player = GetPlayer();
    if (player == null || !player.IsAlive)
        return true; // Player defeated = loss
    
    var aliveEnemies = allEntities.Any(e => e.Type == EntityType.Enemy && e.IsAlive);
    if (!aliveEnemies)
        return true; // No enemies = victory
    
    return false;
}
```

**Victory Conditions**:
- All enemies defeated
- Player alive

**Defeat Conditions**:
- Player HP reaches 0

---

## Battle End Flow

### `EndBattle()`

```csharp
private void EndBattle()
{
    battleActive = false;
    stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.BattleEnd);
    
    GD.Print("[TurnManager] Battle ended");
    
    DetermineBattleOutcome();
}
```

### `DetermineBattleOutcome()`

```csharp
private void DetermineBattleOutcome()
{
    var player = stateManager.GetPlayer();
    bool playerWon = player?.IsAlive == true;
    
    if (playerWon)
    {
        GD.Print("[TurnManager] === VICTORY ===");
    }
    else
    {
        GD.Print("[TurnManager] === DEFEAT ===");
    }
    
    // Notify BattleManager
    OnBattleEnded?.Invoke(playerWon);
}
```

**Event Signal**: `OnBattleEnded(bool playerWon)`
- Subscribed by BattleManager
- Triggers scene transition
- Stores battle results

---

## State Management

### Turn State Variables

```csharp
private bool battleActive = false;
private int currentRound = 0;
private List<Entity> turnOrder = new();
private int currentTurnIndex = 0;
```

| Variable | Purpose |
|----------|---------|
| `battleActive` | Is battle currently running? |
| `currentRound` | Which round (1, 2, 3...) |
| `turnOrder` | List of entities in initiative order |
| `currentTurnIndex` | Which entity's turn (0 to turnOrder.Count-1) |

### State Queries

```csharp
public bool IsBattleActive() => battleActive;
public int GetCurrentRound() => currentRound;
public Entity GetCurrentActor() => 
    currentTurnIndex < turnOrder.Count ? turnOrder[currentTurnIndex] : null;

public bool IsPlayerTurn()
{
    var currentActor = GetCurrentActor();
    return currentActor?.Type == EntityType.Player;
}

public bool IsEnemyTurn() =>
    stateManager.GetCurrentPhase() == BattleStateManager.BattlePhase.EnemyTurn;
```

---

## Phase System

### Battle Phases

```csharp
public enum BattlePhase
{
    Setup,           // Initial setup
    PlayerTurn,      // Player is acting
    EnemyTurn,       // NPC is acting
    ActionSelection, // Player choosing action
    TargetSelection, // Player choosing target
    ActionExecution, // Action being executed
    TurnEnd,         // Turn cleanup
    BattleEnd        // Battle over
}
```

**Phase Transitions**:
```
Setup → PlayerTurn → ActionSelection → TargetSelection 
    → ActionExecution → TurnEnd → (next turn or EnemyTurn)
    → TurnEnd → ... → BattleEnd
```

---

## Turn Cancellation

### `ReturnToActionSelection()`

```csharp
public void ReturnToActionSelection()
{
    stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.ActionSelection);
    uiController.EndTargetSelection();
    uiController.ShowMainMenu();
    
    GD.Print("[TurnManager] Returned to action selection");
}
```

**When Called**: User presses Escape during targeting

**Effect**: Return to main menu without ending turn

---

## NPC Behavior Integration

### Setting NPC Manager

```csharp
public void SetNPCBehaviorManager(NPCBehaviorManager manager)
{
    this.npcBehaviorManager = manager;
    GD.Print("[TurnManager] NPC behavior manager connected");
}
```

**Initialization Order**:
1. TurnManager created
2. NPCBehaviorManager created
3. `SetNPCBehaviorManager()` called
4. NPC turns can now process

### NPC Turn Execution

```csharp
if (npcBehaviorManager != null)
{
    var decision = npcBehaviorManager.GetDecisionForEntity(entity);
    npcBehaviorManager.ExecuteDecision(entity, decision);
}
else
{
    GD.PrintErr("[TurnManager] NPC behavior manager not initialized!");
}
```

**Fallback**: Logs error if NPCBehaviorManager missing

---

## Turn Order Examples

### Example 1: Standard Battle

```
Initial Setup:
  Player (Initiative: 50, Speed: 5)
  Goblin1 (Initiative: 45, Speed: 4)
  Goblin2 (Initiative: 45, Speed: 6)
  Boss (Initiative: 60, Speed: 7)

Round 1 Turn Order:
  1. Boss (60, 7)
  2. Player (50, 5)
  3. Goblin2 (45, 6) ← Speed tiebreaker
  4. Goblin1 (45, 4)
```

### Example 2: Mid-Battle Changes

```
Round 3 Start:
  Boss (60, 7)
  Player (50, 5)
  Goblin2 (45, 6)
  Goblin1 (45, 4)

Goblin1 dies during Round 3

Round 4 Turn Order (recalculated):
  1. Boss (60, 7)
  2. Player (50, 5)
  3. Goblin2 (45, 6)
  ← Goblin1 removed
```

### Example 3: Stun Effect

```
Round 5 Start:
  Boss has "Stunned" status

Boss.CanAct = false (has "stun" status)

Round 5 Turn Order:
  1. Player (50, 5)
  2. Goblin2 (45, 6)
  ← Boss skipped (cannot act)

Round 6 Start:
  Stun wears off, Boss back in order
```

---

## Status Effect Processing

### Turn End Effects

```csharp
// In Entity.ProcessTurnEnd()
foreach (var status in ActiveStatuses.Values)
{
    status.ProcessTurnEnd(this, state);  // Apply damage/healing
    status.Duration--;                     // Decrement duration
    
    if (status.Duration <= 0)
        statusesToRemove.Add(status.Id);   // Mark for removal
}

foreach (var statusId in statusesToRemove)
    RemoveStatus(statusId);

HasActedThisTurn = false;  // Reset for next round
```

**Example**: Poison Status
```csharp
public override void ProcessTurnEnd(Entity target, BattleState state)
{
    target.TakeDamage(Intensity);  // Deal poison damage
    state.Log($"{target.Name} takes {Intensity} poison damage");
}
```

---

## Performance Considerations

### Turn Order Caching

Turn order calculated once per round, not per turn:
```csharp
// ✅ Good: Calculate at round start
turnOrder = CalculateTurnOrder();

// ❌ Bad: Would be calculating every turn
// var currentActor = CalculateTurnOrder()[currentTurnIndex];
```

### Entity Filtering

Only alive, capable entities included:
```csharp
.Where(e => e.IsAlive && e.CanAct)
```

Prevents processing dead/stunned entities.

---

## Debug Methods

### Force End Battle

```csharp
public void ForceEndBattle()
{
    GD.Print("[TurnManager] Force ending battle (debug)");
    EndBattle();
}
```

### Skip to Next Round

```csharp
public void SkipToNextRound()
{
    GD.Print("[TurnManager] Skipping to next round (debug)");
    EndRound();
}
```

### Print Turn Order

```csharp
public void PrintTurnOrder()
{
    GD.Print("[TurnManager] Current turn order:");
    for (int i = 0; i < turnOrder.Count; i++)
    {
        var marker = i == currentTurnIndex ? ">>> " : "    ";
        GD.Print($"{marker}{i + 1}. {turnOrder[i].Name} (Initiative: {turnOrder[i].Initiative})");
    }
}
```

**Output Example**:
```
[TurnManager] Current turn order:
    1. Boss (Initiative: 60)
>>> 2. Player (Initiative: 50)  ← Currently acting
    3. Goblin (Initiative: 45)
```

---

## Common Patterns

### Adding Turn Start Effects

```csharp
private void StartPlayerTurn()
{
    // Standard setup
    stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.ActionSelection);
    uiController.ShowMainMenu();
    
    // Add custom turn start effect
    var player = stateManager.GetPlayer();
    if (player.HasStatus("regeneration"))
    {
        player.Heal(5);
        GD.Print("[TurnManager] Player regenerates 5 HP at turn start");
    }
}
```

### Custom Turn Order

```csharp
// Example: Always let player go first
private List<Entity> CalculateTurnOrder()
{
    var entities = stateManager.GetAliveEntities()
        .Where(e => e.CanAct)
        .OrderByDescending(e => e.Initiative)
        .ThenByDescending(e => e.Speed)
        .ToList();
    
    // Move player to front
    var player = entities.FirstOrDefault(e => e.Type == EntityType.Player);
    if (player != null)
    {
        entities.Remove(player);
        entities.Insert(0, player);
    }
    
    return entities;
}
```

---

## Integration Points

### With BattleManager

```csharp
// BattleManager calls
turnManager.Initialize(stateManager, uiController);
turnManager.SetNPCBehaviorManager(npcBehaviorManager);
turnManager.StartBattle();

// TurnManager calls back
OnBattleEnded?.Invoke(playerWon);
```

### With BattleStateManager

```csharp
// TurnManager queries
stateManager.GetAliveEntities()
stateManager.CheckBattleEndConditions()
stateManager.ProcessTurnEndEffects()
stateManager.SetCurrentPhase(phase)
```

### With BattleUIController

```csharp
// TurnManager updates UI
uiController.ShowMainMenu()
uiController.HideMainMenu()
uiController.EndTargetSelection()
```

### With NPCBehaviorManager

```csharp
// TurnManager delegates NPC turns
var decision = npcBehaviorManager.GetDecisionForEntity(entity);
npcBehaviorManager.ExecuteDecision(entity, decision);
```

---

## Troubleshooting

### Turn Order Not Updating

**Problem**: Same turn order every round despite changes

**Solution**: Check `CalculateTurnOrder()` is called at `StartNextRound()`

### NPC Not Acting

**Problem**: NPC skips turn

**Solution**: 
- Check `entity.CanAct` returns true
- Verify `npcBehaviorManager` is set
- Check entity has available actions

### Battle Won't End

**Problem**: Battle continues after all enemies dead

**Solution**: 
- Verify `CheckBattleEndConditions()` called after each turn
- Check `stateManager.CheckBattleEndConditions()` logic
- Ensure entity `IsAlive` property accurate

### Infinite Round Loop

**Problem**: Round never ends

**Solution**: 
- Check `currentTurnIndex++` in `EndCurrentTurn()`
- Verify `ProcessNextTurn()` calls `EndRound()` when index exceeds count

---

## Related Documentation

- [[Battle Manager]] - Initialization and coordination
- [[Entity System]] - Entity data and properties
- [[AI Behaviors]] - NPC decision-making
- [[Status Effects]] - Turn-end effect processing
- [[Component Reference#TurnManager]] - API reference