
Comprehensive guide to the action system - how skills, items, moves, and talk options work.

---

## Overview

The action system is **data-driven** via JSON configuration. All actions share a common `ActionConfig` structure, making the system flexible and extensible.

### Action Types

1. **Skills** - Combat abilities (damage, healing, buffs)
2. **Items** - Consumable/usable objects (potions, bombs)
3. **Moves** - Movement actions (walk, dash, teleport)
4. **Talk** - Social interactions (negotiate, intimidate, rally)

All types use the same underlying targeting and execution system.

---

## ActionConfig Structure

```json
{
  "id": "direct_hit",
  "name": "Direct Hit",
  "description": "Attacks enemies in range",
  "damage": 20,
  "cost": 3,
  "range": 2,
  "targetType": "Area",
  "useRadiusRange": true,
  "aoePattern": [{"x": 0, "y": 0}]
}
```

### Core Properties

| Property | Type | Purpose |
|----------|------|---------|
| `id` | string | Unique identifier |
| `name` | string | Display name |
| `description` | string | Help text |
| `cost` | int | Money/resource cost |
| `targetType` | string | See [Target Types](#target-types) |

### Effect Properties

| Property | Type | Purpose |
|----------|------|---------|
| `damage` | int | HP damage dealt |
| `healAmount` | int | HP restored |
| `statusEffect` | string | Status to apply |
| `statusDuration` | int | Turns status lasts |

### Targeting Properties

| Property | Type | Purpose |
|----------|------|---------|
| `range` | int | Maximum distance |
| `useRadiusRange` | bool | Use cube distance? |
| `rangePattern` | PatternCell[] | Custom range shape |
| `aoePattern` | PatternCell[] | Affected cells pattern |
| `aoeRadius` | int | Circular AOE radius |
| `aoeType` | string | "", "radius", "line" |

### Filter Properties

| Property | Type | Purpose |
|----------|------|---------|
| `excludeSelf` | bool | Can't target self |
| `excludeOccupied` | bool | Only empty cells |
| `targetEmptyCellsOnly` | bool | Ground-only |
| `targetSelfOnly` | bool | Only self-cast |
| `excludeTypes` | string[] | Filtered entity types |
| `requiresLineOfSight` | bool | Need clear path |

---

## Target Types

### Single Target Types

**"Self"**
- Only targets the caster
- Example: Self-buff, self-heal
- Range typically 0

**"Single"**
- One specific target
- Can be enemy, ally, or any entity
- Range determines max distance

### Area Target Types

**"Area"**
- Generic area targeting
- Can hit any entity type
- Controlled by filters (`excludeTypes`)

**"Enemy"**
- Only targets enemy entities
- Player/Ally can target Enemies
- Automatically filters valid targets

**"Ally"**
- Only targets allied entities
- Player can target other allies
- Excludes enemies

**"Movement"**
- Special type for movement actions
- Only targets empty, walkable cells
- Automatically excludes occupied cells

---

## Range Calculation Methods

### Method 1: Radius Range (Recommended)

```json
{
  "range": 3,
  "useRadiusRange": true
}
```

**How it works**:
- Uses cube coordinate math
- Calculates exact hex distance
- Creates perfect circular range
- **Most common method**

**Algorithm**:
```csharp
for q in [-range...range]:
    for r in [-range...range]:
        for s in [-range...range]:
            if q + r + s == 0:
                distance = (|q| + |r| + |s|) / 2
                if distance <= range:
                    validTarget()
```

### Method 2: Explicit Pattern

```json
{
  "range": 1,
  "rangePattern": [
    {"x": 1, "y": 0},
    {"x": -1, "y": 0},
    {"x": 0, "y": 1},
    {"x": 0, "y": -1},
    {"x": 1, "y": -1},
    {"x": -1, "y": -1}
  ]
}
```

**How it works**:
- Manually defines each valid cell offset
- Useful for irregular shapes
- Example: Cardinal directions only

### Method 3: All Tiles Valid

```json
{
  "range": 999,
  "allTilesValid": true
}
```

**How it works**:
- Every cell on the map is valid
- Still filtered by excludeOccupied, etc.
- Example: Teleport skill

---

## AOE (Area of Effect) Systems

### AOE Method 1: Single Target (No AOE)

```json
{
  "aoePattern": [{"x": 0, "y": 0}]
}
```

**Result**: Only the targeted cell is affected

### AOE Method 2: Radius AOE

```json
{
  "aoeRadius": 2
}
```

**How it works**:
- Same cube coordinate math as range
- Centered on target cell
- Example: Explosion hitting all cells within radius 2

### AOE Method 3: Pattern AOE

```json
{
  "aoePattern": [
    {"x": 0, "y": 0},
    {"x": 1, "y": 0},
    {"x": -1, "y": 0}
  ]
}
```

**How it works**:
- Pattern applied relative to target
- Example: Line of 3 cells

### AOE Method 4: Line AOE

```json
{
  "aoeType": "line",
  "aoeWidth": 1,
  "aoeOvershoot": 5
}
```

**How it works**:
- Draws line from caster to target
- Extends beyond target by `aoeOvershoot` cells
- `aoeWidth` currently unused (for future)
- Example: Laser beam

**Algorithm**:
```csharp
// Get direction from caster to target
direction = normalize(target - caster)

// Draw line to target
lineCells = HexLineDraw(caster, target)

// Extend beyond target
for i in 0..aoeOvershoot:
    nextCell = target + (direction * i)
    lineCells.Add(nextCell)
```

---

## Advanced Targeting Features

### Whitelist/Blacklist System

**Whitelist**: Add additional valid cells
```json
{
  "whitelist": [
    {"type": "coordinate", "x": 2, "y": 3},
    {"type": "radius", "center": {"x": 5, "y": 5}, "radius": 2}
  ]
}
```

**Blacklist**: Remove cells from valid targets
```json
{
  "blacklist": [
    {"type": "coordinate", "x": 0, "y": 0},
    {"type": "radius", "center": {"x": 0, "y": 0}, "radius": 1}
  ]
}
```

**Use case**: Donut pattern (radius 5 whitelist, radius 1 blacklist)

### Inverse AOE

```json
{
  "inverseAOE": true,
  "aoePattern": [
    {"x": 1, "y": 0},
    {"x": -1, "y": 0},
    {"x": 0, "y": 1},
    {"x": 0, "y": -1}
  ]
}
```

**Effect**: Target cell is NOT affected, only surrounding cells
**Use case**: Ring of fire around target

### Exclude Origin

```json
{
  "excludeOrigin": true
}
```

**Effect**: Caster's position excluded from AOE
**Use case**: Line attack that starts 1 cell away

---

## Filter System

### Entity Type Filtering

```json
{
  "excludeTypes": ["Player", "Ally", "NPC"]
}
```

**Effect**: These entity types cannot be targeted
**Use case**: Enemy-only damage skill

### Occupation Filtering

```json
{
  "excludeOccupied": true
}
```

**Effect**: Can only target empty cells
**Use case**: Trap placement

```json
{
  "targetEmptyCellsOnly": true
}
```

**Effect**: Same as excludeOccupied but more explicit

### Self Filtering

```json
{
  "excludeSelf": true
}
```

**Effect**: Cannot target own position
**Use case**: Healing others but not self

```json
{
  "targetSelfOnly": true
}
```

**Effect**: Can ONLY target self
**Use case**: Self-shield

### Line of Sight

```json
{
  "requiresLineOfSight": true
}
```

**Effect**: Must have clear path to target
**Algorithm**: Uses Bresenham hex line drawing, checks for obstacles

---

## Action Execution Flow

### 1. Action Request
```csharp
BattleActionHandler.ProcessActionRequest("skill", "")
```

### 2. Submenu Selection
```csharp
BattleActionHandler.ProcessSubmenuSelection("Direct Hit")
// Loads ActionConfig
// Calculates valid targets
```

### 3. Target Calculation
```csharp
validTargets = CalculateValidTargets(actionConfig)
```

**Steps**:
1. Generate base range pattern
2. Apply whitelist additions
3. Apply blacklist removals
4. Filter by grid validity
5. Filter by line of sight
6. Filter by target type
7. Filter by occupation
8. Filter by entity type
9. Add/remove self based on config

### 4. Target Selection
User selects a cell from `validTargets`

### 5. AOE Calculation
```csharp
affectedCells = CalculateAffectedCells(targetCell, actionConfig)
```

**Steps**:
1. Add target cell (unless inverseAOE)
2. Apply AOE pattern/radius/line
3. Filter affected cells by grid validity

### 6. Action Execution
```csharp
foreach (cell in affectedCells) {
    if (damage > 0) ApplyDamage(cell, damage)
    if (healAmount > 0) ApplyHealing(cell, healAmount)
    if (statusEffect) ApplyStatus(cell, statusEffect)
}
```

---

## Example Configurations

### Example 1: Basic Attack

```json
{
  "id": "basic_attack",
  "name": "Basic Attack",
  "damage": 15,
  "cost": 0,
  "range": 1,
  "targetType": "Enemy",
  "useRadiusRange": true,
  "aoePattern": [{"x": 0, "y": 0}]
}
```

**Behavior**: Attack adjacent enemy for 15 damage

### Example 2: AOE Healing

```json
{
  "id": "healing_wave",
  "name": "Healing Wave",
  "healAmount": 30,
  "cost": 5,
  "range": 2,
  "targetType": "Ally",
  "useRadiusRange": true,
  "aoeRadius": 1,
  "excludeSelf": true
}
```

**Behavior**: Target ally within 2 cells, heal them + adjacent allies

### Example 3: Line Attack

```json
{
  "id": "laser_beam",
  "name": "Laser Beam",
  "damage": 25,
  "cost": 6,
  "range": 1,
  "targetType": "Area",
  "rangePattern": [
    {"x": 1, "y": 0},
    {"x": -1, "y": 0},
    {"x": 0, "y": 1},
    {"x": 0, "y": -1}
  ],
  "aoeType": "line",
  "aoeWidth": 1,
  "aoeOvershoot": 5,
  "excludeOrigin": true
}
```

**Behavior**: Target adjacent cell, shoot line 5 cells beyond

### Example 4: Teleport

```json
{
  "id": "teleport",
  "name": "Teleport",
  "cost": 3,
  "range": 999,
  "targetType": "Movement",
  "allTilesValid": true,
  "excludeOccupied": true,
  "requiresLineOfSight": false,
  "aoePattern": [{"x": 0, "y": 0}]
}
```

**Behavior**: Move to any empty cell on map

### Example 5: Donut AOE

```json
{
  "id": "ring_of_fire",
  "name": "Ring of Fire",
  "damage": 30,
  "cost": 8,
  "range": 5,
  "targetType": "Area",
  "useRadiusRange": true,
  "blacklist": [
    {"type": "radius", "center": {"x": 0, "y": 0}, "radius": 1}
  ],
  "aoeRadius": 1
}
```

**Behavior**: Target within 5, damage ring around target (not center)

---

## Special Action Types

### Movement Actions

**Key Properties**:
- `targetType: "Movement"`
- `excludeOccupied: true`
- No damage/healing

**Execution**:
```csharp
stateManager.MoveEntity(entity, targetCell)
```

### Talk Actions

**Key Properties**:
- `dialogue` - What to say
- `friendshipChange` - Relationship modifier
- `reputationChange` - Global reputation
- `moneyChange` - Money reward/cost

**Example**:
```json
{
  "id": "negotiate",
  "name": "Negotiate",
  "dialogue": "Let's talk this through...",
  "friendshipChange": 15,
  "moneyChange": -5,
  "range": 2,
  "targetType": "Enemy"
}
```

### Item Actions

**Key Properties**:
- `usesRemaining` - Limited uses (-1 = infinite)

**Execution**:
- Same as skills
- Decrements `usesRemaining`
- Removed when depleted

---

## NPC Action Support

NPCs use the same action system but with special methods:

```csharp
// Calculate valid targets from NPC position
validTargets = CalculateValidTargetsFromPosition(
    npcPosition, 
    actionConfig
)

// Calculate AOE from NPC position
affectedCells = CalculateAffectedCellsFromPosition(
    npcPosition,
    targetCell,
    actionConfig
)
```

**Key Difference**: Player-centric methods assume player position, NPC methods accept arbitrary positions

---

## Hex Coordinate Math

### Offset → Cube Conversion

```csharp
int x = col - (row - (row & 1)) / 2
int z = row
int y = -x - z
```

### Cube → Offset Conversion

```csharp
int col = cubeX
int row = cubeZ + (cubeX - (cubeX & 1)) / 2
```

### Hex Distance

```csharp
distance = (|x1-x2| + |y1-y2| + |z1-z2|) / 2
```

### Hex Neighbors (Offset)

**Even columns**:
```
(0,-1), (1,-1), (1,0), (0,1), (-1,0), (-1,-1)
```

**Odd columns**:
```
(0,-1), (1,0), (1,1), (0,1), (-1,1), (-1,0)
```

---

## Debugging Actions

### Debug Mode

```json
{
  "debugMode": true
}
```

**Effect**: Enables verbose logging for this action

**Logs**:
- Target calculation steps
- Valid target count
- AOE cell count
- Execution details

### Common Issues

**Issue**: Action has no valid targets
**Fix**: Check `useRadiusRange`, `range`, `excludeTypes`

**Issue**: AOE not showing correctly
**Fix**: Check `aoePattern` vs `aoeRadius`, ensure coordinates correct

**Issue**: Can't target self
**Fix**: Ensure `excludeSelf: false` and `targetType` allows self

**Issue**: Targeting through walls
**Fix**: Add `requiresLineOfSight: true`

---

## Extending the Action System

### Adding New Effect Types

1. Add property to `ActionConfig.cs`
2. Handle in `BattleActionHandler.ExecuteSkillAction()`
3. Apply via `BattleStateManager`

**Example**: Adding "Shield" effect
```csharp
// ActionConfig.cs
public int ShieldAmount { get; set; }

// BattleActionHandler.cs
if (config.ShieldAmount > 0) {
    stateManager.ApplyShieldToEntity(cell, config.ShieldAmount);
}
```

### Adding New Target Filters

1. Add property to `ActionConfig.cs`
2. Check in `BattleActionHandler.PassesTargetFilters()`

**Example**: Max HP filter
```csharp
// ActionConfig.cs
public float MaxHPThreshold { get; set; }

// BattleActionHandler.cs
if (config.MaxHPThreshold > 0) {
    var entity = stateManager.GetEntityAt(cell);
    if (entity.MaxHP > config.MaxHPThreshold) return false;
}
```

---

## Related Documentation

- [[Targeting System]] - Deep dive into targeting algorithms
- [[Configuration Files]] - JSON structure reference
- [[Component Reference]] - BattleActionHandler details
- [[Data Flow]] - Action execution sequence