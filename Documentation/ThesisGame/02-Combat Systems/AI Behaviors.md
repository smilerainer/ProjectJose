
Complete guide to NPC decision-making and behavior patterns.

---

## Overview

The AI system uses **pluggable behavior strategies** to control NPC actions. Each behavior implements `INPCBehavior` interface and makes decisions based on battle state.

**Architecture**:
```
NPCBehaviorManager (coordinator)
    ↓
INPCBehavior (interface)
    ↓
Concrete Behaviors:
├── AggressiveBehavior
├── DefensiveBehavior
├── SupportBehavior
├── BalancedBehavior
└── CowardlyBehavior
```

---

## Behavior Interface

### INPCBehavior

```csharp
public interface INPCBehavior
{
    /// <summary>
    /// Get the name of this behavior for registration
    /// </summary>
    string GetBehaviorName();
    
    /// <summary>
    /// Decide what action the NPC should take this turn
    /// </summary>
    NPCDecision DecideAction(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler
    );
}
```

### NPCDecision Structure

```csharp
public struct NPCDecision
{
    public string ActionType;   // "skill", "move", "item", "talk", "skip"
    public string ActionName;   // Specific action to use
    public Vector2I TargetCell; // Where to target
    public bool IsValid;        // Can be executed?
    public int Priority;        // For comparing options (higher = better)
    
    // Helper methods
    public static NPCDecision Invalid();
    public static NPCDecision Skip();
}
```

**Priority System**:
- 15+ : Emergency/critical action
- 10-14: High priority action
- 5-9: Normal action
- 1-4: Low priority action
- 0: Skip turn

---

## Behavior 1: Aggressive

**Strategy**: "Attack! Attack! Attack!"

**Decision Tree**:
```
1. Find skills with damage > 0
2. Order by damage (highest first)
3. For each skill:
   - Get valid targets
   - Filter to enemies only
   - Pick nearest enemy
   - Return attack decision (Priority: 10)
4. If no attacks available: Skip
```

### Implementation

```csharp
public class AggressiveBehavior : INPCBehavior
{
    public string GetBehaviorName() => "aggressive";
    
    public NPCDecision DecideAction(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        // Get available skills
        var skills = configLoader.GetSkillsForEntity(actor.Type.ToString());
        
        // Filter by entity's available skills
        if (actor.AvailableSkills.Count > 0)
            skills = skills.Where(s => 
                actor.AvailableSkills.Contains(s.Id) || 
                actor.AvailableSkills.Contains(s.Name)
            ).ToList();
        
        // Try each damaging skill
        foreach (var skill in skills.Where(s => s.Damage > 0).OrderByDescending(s => s.Damage))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(
                actor.Position, 
                skill
            );
            
            var enemyTargets = validTargets
                .Where(t => IsEnemyTarget(actor, t, stateManager))
                .ToList();
            
            if (enemyTargets.Count > 0)
            {
                // Pick nearest enemy
                var target = enemyTargets
                    .OrderBy(t => HexDistance(actor.Position, t))
                    .First();
                
                return new NPCDecision
                {
                    ActionType = "skill",
                    ActionName = skill.Name,
                    TargetCell = target,
                    IsValid = true,
                    Priority = 10
                };
            }
        }
        
        return NPCDecision.Skip();
    }
}
```

**Characteristics**:
- Always attacks if possible
- Prefers highest damage skills
- Targets nearest enemy
- No self-preservation
- No retreating

**Best For**:
- Berserkers
- Zombies
- Suicide units
- Boss minions

---

## Behavior 2: Defensive

**Strategy**: "Survive first, attack second"

**Decision Tree**:
```
1. IF HP < healthThreshold (30%):
   - Try to heal self → Priority 15
   - If can't heal, try items
2. IF enemies within 2 cells:
   - Try to retreat → Priority 12
3. ELSE:
   - Try long-range attack → Priority 8
4. If nothing works: Skip
```

