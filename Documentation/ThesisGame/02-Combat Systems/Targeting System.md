
Deep dive into range calculation, target validation, and AOE mechanics.

---

## Overview

The targeting system determines **which cells can be targeted** and **which cells are affected** by actions. It's implemented primarily in **BattleActionHandler.cs** using hex coordinate mathematics.

**Two-Phase Process**:
1. **Target Calculation** - What cells can be selected?
2. **AOE Calculation** - What cells are affected after selection?

---

## Hex Coordinate Systems

### Offset Coordinates (Used in Code)

```
Even Column (x % 2 == 0):
     (0,-1)   (1,-1)
(-1,0)    (0,0)    (1,0)
     (-1,-1)  (0,1)

Odd Column (x % 2 == 1):
     (0,-1)  (1,0)
(-1,0)   (0,0)    (1,1)
     (-1,1)  (0,1)
```

**Properties**:
- Simple to visualize
- Used by TileMapLayer
- Column-dependent neighbors

### Cube Coordinates (Used for Math)

```
Constraint: q + r + s = 0

Conversion:
Offset → Cube:
  q = col
  r = -col - row + (col - (col & 1)) / 2
  s = row - (col - (col & 1)) / 2

Cube → Offset:
  col = q
  row = s + (q - (q & 1)) / 2
```

**Properties**:
- Mathematically elegant
- Distance = (|q| + |r| + |s|) / 2
- Rotation/reflection easy
- Used for range calculations

---

## Target Calculation Pipeline

### Complete Flow

```
1. Start with ActionConfig
2. Determine base range method:
   - allTilesValid → Every cell
   - useRadiusRange → Cube distance
   - rangePattern → Explicit offsets
   - (default) → Hex neighbors
3. Apply whitelist (add cells)
4. Apply blacklist (remove cells)
5. Filter by grid validity
6. Filter by line of sight
7. Filter by target type
8. Filter by occupation
9. Filter by entity type
10. Add/remove self based on config
11. Return final valid targets
```

### Method: `CalculateValidTargets()`

```csharp
private List<Vector2I> CalculateValidTargets(ActionConfig config)
{
    var validTargets = new HashSet<Vector2I>();
    var playerPos = stateManager.GetPlayerPosition();
    
    // STEP 1: Generate base range
    if (config.AllTilesValid)
    {
        validTargets = GetAllValidGridPositions();
    }
    else if (config.UseRadiusRange)
    {
        validTargets = GenerateRadiusRange(playerPos, config.Range);
    }
    else if (config.RangePattern != null && config.RangePattern.Count > 0)
    {
        validTargets = ApplyPatternFromPosition(playerPos, config.RangePattern);
    }
    else
    {
        validTargets = GetDefaultHexPattern(playerPos);
    }
    
    // STEP 2: Apply whitelist/blacklist
    ApplyWhitelist(validTargets, playerPos, config.Whitelist);
    ApplyBlacklist(validTargets, playerPos, config.Blacklist);
    
    // STEP 3: Filter results
    var finalTargets = new List<Vector2I>();
    foreach (var cell in validTargets)
    {
        if (!IsValidGridPosition(cell)) continue;
        if (config.RequiresLineOfSight && !HasLineOfSight(playerPos, cell)) continue;
        if (!PassesTargetFilters(cell, playerPos, config)) continue;
        
        finalTargets.Add(cell);
    }
    
    // STEP 4: Handle self-targeting
    if (CanTargetSelf(config.TargetType) && !config.ExcludeSelf)
    {
        if (!finalTargets.Contains(playerPos))
            finalTargets.Add(playerPos);
    }
    
    return finalTargets;
}
```

---

## Range Methods

### Method 1: Radius Range (Most Common)

```json
{
  "range": 3,
  "useRadiusRange": true
}
```

**Algorithm**:
```csharp
private HashSet<Vector2I> GenerateRadiusRange(Vector2I origin, int range)
{
    var cells = new HashSet<Vector2I>();
    var originCube = OffsetToCube(origin);
    
    // Iterate through cube space
    for (int q = -range; q <= range; q++)
    {
        for (int r = -range; r <= range; r++)
        {
            for (int s = -range; s <= range; s++)
            {
                // Cube constraint
                if (q + r + s == 0)
                {
                    // Calculate distance
                    int distance = (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(s)) / 2;
                    
                    if (distance > 0 && distance <= range)
                    {
                        var targetCube = new Vector3(
                            originCube.X + q,
                            originCube.Y + r,
                            originCube.Z + s
                        );
                        cells.Add(CubeToOffset(targetCube));
                    }
                }
            }
        }
    }
    
    return cells;
}
```

