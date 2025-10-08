
Reference guide for JSON configuration files.

---

## battle_config.json

**Location**: `res://data/battle_config.json`

**Purpose**: Defines all actions (skills/items/moves/talk) and entities for a battle

### Root Structure

```json
{
  "skills": [ /* ActionConfig[] */ ],
  "items": [ /* ActionConfig[] */ ],
  "talkOptions": [ /* ActionConfig[] */ ],
  "moveOptions": [ /* ActionConfig[] */ ],
  "entities": [ /* EntityDefinition[] */ ],
  "settings": { /* GameSettings */ }
}
```

---

## ActionConfig Schema

Used for skills, items, talkOptions, and moveOptions.

```json
{
  // Identity
  "id": "string (required, unique)",
  "name": "string (required)",
  "description": "string (optional)",
  
  // Cost
  "cost": 0,  // Money/resource cost
  
  // Effects
  "damage": 0,
  "healAmount": 0,
  "statusEffect": "string (optional)",
  "statusDuration": 0,
  
  // Targeting
  "range": 1,
  "targetType": "Single|Area|Enemy|Ally|Self|Movement",
  
  // Range Calculation
  "useRadiusRange": false,  // Use cube distance math
  "rangePattern": [
    {"x": 0, "y": 0}  // Explicit cell offsets
  ],
  "allTilesValid": false,  // Every tile is valid
  
  // AOE
  "aoeRadius": 0,  // Circular AOE
  "aoePattern": [
    {"x": 0, "y": 0}  // Explicit AOE offsets
  ],
  "aoeType": "",  // "" | "radius" | "line"
  "aoeWidth": 1,  // For line AOE
  "aoeOvershoot": 0,  // How far line extends
  
  // Filters
  "excludeSelf": false,
  "excludeOccupied": false,
  "targetEmptyCellsOnly": false,
  "targetSelfOnly": false,
  "excludeOrigin": false,
  "excludeTypes": [],  // ["Player", "Ally", "Enemy", "NPC"]
  
  // Advanced
  "inverseAOE": false,  // AOE hits around target, not target
  "requiresLineOfSight": false,
  "ignoreObstacles": false,
  
  // Whitelist/Blacklist
  "whitelist": [
    {
      "type": "coordinate|radius",
      "x": 0, "y": 0,  // For coordinate type
      "center": {"x": 0, "y": 0},  // For radius type
      "radius": 0  // For radius type
    }
  ],
  "blacklist": [ /* Same structure as whitelist */ ],
  
  // Talk-specific
  "dialogue": "string (optional)",
  "friendshipChange": 0,
  "reputationChange": 0,
  "moneyChange": 0,
  
  // Item-specific
  "usesRemaining": -1,  // -1 = infinite
  
  // Debug
  "debugMode": false,
  "speed": 1.0
}
```

### Target Type Reference

| Type | Behavior |
|------|----------|
| `"Self"` | Only caster |
| `"Single"` | One entity |
| `"Area"` | Any entity (filtered by excludeTypes) |
| `"Enemy"` | Enemy entities only |
| `"Ally"` | Allied entities only |
| `"Movement"` | Empty walkable cells only |

---

## EntityDefinition Schema

```json
{
  "id": "string (required, unique)",
  "name": "string (required)",
  "entityType": "Player|Ally|Enemy|NPC|Neutral",
  
  // Stats
  "maxHP": 100.0,
  "initiative": 50,  // Turn order priority
  "speed": 5,  // Tiebreaker for initiative
  
  // Position
  "startPosition": {
    "x": 0,
    "y": 0
  },
  
  // Available Actions
  "availableSkills": ["skill_id1", "skill_id2"],
  "availableItems": ["item_id1"],
  "availableMoveOptions": ["walk", "dash"],
  "availableTalkOptions": ["negotiate"],
  
  // AI Behavior
  "behaviorConfig": {
    "behaviorType": "aggressive|defensive|support|balanced|cowardly",
    
    // Tuning Parameters (0-10)
    "aggressionLevel": 5,
    "cautiousnessLevel": 5,
    "healthThreshold": 0.3,  // 30% HP panic threshold
    
    // Action Priorities (0-10)
    "attackPriority": 5,
    "defendPriority": 5,
    "supportPriority": 5,
    "movePriority": 5,
    
    // Targeting
    "preferredTargets": [],  // Entity IDs or types
    "avoidFriendlyFire": true,
    "preferGroupedTargets": false,
    
    // Skill Selection
    "preferredSkills": [],  // Skill IDs
    "emergencySkills": [],  // Low HP skills
    "minRangePreference": 0,
    "maxRangePreference": 10
  }
}
```