### Implementation

```csharp
public class DefensiveBehavior : INPCBehavior
{
    public string GetBehaviorName() => "defensive";
    
    public NPCDecision DecideAction(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        // Self-preservation if low health
        if (actor.IsLowHealth(actor.BehaviorConfig.HealthThreshold))
        {
            var healDecision = TryHeal(actor, stateManager, configLoader, actionHandler);
            if (healDecision.IsValid) return healDecision;
        }
        
        // Retreat if threatened
        if (IsThreatenedByNearbyEnemies(actor, stateManager))
        {
            var retreatDecision = TryRetreat(actor, stateManager, configLoader, actionHandler);
            if (retreatDecision.IsValid) return retreatDecision;
        }
        
        // Attack if safe
        var attackDecision = TryAttack(actor, stateManager, configLoader, actionHandler);
        if (attackDecision.IsValid) return attackDecision;
        
        return NPCDecision.Skip();
    }
    
    private NPCDecision TryHeal(...)
    {
        var skills = configLoader.GetSkillsForEntity(actor.Type.ToString());
        
        // Filter to healing skills that target self
        foreach (var skill in skills.Where(s => 
            s.HealAmount > 0 && 
            s.TargetType.ToLower() == "self"
        ).OrderByDescending(s => s.HealAmount))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(
                actor.Position, 
                skill
            );
            
            if (validTargets.Contains(actor.Position))
            {
                return new NPCDecision
                {
                    ActionType = "skill",
                    ActionName = skill.Name,
                    TargetCell = actor.Position,
                    IsValid = true,
                    Priority = 15  // High priority!
                };
            }
        }
        
        return NPCDecision.Invalid();
    }
    
    private NPCDecision TryRetreat(...)
    {
        var moveOptions = configLoader.GetMoveOptionsForEntity(actor.Type.ToString());
        
        // Filter by available moves
        if (actor.AvailableMoveOptions.Count > 0)
            moveOptions = moveOptions.Where(m => 
                actor.AvailableMoveOptions.Contains(m.Id) || 
                actor.AvailableMoveOptions.Contains(m.Name)
            ).ToList();
        
        var nearestEnemy = GetNearestEnemyPosition(actor, stateManager);
        
        foreach (var move in moveOptions.OrderByDescending(m => m.Range))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(
                actor.Position, 
                move
            );
            
            if (validTargets.Count > 0)
            {
                // Pick cell farthest from enemy
                var target = validTargets
                    .OrderByDescending(t => HexDistance(t, nearestEnemy))
                    .First();
                
                return new NPCDecision
                {
                    ActionType = "move",
                    ActionName = move.Name,
                    TargetCell = target,
                    IsValid = true,
                    Priority = 12
                };
            }
        }
        
        return NPCDecision.Invalid();
    }
    
    private NPCDecision TryAttack(...)
    {
        var skills = configLoader.GetSkillsForEntity(actor.Type.ToString());
        
        // Prefer longer range attacks
        foreach (var skill in skills
            .Where(s => s.Damage > 0)
            .OrderByDescending(s => s.Range)
            .ThenByDescending(s => s.Damage))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(
                actor.Position, 
                skill
            );
            
            var enemyTargets = validTargets
                .Where(t => IsEnemyTarget(actor, t, stateManager))
                .ToList();
            
            if (enemyTargets.Count > 0)
            {
                var target = enemyTargets
                    .OrderBy(t => HexDistance(actor.Position, t))
                    .First();
                
                return new NPCDecision
                {
                    ActionType = "skill",
                    ActionName = skill.Name,
                    TargetCell = target,
                    IsValid = true,
                    Priority = 8
                };
            }
        }
        
        return NPCDecision.Invalid();
    }
}
```

**Characteristics**:
- Heals when HP < 30%
- Retreats when enemies nearby
- Prefers long-range attacks
- Cautious playstyle