**Why Cube Coordinates?**
- Perfect circular range in hex space
- Works correctly for odd/even columns
- Mathematical distance accuracy

**Visual Example** (range 2):
```
      2
    2 1 2
  2 1 O 1 2
    2 1 2
      2

O = Origin
Numbers = hex distance
```

### Method 2: Explicit Pattern

```json
{
  "rangePattern": [
    {"x": 1, "y": 0},
    {"x": -1, "y": 0},
    {"x": 0, "y": 1},
    {"x": 0, "y": -1}
  ]
}
```

**Algorithm**:
```csharp
private HashSet<Vector2I> ApplyPatternFromPosition(Vector2I origin, List<PatternCell> pattern)
{
    var cells = new HashSet<Vector2I>();
    var originCube = OffsetToCube(origin);
    
    foreach (var patternCell in pattern)
    {
        var offset = patternCell.ToVector2I();
        var offsetCube = OffsetToCube(offset);
        
        // Add in cube space to handle odd/even columns
        var targetCube = new Vector3(
            originCube.X + offsetCube.X,
            originCube.Y + offsetCube.Y,
            originCube.Z + offsetCube.Z
        );
        
        cells.Add(CubeToOffset(targetCube));
    }
    
    return cells;
}
```

**Use Cases**:
- Cardinal directions only
- Specific irregular shapes
- Asymmetric patterns

### Method 3: All Tiles Valid

```json
{
  "allTilesValid": true,
  "range": 999
}
```

**Algorithm**:
```csharp
var allPositions = stateManager.GetAllValidGridPositions();
foreach (var pos in allPositions)
    validTargets.Add(pos);
```

**Use Cases**:
- Teleport skills
- Global effects
- Map-wide targeting

---

## Target Filtering

### Filter: Target Type

```csharp
private bool CanTargetCell(Vector2I cell, string targetType)
{
    return targetType.ToLower() switch
    {
        "self" => stateManager.IsPlayerCell(cell),
        "ally" => stateManager.IsPlayerCell(cell),
        "enemy" => stateManager.IsEnemyCell(cell),
        "movement" => !stateManager.IsOccupiedCell(cell),
        "area" => true,  // Any cell
        "any" => true,
        _ => true
    };
}
```

**Target Types**:

| Type | Targets |
| ----- | -------- |
| "Self" | Only caster's position |
| "Ally" | Friendly entities |
| "Enemy" | Hostile entities |
| "Movement" | Empty cells |
| "Area" | Any cell (filtered by excludeTypes) |

### Filter: Exclude Self

```json
{
  "excludeSelf": true
}
```

```csharp
if (config.ExcludeSelf && targetCell == playerPos)
    return false;
```

### Filter: Exclude Occupied

```json
{
  "excludeOccupied": true
}
```

```csharp
if (config.ExcludeOccupied && stateManager.IsOccupiedCell(targetCell))
    return false;
```

### Filter: Target Empty Cells Only

```json
{
  "targetEmptyCellsOnly": true
}
```

```csharp
if (config.TargetEmptyCellsOnly && stateManager.IsOccupiedCell(targetCell))
    return false;
```

**Use Case**: Trap placement, ground-targeted abilities

### Filter: Target Self Only

```json
{
  "targetSelfOnly": true
}
```

```csharp
if (config.TargetSelfOnly && targetCell != playerPos)
    return false;
```

**Use Case**: Self-buffs, self-shields

### Filter: Exclude Entity Types

```json
{
  "excludeTypes": ["Player", "Ally"]
}
```

```csharp
var entity = stateManager.GetEntityAt(cell);
if (entity != null && config.ExcludeTypes.Contains(entity.Type.ToString()))
    return false;
```

**Use Case**: Enemy-only damage skills

### Filter: Line of Sight

```json
{
  "requiresLineOfSight": true
}
```

**Algorithm**:
```csharp
private bool HasLineOfSight(Vector2I from, Vector2I to)
{
    var line = HexLineDraw(from, to);
    
    // Check cells between from and to (exclusive)
    for (int i = 1; i < line.Count - 1; i++)
    {
        if (stateManager.IsOccupiedCell(line[i]))
            return false;  // Blocked by entity
    }
    
    return true;
}
```