### Entity Type Reference

| Type | Behavior |
|------|----------|
| `"Player"` | Controlled by human |
| `"Ally"` | Friendly NPC, AI controlled |
| `"Enemy"` | Hostile entity |
| `"NPC"` | Neutral character |
| `"Neutral"` | Non-combatant |

### Behavior Type Reference

| Type | Strategy |
|------|----------|
| `"aggressive"` | Always attack nearest enemy |
| `"defensive"` | Heal if low → Retreat if threatened → Attack |
| `"support"` | Heal wounded → Buff allies → Attack |
| `"balanced"` | Adaptive based on situation |
| `"cowardly"` | Always run from enemies |

---

## GameSettings Schema

```json
{
  "startingMoney": 100,
  "maxPartySize": 4,
  "defaultMoveRange": 1,
  "enableFriendlyFire": false,
  "enableCriticalHits": true,
  "enableStatusEffects": true,
  
  "customSettings": {
    "debugMode": true,
    "showRangeIndicators": true,
    "showAoeIndicators": true,
    "showExcludedCells": true,
    "showFilterInfo": true,
    "enableAdvancedCombat": true,
    "useHexGrid": true,
    "logTargetFilters": true,
    "visualizeLineOfSight": true
  }
}
```

---

## story_sequence.json

**Location**: `res://data/story_sequence.json`

**Purpose**: Defines the sequence of VN and Battle scenes

### Structure

```json
{
  "sequences": [
    {
      "id": "intro",
      "type": "vn",
      "timeline": "res://Assets/Dialogue/Timelines/intro.dtl"
    },
    {
      "id": "forest_battle",
      "type": "battle",
      "config": "res://data/battle_config.json",
      "map": "res://Assets/Dialogue/Scenes/Maps/Forest.tscn"
    },
    {
      "id": "victory",
      "type": "vn",
      "timeline": "res://Assets/Dialogue/Timelines/victory.dtl"
    }
  ]
}
```

### Sequence Entry Schema

```json
{
  "id": "string (required, unique)",
  "type": "vn|battle",
  
  // For VN sequences
  "timeline": "res://path/to/timeline.dtl",
  
  // For Battle sequences
  "config": "res://path/to/battle_config.json",
  "map": "res://path/to/map.tscn",
  
  // Optional custom data
  "data": "{\"key\": \"value\"}"  // JSON string
}
```

---

## Example Configurations

### Complete Skill Example

```json
{
  "id": "fireball",
  "name": "Fireball",
  "description": "Launches a ball of fire that explodes on impact",
  "damage": 40,
  "cost": 8,
  "range": 5,
  "targetType": "Area",
  "speed": 0.8,
  "useRadiusRange": true,
  "aoeRadius": 2,
  "excludeTypes": ["Ally"],
  "requiresLineOfSight": true,
  "statusEffect": "Burning",
  "statusDuration": 2,
  "debugMode": false
}
```

**Behavior**:
- Target any cell within 5 hex distance
- Must have line of sight
- Deals 40 damage to all entities within radius 2
- Does not damage allies
- Applies Burning status for 2 turns
- Costs 8 money

### Complete Item Example

```json
{
  "id": "mega_potion",
  "name": "Mega Potion",
  "description": "Powerful healing potion",
  "healAmount": 80,
  "usesRemaining": 2,
  "cost": 0,
  "range": 1,
  "targetType": "Ally",
  "speed": 2.0,
  "useRadiusRange": true,
  "aoePattern": [{"x": 0, "y": 0}],
  "debugMode": false
}
```

**Behavior**:
- Can be used 2 times
- Targets ally within 1 hex
- Heals 80 HP
- No money cost to use

### Complete Move Example