**Best For**:
- Archers
- Mages
- Support units
- Cowardly enemies

---

## Behavior 3: Support

**Strategy**: "Help allies, attack as last resort"

**Decision Tree**:
```
1. Find most wounded ally (< 80% HP):
   - Try to heal them → Priority 15
2. If no wounded allies:
   - Try to buff allies → Priority 10
3. If no support needed:
   - Attack weakest enemy → Priority 5
4. If nothing works: Skip
```

### Implementation

```csharp
public class SupportBehavior : INPCBehavior
{
    public string GetBehaviorName() => "support";
    
    public NPCDecision DecideAction(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        // Try to heal wounded allies
        var healDecision = TryHealAlly(actor, stateManager, configLoader, actionHandler);
        if (healDecision.IsValid) return healDecision;
        
        // Try to buff allies
        var buffDecision = TryBuffAlly(actor, stateManager, configLoader, actionHandler);
        if (buffDecision.IsValid) return buffDecision;
        
        // Attack if no support needed
        var attackDecision = TryAttack(actor, stateManager, configLoader, actionHandler);
        if (attackDecision.IsValid) return attackDecision;
        
        return NPCDecision.Skip();
    }
    
    private NPCDecision TryHealAlly(...)
    {
        var skills = configLoader.GetSkillsForEntity(actor.Type.ToString());
        
        foreach (var skill in skills
            .Where(s => s.HealAmount > 0)
            .OrderByDescending(s => s.HealAmount))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(
                actor.Position, 
                skill
            );
            
            // Find allies that need healing
            var allyTargets = validTargets
                .Where(t => IsAllyTarget(actor, t, stateManager) && IsWounded(t, stateManager))
                .ToList();
            
            if (allyTargets.Count > 0)
            {
                // Prioritize most wounded
                var target = GetMostWoundedTarget(allyTargets, stateManager);
                
                return new NPCDecision
                {
                    ActionType = "skill",
                    ActionName = skill.Name,
                    TargetCell = target,
                    IsValid = true,
                    Priority = 15
                };
            }
        }
        
        // Try healing items
        var items = configLoader.GetItemsForEntity(actor.Type.ToString());
        
        foreach (var item in items
            .Where(i => i.HealAmount > 0)
            .OrderByDescending(i => i.HealAmount))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(
                actor.Position, 
                item
            );
            
            var allyTargets = validTargets
                .Where(t => IsAllyTarget(actor, t, stateManager) && IsWounded(t, stateManager))
                .ToList();
            
            if (allyTargets.Count > 0)
            {
                var target = GetMostWoundedTarget(allyTargets, stateManager);
                
                return new NPCDecision
                {
                    ActionType = "item",
                    ActionName = item.Name,
                    TargetCell = target,
                    IsValid = true,
                    Priority = 14
                };
            }
        }
        
        return NPCDecision.Invalid();
    }
    
    private bool IsWounded(Vector2I position, BattleStateManager stateManager)
    {
        var entity = stateManager.GetEntityAt(position);
        return entity != null && entity.CurrentHP < entity.MaxHP * 0.8f;
    }
    
    private Vector2I GetMostWoundedTarget(List<Vector2I> targets, BattleStateManager stateManager)
    {
        return targets
            .Select(t => new { Position = t, Entity = stateManager.GetEntityAt(t) })
            .Where(x => x.Entity != null)
            .OrderBy(x => x.Entity.CurrentHP / x.Entity.MaxHP)
            .First()
            .Position;
    }
}
```

**Characteristics**:
- Prioritizes healing allies
- Applies buffs when no healing needed
- Only attacks when support unnecessary
- Team-oriented

**Best For**:
- Clerics
- Medics
- Support mages
- Dedicated healers

---

## Behavior 4: Balanced

**Strategy**: "Adapt to the situation"