**Hex Line Drawing**:
```csharp
private List<Vector2I> HexLineDraw(Vector2I from, Vector2I to)
{
    var line = new List<Vector2I>();
    var cubeFrom = OffsetToCube(from);
    var cubeTo = OffsetToCube(to);
    
    int distance = HexCubeDistance(cubeFrom, cubeTo);
    
    for (int i = 0; i <= distance; i++)
    {
        float t = i / (float)distance;
        var cubeLerp = CubeLerp(cubeFrom, cubeTo, t);
        var cubeRounded = CubeRound(cubeLerp);
        line.Add(CubeToOffset(cubeRounded));
    }
    
    return line;
}
```

---

## Whitelist/Blacklist System

### Whitelist (Add Cells)

```json
{
  "whitelist": [
    {
      "type": "coordinate",
      "x": 5,
      "y": 3
    },
    {
      "type": "radius",
      "center": {"x": 0, "y": 0},
      "radius": 2
    }
  ]
}
```

**Process**:
1. Start with base range
2. Add each whitelist pattern
3. Result: Base + whitelist cells

### Blacklist (Remove Cells)

```json
{
  "blacklist": [
    {
      "type": "coordinate",
      "x": 0,
      "y": 0
    },
    {
      "type": "radius",
      "center": {"x": 0, "y": 0},
      "radius": 1
    }
  ]
}
```

**Process**:
1. Start with base range + whitelist
2. Remove each blacklist pattern
3. Result: (Base + whitelist) - blacklist

### Donut Pattern Example

```json
{
  "range": 5,
  "useRadiusRange": true,
  "blacklist": [
    {
      "type": "radius",
      "center": {"x": 0, "y": 0},
      "radius": 1
    }
  ]
}
```

**Visual**:
```
      5 5 5 5 5
    5 4 3 3 3 4 5
  5 4 3 2 2 2 3 4 5
 5 4 3 2 X X 2 3 4 5
5 4 3 2 X O X 2 3 4 5
 5 4 3 2 X X 2 3 4 5
  5 4 3 2 2 2 3 4 5
    5 4 3 3 3 4 5
      5 5 5 5 5

O = Origin (blacklisted)
X = Blacklist radius 1
Numbers = Available targets
```

---

## AOE Calculation

### Method: `CalculateAffectedCells()`

```csharp
public List<Vector2I> CalculateAffectedCells(Vector2I targetCell, ActionConfig config)
{
    var affectedCells = new List<Vector2I>();
    
    // STEP 1: Add target (unless inverse)
    if (!config.InverseAOE)
        affectedCells.Add(targetCell);
    
    // STEP 2: Apply AOE type
    if (config.AoeType == "line")
    {
        var lineCells = CalculateLineAOE(playerPos, targetCell, config);
        affectedCells.AddRange(lineCells);
    }
    else if (config.AoeRadius > 0)
    {
        var radiusCells = GenerateRadiusAOE(targetCell, config.AoeRadius);
        affectedCells.AddRange(radiusCells);
    }
    else if (config.AoePattern != null && config.AoePattern.Count > 0)
    {
        var patternCells = ApplyAOEPattern(targetCell, config.AoePattern);
        affectedCells.AddRange(patternCells);
    }
    
    // STEP 3: Handle excludeOrigin
    if (config.ExcludeOrigin)
        affectedCells.Remove(playerPos);
    
    // STEP 4: Filter invalid cells
    affectedCells = affectedCells
        .Where(c => stateManager.IsValidGridPosition(c))
        .ToList();
    
    return affectedCells;
}
```

### AOE Type: Radius

```json
{
  "aoeRadius": 2
}
```

**Same algorithm as radius range**, centered on target:
```csharp
private HashSet<Vector2I> GenerateRadiusAOE(Vector2I center, int radius)
{
    return GenerateRadiusRange(center, radius);
}
```

**Visual** (radius 2):
```
      2
    2 1 2
  2 1 T 1 2
    2 1 2
      2

T = Target cell
All numbered cells affected
```

### AOE Type: Pattern

```json
{
  "aoePattern": [
    {"x": 0, "y": 0},
    {"x": 1, "y": 0},
    {"x": -1, "y": 0}
  ]
}
```

**Algorithm**:
```csharp
private List<Vector2I> ApplyAOEPattern(Vector2I target, List<PatternCell> pattern)
{
    var cells = new List<Vector2I>();
    var targetCube = OffsetToCube(target);
    
    foreach (var aoeOffset in pattern)
    {
        var offset = aoeOffset.ToVector2I();
        var offsetCube = OffsetToCube(offset);
        
        var aoeCube = new Vector3(
            targetCube.X + offsetCube.X,
            targetCube.Y + offsetCube.Y,
            targetCube.Z + offsetCube.Z
        );
        
        cells.Add(CubeToOffset(aoeCube));
    }
    
    return cells;
}
```