```json
{
  "id": "tactical_dash",
  "name": "Tactical Dash",
  "description": "Quick 3-cell movement ignoring obstacles",
  "range": 3,
  "cost": 2,
  "targetType": "Movement",
  "speed": 3.0,
  "useRadiusRange": true,
  "excludeOccupied": true,
  "requiresLineOfSight": false,
  "ignoreObstacles": true,
  "aoePattern": [{"x": 0, "y": 0}],
  "debugMode": false
}
```

**Behavior**:
- Move up to 3 hexes
- Costs 2 money
- Can move through obstacles
- Cannot land on occupied cells

### Complete Entity Example

```json
{
  "id": "enemy_archer",
  "name": "Goblin Archer",
  "entityType": "Enemy",
  "maxHP": 60,
  "initiative": 55,
  "speed": 6,
  "startPosition": {"x": 8, "y": 3},
  "availableSkills": ["arrow_shot", "poison_arrow"],
  "availableItems": [],
  "availableMoveOptions": ["walk", "retreat"],
  "availableTalkOptions": [],
  "behaviorConfig": {
    "behaviorType": "defensive",
    "aggressionLevel": 6,
    "cautiousnessLevel": 8,
    "healthThreshold": 0.4,
    "attackPriority": 7,
    "defendPriority": 8,
    "supportPriority": 2,
    "movePriority": 7,
    "preferredTargets": ["Player"],
    "avoidFriendlyFire": true,
    "preferGroupedTargets": false,
    "preferredSkills": ["arrow_shot"],
    "emergencySkills": ["smoke_bomb"],
    "minRangePreference": 3,
    "maxRangePreference": 6
  }
}
```

**Behavior**:
- Defensive archer that keeps distance
- Prefers range 3-6
- Retreats when HP < 40%
- High caution and defense priorities

---

## Configuration Best Practices

### 1. Unique IDs
Always use unique IDs for actions and entities
```json
// ✅ Good
"id": "fire_spell_tier1"

// ❌ Bad
"id": "spell"
```

### 2. Descriptive Names
Use clear, readable names
```json
// ✅ Good
"name": "Healing Wave"

// ❌ Bad
"name": "Skill_03"
```

### 3. Balance Range and AOE
Large range + large AOE = overpowered
```json
// ✅ Balanced
{"range": 5, "aoeRadius": 1}

// ❌ Overpowered
{"range": 10, "aoeRadius": 5}
```

### 4. Use Radius for Circles
For circular patterns, use `aoeRadius` not `aoePattern`
```json
// ✅ Good
"aoeRadius": 2

// ❌ Tedious
"aoePattern": [
  {"x": 0, "y": 0},
  {"x": 1, "y": 0},
  {"x": -1, "y": 0},
  // ... 18 more cells
]
```

### 5. Debug During Development
Enable debug mode for testing
```json
{
  "debugMode": true,  // Remember to disable for release
  "description": "DEBUG: Testing line AOE"
}
```

### 6. Group Related Actions
Keep similar actions together in file
```json
{
  "skills": [
    // Tier 1 skills
    {"id": "fire_1", ...},
    {"id": "ice_1", ...},
    
    // Tier 2 skills
    {"id": "fire_2", ...},
    {"id": "ice_2", ...}
  ]
}
```

---

## Validation

### Common Errors

**Missing Required Fields**
```
Error: Action with ID fire_spell has no name
```
Fix: Add `"name": "Fire Spell"`

**Duplicate IDs**
```
Error: Duplicate action ID: heal
```
Fix: Make IDs unique

**Invalid Target Type**
```
Warning: Unknown target type 'Friend'
```
Fix: Use `"Ally"` instead

**Invalid Entity Type**
```
Warning: Unknown entity type 'Monster'
```
Fix: Use `"Enemy"` instead

---

## Loading Process

1. `BattleManager` calls `configLoader.LoadConfiguration()`
2. `CustomJsonLoader.LoadBattleConfig()` opens file
3. JSON deserialized to `BattleConfigData`
4. Lookups built: `allActions[id]`, `actionsByName[name]`
5. Entities converted from definitions
6. Configuration validated
7. Ready for use

---

## Related Documentation

- [[Action System]] - How actions work
- [[Component Reference]] - BattleConfigurationLoader
- [[System Architecture]] - Configuration flow
- [[Data Flow]] - Loading sequence