**Decision Tree**:
```
1. IF HP < 40%:
   - Try heal self → Priority 15
   - Try retreat → Priority 12
2. IF good attack opportunity (priority ≥ 8):
   - Attack → Priority varies
3. ELSE:
   - Try reposition to optimal range → Priority 6
4. IF nothing better:
   - Attack anyway → Priority varies
5. If nothing works: Skip
```

### Implementation

```csharp
public class BalancedBehavior : INPCBehavior
{
    public string GetBehaviorName() => "balanced";
    
    public NPCDecision DecideAction(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        // Self-preservation if low health
        if (actor.IsLowHealth(0.4f))
        {
            var healDecision = TryHealSelf(actor, stateManager, configLoader, actionHandler);
            if (healDecision.IsValid) return healDecision;
            
            var retreatDecision = TryRetreat(actor, stateManager, configLoader, actionHandler);
            if (retreatDecision.IsValid) return retreatDecision;
        }
        
        // Attack if good opportunity
        var attackDecision = TryAttack(actor, stateManager, configLoader, actionHandler);
        if (attackDecision.IsValid && attackDecision.Priority >= 8)
            return attackDecision;
        
        // Move into better position
        var repositionDecision = TryReposition(actor, stateManager, configLoader, actionHandler);
        if (repositionDecision.IsValid) return repositionDecision;
        
        // Attack even if not ideal
        if (attackDecision.IsValid) return attackDecision;
        
        return NPCDecision.Skip();
    }
    
    private NPCDecision TryAttack(...)
    {
        var skills = configLoader.GetSkillsForEntity(actor.Type.ToString());
        
        NPCDecision bestDecision = NPCDecision.Invalid();
        int bestPriority = 0;
        
        foreach (var skill in skills.Where(s => s.Damage > 0))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(
                actor.Position, 
                skill
            );
            
            var enemyTargets = validTargets
                .Where(t => IsEnemyTarget(actor, t, stateManager))
                .ToList();
            
            if (enemyTargets.Count > 0)
            {
                var target = GetBestTarget(actor, enemyTargets, stateManager);
                int priority = CalculateAttackPriority(actor, skill, target, stateManager);
                
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    bestDecision = new NPCDecision
                    {
                        ActionType = "skill",
                        ActionName = skill.Name,
                        TargetCell = target,
                        IsValid = true,
                        Priority = priority
                    };
                }
            }
        }
        
        return bestDecision;
    }
    
    private int CalculateAttackPriority(
        Entity actor, 
        ActionConfig skill, 
        Vector2I target, 
        BattleStateManager stateManager)
    {
        int priority = 5; // Base priority
        
        var targetEntity = stateManager.GetEntityAt(target);
        if (targetEntity == null) return priority;
        
        // Bonus for low HP targets
        if (targetEntity.IsLowHealth(0.5f)) priority += 3;
        if (targetEntity.IsLowHealth(0.3f)) priority += 2;
        
        // Bonus for high damage skills
        priority += (int)(skill.Damage / 10);
        
        // Bonus for close range
        int distance = HexDistance(actor.Position, target);
        if (distance <= 2) priority += 2;
        
        return priority;
    }
    
    private Vector2I GetBestTarget(
        Entity actor, 
        List<Vector2I> targets, 
        BattleStateManager stateManager)
    {
        return targets
            .Select(t => new 
            { 
                Position = t, 
                Entity = stateManager.GetEntityAt(t),
                Distance = HexDistance(actor.Position, t)
            })
            .Where(x => x.Entity != null)
            .OrderBy(x => x.Entity.CurrentHP)  // Lowest HP first
            .ThenBy(x => x.Distance)            // Then nearest
            .First()
            .Position;
    }
    
    private NPCDecision TryReposition(...)
    {
        var moveOptions = configLoader.GetMoveOptionsForEntity(actor.Type.ToString());
        var nearestEnemy = GetNearestEnemyPosition(actor, stateManager);
        
        if (nearestEnemy == Vector2I.Zero) return NPCDecision.Invalid();
        
        int currentDistance = HexDistance(actor.Position, nearestEnemy);
        int idealDistance = 2; // Prefer medium range
        
        foreach (var move in moveOptions.OrderByDescending(m => m.Range))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(
                actor.Position, 
                move
            );
            
            if (validTargets.Count > 0)
            {
                var target = validTargets
                    .OrderBy(t => Mathf.Abs(HexDistance(t, nearestEnemy) - idealDistance))
                    .First();
                
                int newDistance = HexDistance(target, nearestEnemy);
                
                // Only move if it improves position
                if (Mathf.Abs(newDistance - idealDistance) < 
                    Mathf.Abs(currentDistance - idealDistance))
                {
                    return new NPCDecision
                    {
                        ActionType = "move",
                        ActionName = move.Name,
                        TargetCell = target,
                        IsValid = true,
                        Priority = 6
                    };
                }
            }
        }
        
        return NPCDecision.Invalid();
    }
}
```