**Visual** (line of 3):
```
    X T X

T = Target
X = AOE pattern hits
```

### AOE Type: Line

```json
{
  "aoeType": "line",
  "aoeWidth": 1,
  "aoeOvershoot": 5
}
```

**Algorithm**:
```csharp
private List<Vector2I> CalculateLineAOE(Vector2I origin, Vector2I target, ActionConfig config)
{
    var cells = new List<Vector2I>();
    
    // Draw line from origin to target
    var linePath = HexLineDraw(origin, target);
    cells.AddRange(linePath);
    
    // Extend beyond target
    if (config.AoeOvershoot > 0)
    {
        var targetCube = OffsetToCube(target);
        var originCube = OffsetToCube(origin);
        
        // Get direction
        var dirCube = new Vector3(
            targetCube.X - originCube.X,
            targetCube.Y - originCube.Y,
            targetCube.Z - originCube.Z
        );
        
        // Normalize
        float maxComponent = Mathf.Max(
            Mathf.Abs(dirCube.X),
            Mathf.Max(Mathf.Abs(dirCube.Y), Mathf.Abs(dirCube.Z))
        );
        
        if (maxComponent > 0)
        {
            dirCube /= maxComponent;
        }
        
        // Extend
        var currentCube = targetCube;
        for (int i = 0; i < config.AoeOvershoot; i++)
        {
            currentCube = new Vector3(
                currentCube.X + dirCube.X,
                currentCube.Y + dirCube.Y,
                currentCube.Z + dirCube.Z
            );
            
            var rounded = CubeRound(currentCube);
            var cell = CubeToOffset(rounded);
            if (!cells.Contains(cell))
                cells.Add(cell);
        }
    }
    
    return cells;
}
```

**Visual** (overshoot 3):
```
O → → → T → → →

O = Origin (caster)
T = Target selected
→ = Line continues 3 more cells
```

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

**Effect**: Target NOT affected, only surrounding cells

**Visual**:
```
      X
    X T X
      X

T = Target (NOT affected)
X = Surrounding cells (affected)
```

---

## Coordinate Conversion

### Offset → Cube

```csharp
private Vector3 OffsetToCube(Vector2I offset)
{
    int col = offset.X;
    int row = offset.Y;
    int x = col;
    int z = row - (col - (col & 1)) / 2;
    int y = -x - z;
    return new Vector3(x, y, z);
}
```

### Cube → Offset

```csharp
private Vector2I CubeToOffset(Vector3 cube)
{
    int col = (int)cube.X;
    int row = (int)cube.Z + (col - (col & 1)) / 2;
    return new Vector2I(col, row);
}
```

### Hex Distance

```csharp
private int HexCubeDistance(Vector3 a, Vector3 b)
{
    return (int)((Mathf.Abs(a.X - b.X) + 
                  Mathf.Abs(a.Y - b.Y) + 
                  Mathf.Abs(a.Z - b.Z)) / 2);
}
```

### Cube Lerp

```csharp
private Vector3 CubeLerp(Vector3 a, Vector3 b, float t)
{
    return new Vector3(
        Mathf.Lerp(a.X, b.X, t),
        Mathf.Lerp(a.Y, b.Y, t),
        Mathf.Lerp(a.Z, b.Z, t)
    );
}
```

### Cube Round

```csharp
private Vector3 CubeRound(Vector3 cube)
{
    int rx = Mathf.RoundToInt(cube.X);
    int ry = Mathf.RoundToInt(cube.Y);
    int rz = Mathf.RoundToInt(cube.Z);
    
    float xDiff = Mathf.Abs(rx - cube.X);
    float yDiff = Mathf.Abs(ry - cube.Y);
    float zDiff = Mathf.Abs(rz - cube.Z);
    
    // Reset coordinate with largest rounding error
    if (xDiff > yDiff && xDiff > zDiff)
        rx = -ry - rz;
    else if (yDiff > zDiff)
        ry = -rx - rz;
    else
        rz = -rx - ry;
    
    return new Vector3(rx, ry, rz);
}
```

---

## NPC Targeting Support

### From Arbitrary Position