**Characteristics**:
- Adapts to situation
- Uses priority scoring
- Positions strategically
- Targets wounded enemies
- Most complex behavior

**Best For**:
- Versatile units
- Tactical enemies
- Boss fights
- Default NPC behavior

---

## Behavior 5: Cowardly

**Strategy**: "Run away!"

**Decision Tree**:
```
1. Find nearest enemy
2. Try to move as far as possible from them → Priority 15
3. If can't move: Skip
```

### Implementation

```csharp
public class CowardlyBehavior : INPCBehavior
{
    public string GetBehaviorName() => "cowardly";
    
    public NPCDecision DecideAction(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        // Always try to flee from enemies
        var moveOptions = configLoader.GetMoveOptionsForEntity(actor.Type.ToString());
        
        if (actor.AvailableMoveOptions.Count > 0)
            moveOptions = moveOptions.Where(m => 
                actor.AvailableMoveOptions.Contains(m.Id) || 
                actor.AvailableMoveOptions.Contains(m.Name)
            ).ToList();
        
        var nearestEnemy = GetNearestEnemyPosition(actor, stateManager);
        
        if (nearestEnemy != Vector2I.Zero)
        {
            foreach (var move in moveOptions.OrderByDescending(m => m.Range))
            {
                var validTargets = actionHandler.CalculateValidTargetsFromPosition(
                    actor.Position, 
                    move
                );
                
                if (validTargets.Count > 0)
                {
                    // Pick cell farthest from enemy
                    var target = validTargets
                        .OrderByDescending(t => HexDistance(t, nearestEnemy))
                        .First();
                    
                    return new NPCDecision
                    {
                        ActionType = "move",
                        ActionName = move.Name,
                        TargetCell = target,
                        IsValid = true,
                        Priority = 15
                    };
                }
            }
        }
        
        return NPCDecision.Skip();
    }
}
```

**Characteristics**:
- Never attacks
- Always runs away
- High priority (desperate)
- Simple behavior

**Best For**:
- Civilians
- Weak enemies
- Escort mission targets
- Comic relief

---

## NPCBehaviorManager

### Coordinator Class

```csharp
public class NPCBehaviorManager
{
    private Dictionary<string, INPCBehavior> behaviors = new();
    private BattleStateManager stateManager;
    private BattleConfigurationLoader configLoader;
    private BattleActionHandler actionHandler;
    
    public void Initialize(
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        this.stateManager = stateManager;
        this.configLoader = configLoader;
        this.actionHandler = actionHandler;
        
        RegisterDefaultBehaviors();
    }
    
    private void RegisterDefaultBehaviors()
    {
        RegisterBehavior(new AggressiveBehavior());
        RegisterBehavior(new DefensiveBehavior());
        RegisterBehavior(new SupportBehavior());
        RegisterBehavior(new BalancedBehavior());
        RegisterBehavior(new CowardlyBehavior());
    }
    
    public void RegisterBehavior(INPCBehavior behavior)
    {
        behaviors[behavior.GetBehaviorName()] = behavior;
    }
    
    public NPCDecision GetDecisionForEntity(Entity entity)
    {
        if (!entity.IsAlive || !entity.CanAct)
        {
            return NPCDecision.Skip();
        }
        
        var behaviorType = entity.BehaviorType ?? "balanced";
        
        if (!behaviors.TryGetValue(behaviorType, out var behavior))
        {
            GD.PrintErr($"Unknown behavior: {behaviorType}, using balanced");
            behavior = behaviors["balanced"];
        }
        
        return behavior.DecideAction(
            entity, 
            stateManager, 
            configLoader, 
            actionHandler
        );
    }
    
    public void ExecuteDecision(Entity entity, NPCDecision decision)
    {
        if (!decision.IsValid || decision.ActionType == "skip")
        {
            GD.Print($"{entity.Name} skips turn");
            return;
        }
        
        // Execute based on action type
        if (decision.ActionType == "move")
        {
            stateManager.MoveEntity(entity, decision.TargetCell);
        }
        else if (decision.ActionType == "skill")
        {
            var config = configLoader.GetActionConfig(decision.ActionName);
            if (config != null)
            {
                var affectedCells = actionHandler.CalculateAffectedCellsFromPosition(
                    entity.Position, 
                    decision.TargetCell, 
                    config
                );
                
                foreach (var cell in affectedCells)
                {
                    if (config.Damage > 0)
                        stateManager.ApplyDamageToEntity(cell, config.Damage);
                    if (config.HealAmount > 0)
                        stateManager.ApplyHealingToEntity(cell, config.HealAmount);
                }
            }
        }
        
        entity.HasActedThisTurn = true;
    }
}
```

---

## Behavior Configuration

### NPCBehaviorConfig

```csharp
public class NPCBehaviorConfig
{
    public string BehaviorType { get; set; } = "balanced";
    
    // Personality (0-10)
    public int AggressionLevel { get; set; } = 5;
    public int CautiousnessLevel { get; set; } = 5;
    public float HealthThreshold { get; set; } = 0.3f;
    
    // Action Priorities (0-10)
    public int AttackPriority { get; set; } = 5;
    public int DefendPriority { get; set; } = 5;
    public int SupportPriority { get; set; } = 5;
    public int MovePriority { get; set; } = 5;
    
    // Targeting
    public List<string> PreferredTargets { get; set; } = new();
    public bool AvoidFriendlyFire { get; set; } = true;
    public bool PreferGroupedTargets { get; set; } = false;
    
    // Skills
    public List<string> PreferredSkills { get; set; } = new();
    public List<string> EmergencySkills { get; set; } = new();
    public int MinRangePreference { get; set; } = 0;
    public int MaxRangePreference { get; set; } = 10;
}
```

### JSON Configuration Example

```json
{
  "id": "boss_mage",
  "entityType": "Enemy",
  "behaviorConfig": {
    "behaviorType": "balanced",
    "aggressionLevel": 7,
    "cautiousnessLevel": 6,
    "healthThreshold": 0.4,
    "attackPriority": 8,
    "defendPriority": 6,
    "supportPriority": 3,
    "movePriority": 5,
    "preferredTargets": ["Player"],
    "avoidFriendlyFire": true,
    "preferGroupedTargets": true,
    "preferredSkills": ["fireball", "lightning"],
    "emergencySkills": ["teleport", "heal"],
    "minRangePreference": 3,
    "maxRangePreference": 6
  }
}
```

---

## Creating Custom Behaviors

### Step 1: Implement Interface

```csharp
public class BerserkerBehavior : INPCBehavior
{
    public string GetBehaviorName() => "berserker";
    
    public NPCDecision DecideAction(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        // Custom logic: Attack nearest, get more aggressive when wounded
        var damageMultiplier = 1.0f + (1.0f - actor.GetHealthPercentage());
        
        // Find and attack nearest enemy with increased damage preference
        // ... implementation
        
        return decision;
    }
}
```