```csharp
public List<Vector2I> CalculateValidTargetsFromPosition(
    Vector2I sourcePosition, 
    ActionConfig config)
{
    // Same algorithm as CalculateValidTargets
    // but uses sourcePosition instead of playerPos
    
    var validTargets = new HashSet<Vector2I>();
    
    if (config.UseRadiusRange)
    {
        validTargets = GenerateRadiusRange(sourcePosition, config.Range);
    }
    // ... rest of algorithm
    
    return finalTargets;
}
```

**Use Case**: NPCs calculate their own valid targets

### AOE from Arbitrary Position

```csharp
public List<Vector2I> CalculateAffectedCellsFromPosition(
    Vector2I sourcePosition,
    Vector2I targetCell,
    ActionConfig config)
{
    // Same as CalculateAffectedCells
    // but uses sourcePosition for line AOE
    
    if (config.AoeType == "line")
    {
        lineCells = CalculateLineAOE(sourcePosition, targetCell, config);
    }
    // ... rest of algorithm
    
    return affectedCells;
}
```

---

## Complex Targeting Examples

### Example 1: Sniper Shot

```json
{
  "name": "Sniper Shot",
  "range": 8,
  "useRadiusRange": true,
  "requiresLineOfSight": true,
  "targetType": "Enemy",
  "excludeTypes": ["Ally"],
  "aoePattern": [{"x": 0, "y": 0}]
}
```

**Behavior**:
- Long range (8 hexes)
- Must have line of sight
- Only enemies
- Single target

### Example 2: Chain Lightning

```json
{
  "name": "Chain Lightning",
  "range": 4,
  "useRadiusRange": true,
  "targetType": "Enemy",
  "aoePattern": [
    {"x": 0, "y": 0},
    {"x": 1, "y": 0},
    {"x": -1, "y": 0}
  ],
  "excludeTypes": ["Ally"]
}
```

**Behavior**:
- Target enemy within 4
- Hits target + 2 adjacent
- Won't hit allies

### Example 3: Tactical Retreat

```json
{
  "name": "Tactical Retreat",
  "range": 3,
  "useRadiusRange": true,
  "targetType": "Movement",
  "excludeOccupied": true,
  "blacklist": [
    {
      "type": "radius",
      "center": {"x": 0, "y": 0},
      "radius": 1
    }
  ]
}
```

**Behavior**:
- Move 2-3 cells away
- Can't move adjacent to current position
- Only empty cells

### Example 4: Meteor Strike

```json
{
  "name": "Meteor Strike",
  "range": 6,
  "useRadiusRange": true,
  "targetType": "Area",
  "aoeRadius": 2,
  "requiresLineOfSight": false,
  "excludeTypes": []
}
```

**Behavior**:
- Target any cell within 6
- Radius 2 explosion
- Hits everything (including allies)
- No line of sight needed

---

## Debugging Targeting

### Enable Debug Mode

```json
{
  "debugMode": true
}
```

**Output**:
```
[BattleAction] Calculating targets for 'Fireball' from (0,0)
[BattleAction] Final valid targets: 12
[BattleAction] Total affected cells: 7
```

### Common Issues

**No Valid Targets**:
```
Check:
- range value
- useRadiusRange setting
- excludeTypes list
- targetType value
```

**Wrong Cells Targeted**:
```
Check:
- Offset vs Cube conversion
- Even/odd column handling
- rangePattern coordinates
```

**AOE Not Working**:
```
Check:
- aoeRadius > 0?
- aoePattern not empty?
- aoeType spelling
- inverseAOE setting
```

---

## Performance Tips

### Cache Cube Conversions

```csharp
// ❌ Bad: Convert repeatedly
for (int i = 0; i < 100; i++)
{
    var cube = OffsetToCube(cell);
    // use cube
}

// ✅ Good: Convert once
var cube = OffsetToCube(cell);
for (int i = 0; i < 100; i++)
{
    // use cube
}
```

### Use HashSet for Lookup

```csharp
// ✅ Good: O(1) lookup
var validCells = new HashSet<Vector2I>(validTargets);
if (validCells.Contains(cell))
{
    // Valid target
}
```

### Minimize Grid Queries

```csharp
// ❌ Bad: Query for every cell
foreach (var cell in cells)
{
    if (stateManager.IsValidGridPosition(cell))
        // ...
}

// ✅ Good: Query once, filter
var validGridCells = cells
    .Where(c => stateManager.IsValidGridPosition(c))
    .ToList();
```

---

## Related Documentation

- [[Action System]] - How targeting integrates with actions
- [[Hex Grid System]] - Grid mathematics
- [[Component Reference#BattleActionHandler]] - Implementation details
- [[Configuration Files#ActionConfig]] - JSON targeting properties