### Step 2: Register Behavior

```csharp
// In NPCBehaviorManager.RegisterDefaultBehaviors()
RegisterBehavior(new BerserkerBehavior());
```

### Step 3: Use in JSON

```json
{
  "behaviorConfig": {
    "behaviorType": "berserker"
  }
}
```

---

## Behavior Comparison

| Behavior | Aggression | Defense | Support | Positioning | Complexity |
|----------|-----------|---------|---------|-------------|------------|
| **Aggressive** | ★★★★★ | ☆☆☆☆☆ | ☆☆☆☆☆ | ☆☆☆☆☆ | ★☆☆☆☆ |
| **Defensive** | ★★☆☆☆ | ★★★★★ | ☆☆☆☆☆ | ★★★★☆ | ★★★☆☆ |
| **Support** | ★☆☆☆☆ | ★★☆☆☆ | ★★★★★ | ★★☆☆☆ | ★★★☆☆ |
| **Balanced** | ★★★☆☆ | ★★★☆☆ | ★★☆☆☆ | ★★★★☆ | ★★★★★ |
| **Cowardly** | ☆☆☆☆☆ | ★★★★★ | ☆☆☆☆☆ | ★★★☆☆ | ★☆☆☆☆ |

---

## Common Helper Methods

Used across behaviors:

```csharp
private bool IsEnemyTarget(Entity actor, Vector2I target, BattleStateManager stateManager)
{
    var entityAt = stateManager.GetEntityAt(target);
    return entityAt != null && actor.IsEnemyOf(entityAt);
}

private bool IsAllyTarget(Entity actor, Vector2I target, BattleStateManager stateManager)
{
    var entityAt = stateManager.GetEntityAt(target);
    return entityAt != null && actor.IsAllyOf(entityAt);
}

private Vector2I GetNearestEnemyPosition(Entity actor, BattleStateManager stateManager)
{
    var enemies = stateManager.GetAllEntities()
        .Where(e => e.IsAlive && actor.IsEnemyOf(e))
        .ToList();
    
    if (enemies.Count == 0) return Vector2I.Zero;
    
    return enemies
        .OrderBy(e => HexDistance(actor.Position, e.Position))
        .First()
        .Position;
}

private int HexDistance(Vector2I from, Vector2I to)
{
    int q1 = from.X;
    int r1 = from.Y - (from.X - (from.X & 1)) / 2;
    int q2 = to.X;
    int r2 = to.Y - (to.X - (to.X & 1)) / 2;
    int dq = q2 - q1;
    int dr = r2 - r1;
    return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;
}
```

---

## Testing Behaviors

### Debug NPC Decisions

```csharp
var decision = npcBehaviorManager.GetDecisionForEntity(entity);

GD.Print($"=== {entity.Name} Decision ===");
GD.Print($"  Behavior: {entity.BehaviorType}");
GD.Print($"  Action: {decision.ActionType} - {decision.ActionName}");
GD.Print($"  Target: {decision.TargetCell}");
GD.Print($"  Priority: {decision.Priority}");
GD.Print($"  Valid: {decision.IsValid}");
```

### Test Scenarios

Create test battles with specific setups:
```json
{
  "entities": [
    {"id": "player", "entityType": "Player", ...},
    {"id": "aggressive_test", "behaviorType": "aggressive", ...},
    {"id": "defensive_test", "behaviorType": "defensive", ...}
  ]
}
```

---

## Related Documentation

- [[Entity System]] - Entity data and properties
- [[Turn System]] - How NPCs take turns
- [[Targeting System]] - How NPCs calculate targets
- [[Configuration Files#NPCBehaviorConfig]] - Behavior configuration
- [[Component Reference#NPCBehaviorManager]] - API reference