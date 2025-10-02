// BattleActionHandler.cs - Processes actions and targeting
using Godot;
using System.Collections.Generic;
using System.Linq;
using CustomJsonSystem;

public class BattleActionHandler
{
    #region Dependencies
    
    private BattleStateManager stateManager;
    private BattleConfigurationLoader configLoader;
    
    #endregion
    
    #region State
    
    private string currentActionType = "";
    private string selectedActionOption = "";
    private ActionConfig currentActionConfig;
    private List<Vector2I> currentValidTargets = new();
    private bool debugEnabled = false;
    
    #endregion
    
    #region Initialization
    
    public void Initialize(BattleStateManager stateManager, BattleConfigurationLoader configLoader)
    {
        this.stateManager = stateManager;
        this.configLoader = configLoader;
        GD.Print("[BattleAction] Action handler initialized");
    }
    
    public void SetDebugEnabled(bool enabled)
    {
        debugEnabled = enabled;
        DebugLog($"Debug enabled: {enabled}");
    }
    
    #endregion
    
    #region Action Flow
    
    public void ProcessActionRequest(string actionType, string actionName)
    {
        currentActionType = actionType;
        selectedActionOption = "";
        currentActionConfig = null;
        currentValidTargets.Clear();
        DebugLog($"Action request: {actionType}");
    }
    
    public void ProcessSubmenuSelection(string actionName)
    {
        selectedActionOption = actionName;
        currentActionConfig = configLoader.GetActionConfig(actionName);
        
        if (currentActionConfig != null)
        {
            currentValidTargets = CalculateValidTargets(currentActionConfig);
            DebugLog($"Action selected: {actionName}, {currentValidTargets.Count} valid targets");
        }
        else
        {
            GD.PrintErr($"[BattleAction] Action config not found for: {actionName}");
        }
    }
    
    public void ProcessTargetSelection(Vector2I targetCell)
    {
        if (currentActionConfig != null)
        {
            ExecuteAction(targetCell, currentActionConfig);
        }
        else
        {
            GD.PrintErr("[BattleAction] Cannot execute - no current action config");
        }
        ClearCurrentAction();
    }
    
    public void CancelCurrentAction()
    {
        DebugLog("Current action cancelled");
        ClearCurrentAction();
    }
    
    private void ClearCurrentAction()
    {
        currentActionType = "";
        selectedActionOption = "";
        currentActionConfig = null;
        currentValidTargets.Clear();
    }
    
    #endregion
    
    #region Action Execution
    
    private void ExecuteAction(Vector2I targetCell, ActionConfig config)
    {
        DebugLog($"Executing {currentActionType}: {config.Name} on {targetCell}");
        
        if (string.IsNullOrEmpty(currentActionType))
        {
            GD.PrintErr("[BattleAction] ERROR: currentActionType is empty!");
            return;
        }
        
        switch (currentActionType)
        {
            case "move": ExecuteMoveAction(targetCell, config); break;
            case "skill": ExecuteSkillAction(targetCell, config); break;
            case "item": ExecuteItemAction(targetCell, config); break;
            case "talk": ExecuteTalkAction(targetCell, config); break;
            default: GD.PrintErr($"[BattleAction] Unknown action type: '{currentActionType}'"); break;
        }
    }
    
    private void ExecuteMoveAction(Vector2I targetCell, ActionConfig config)
    {
        if (!currentValidTargets.Contains(targetCell))
        {
            GD.PrintErr($"[BattleAction] Invalid move target: {targetCell}");
            return;
        }
        
        stateManager.MovePlayer(targetCell);
        DebugLog($"Move '{config.Name}' executed, cost: {config.Cost}");
    }
    
    private void ExecuteSkillAction(Vector2I targetCell, ActionConfig config)
    {
        DebugLog($"Skill '{config.Name}' targeting {targetCell}");
        var affectedCells = CalculateAffectedCells(targetCell, config);
        
        foreach (var cell in affectedCells)
        {
            if (config.Damage > 0)
            {
                stateManager.ApplyDamageToEntity(cell, config.Damage);
                DebugLog($"Dealt {config.Damage} damage to {cell}");
            }
            
            if (!string.IsNullOrEmpty(config.StatusEffect))
            {
                stateManager.ApplyStatusEffectToEntity(cell, config.StatusEffect);
                DebugLog($"Applied {config.StatusEffect} to {cell}");
            }
        }
        
        DebugLog($"Skill cost: {config.Cost}");
    }
    
    private void ExecuteItemAction(Vector2I targetCell, ActionConfig config)
    {
        DebugLog($"Item '{config.Name}' used on {targetCell}");
        var affectedCells = CalculateAffectedCells(targetCell, config);
        
        foreach (var cell in affectedCells)
        {
            if (config.HealAmount > 0)
            {
                stateManager.ApplyHealingToEntity(cell, config.HealAmount);
                DebugLog($"Healed {config.HealAmount} at {cell}");
            }
            
            if (config.Damage > 0)
            {
                stateManager.ApplyDamageToEntity(cell, config.Damage);
                DebugLog($"Dealt {config.Damage} damage to {cell}");
            }
            
            if (!string.IsNullOrEmpty(config.StatusEffect))
            {
                stateManager.ApplyStatusEffectToEntity(cell, config.StatusEffect);
            }
        }
        
        if (config.UsesRemaining > 0)
        {
            config.UsesRemaining--;
            DebugLog($"{config.Name} uses remaining: {config.UsesRemaining}");
            if (config.UsesRemaining == 0) GD.Print($"[BattleAction] {config.Name} depleted!");
        }
    }
    
    private void ExecuteTalkAction(Vector2I targetCell, ActionConfig config)
    {
        if (!string.IsNullOrEmpty(config.Dialogue))
        {
            GD.Print($"[BattleAction] Player says: \"{config.Dialogue}\"");
        }
        
        if (stateManager.IsEnemyCell(targetCell) || stateManager.IsPlayerCell(targetCell))
        {
            if (config.FriendshipChange != 0)
                DebugLog($"Friendship: {(config.FriendshipChange > 0 ? "+" : "")}{config.FriendshipChange}");
            if (config.ReputationChange != 0)
                DebugLog($"Reputation: {(config.ReputationChange > 0 ? "+" : "")}{config.ReputationChange}");
        }
    }
    
    #endregion
    
    #region Target Validation
    
    private List<Vector2I> CalculateValidTargets(ActionConfig config)
    {
        var validTargets = new HashSet<Vector2I>();
        var playerPos = stateManager.GetPlayerPosition();
        
        DebugLog($"Calculating targets for '{config.Name}' from {playerPos}");
        
        if (config.AllTilesValid)
        {
            var allPositions = stateManager.GetAllValidGridPositions();
            foreach (var pos in allPositions)
                validTargets.Add(pos);
        }
        else if (config.UseRadiusRange)
        {
            // Use proper cube coordinate math for radius
            var playerCube = OffsetToCube(playerPos);
            for (int q = -config.Range; q <= config.Range; q++)
            {
                for (int r = -config.Range; r <= config.Range; r++)
                {
                    for (int s = -config.Range; s <= config.Range; s++)
                    {
                        if (q + r + s == 0)
                        {
                            int distance = (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(s)) / 2;
                            if (distance > 0 && distance <= config.Range)
                            {
                                var targetCube = new Vector3(playerCube.X + q, playerCube.Y + r, playerCube.Z + s);
                                var targetCell = CubeToOffset(targetCube);
                                validTargets.Add(targetCell);
                            }
                        }
                    }
                }
            }
        }
        else if (config.RangePattern != null && config.RangePattern.Count > 0)
        {
            // Explicit patterns - use cube coordinate conversion for odd/even column compatibility
            var playerCube = OffsetToCube(playerPos);
            
            foreach (var pattern in config.RangePattern)
            {
                var offset = pattern.ToVector2I();
                
                // Convert offset to cube, add to player cube, convert back
                var offsetCube = OffsetToCube(offset);
                var targetCube = new Vector3(
                    playerCube.X + offsetCube.X,
                    playerCube.Y + offsetCube.Y,
                    playerCube.Z + offsetCube.Z
                );
                var targetCell = CubeToOffset(targetCube);
                validTargets.Add(targetCell);
            }
        }
        else
        {
            // Default hex pattern - direct neighbors
            foreach (var offset in GetDefaultHexPattern())
            {
                var targetCell = playerPos + offset;
                validTargets.Add(targetCell);
            }
        }
        
        if (config.Whitelist != null)
        {
            foreach (var pattern in config.Whitelist)
                foreach (var offset in ExpandPattern(pattern))
                {
                    var targetCell = playerPos + offset;
                    validTargets.Add(targetCell);
                }
        }
        
        if (config.Blacklist != null)
        {
            foreach (var pattern in config.Blacklist)
                foreach (var offset in ExpandPattern(pattern))
                {
                    var targetCell = playerPos + offset;
                    validTargets.Remove(targetCell);
                }
        }
        
        var finalTargets = new List<Vector2I>();
        foreach (var targetCell in validTargets)
        {
            if (!stateManager.IsValidGridPosition(targetCell)) continue;
            if (config.RequiresLineOfSight && !HasLineOfSight(playerPos, targetCell)) continue;
            if (!PassesTargetFilters(targetCell, playerPos, config)) continue;
            
            finalTargets.Add(targetCell);
        }
        
        if (CanTargetSelf(config.TargetType) && !config.ExcludeSelf)
        {
            if (!finalTargets.Contains(playerPos))
                finalTargets.Add(playerPos);
        }
        
        DebugLog($"Final valid targets: {finalTargets.Count}");
        return finalTargets;
    }
    
    // Removed - no longer needed
    // private Vector2I ApplyHexOffset(Vector2I origin, Vector2I offset)
    
    private List<Vector2I> ExpandPattern(CustomJsonSystem.TargetPattern pattern)
    {
        var tiles = new List<Vector2I>();
        
        if (pattern.Type == "coordinate")
        {
            tiles.Add(new Vector2I(pattern.X, pattern.Y));
        }
        else if (pattern.Type == "radius")
        {
            var centerOffset = new Vector2I(pattern.Center.X, pattern.Center.Y);
            
            // Generate radius pattern in cube coordinates from center
            for (int q = -pattern.Radius; q <= pattern.Radius; q++)
            {
                for (int r = -pattern.Radius; r <= pattern.Radius; r++)
                {
                    for (int s = -pattern.Radius; s <= pattern.Radius; s++)
                    {
                        if (q + r + s == 0)
                        {
                            int distance = (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(s)) / 2;
                            if (distance > 0 && distance <= pattern.Radius)
                            {
                                // Convert to offset
                                int col = q;
                                int row = s + (q - (q & 1)) / 2;
                                tiles.Add(centerOffset + new Vector2I(col, row));
                            }
                        }
                    }
                }
            }
            
            // Include center if radius > 0
            if (pattern.Radius > 0)
                tiles.Add(centerOffset);
        }
        
        return tiles;
    }
    
    private bool PassesTargetFilters(Vector2I targetCell, Vector2I playerPos, ActionConfig config)
    {
        if (!CanTargetCell(targetCell, config.TargetType)) return false;
        if (config.ExcludeSelf && targetCell == playerPos) return false;
        if (config.ExcludeOccupied && stateManager.IsOccupiedCell(targetCell)) return false;
        if (config.TargetEmptyCellsOnly && stateManager.IsOccupiedCell(targetCell)) return false;
        if (config.TargetSelfOnly && targetCell != playerPos) return false;
        return true;
    }
    
    #endregion
    
    #region AOE Calculation
    
    public List<Vector2I> CalculateAffectedCells(Vector2I targetCell, ActionConfig config)
    {
        var affectedCells = new List<Vector2I>();
        var playerPos = stateManager.GetPlayerPosition();
        
        if (!config.InverseAOE)
            affectedCells.Add(targetCell);
        
        if (config.AoeType == "line")
        {
            var lineCells = CalculateLineAOE(playerPos, targetCell, config.AoeWidth, config.AoeOvershoot);
            if (config.ExcludeOrigin)
                lineCells.Remove(playerPos);
            
            foreach (var cell in lineCells)
                if (stateManager.IsValidGridPosition(cell) && !affectedCells.Contains(cell))
                    affectedCells.Add(cell);
        }
        else if (config.AoeRadius > 0)
        {
            // Use exact same cube coordinate approach as valid targets
            var targetCube = OffsetToCube(targetCell);
            for (int q = -config.AoeRadius; q <= config.AoeRadius; q++)
            {
                for (int r = -config.AoeRadius; r <= config.AoeRadius; r++)
                {
                    for (int s = -config.AoeRadius; s <= config.AoeRadius; s++)
                    {
                        if (q + r + s == 0)
                        {
                            int distance = (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(s)) / 2;
                            if (distance > 0 && distance <= config.AoeRadius)
                            {
                                var aoeCube = new Vector3(targetCube.X + q, targetCube.Y + r, targetCube.Z + s);
                                var aoeCell = CubeToOffset(aoeCube);
                                if (stateManager.IsValidGridPosition(aoeCell) && !affectedCells.Contains(aoeCell))
                                    affectedCells.Add(aoeCell);
                            }
                        }
                    }
                }
            }
        }
        else if (config.AoePattern != null && config.AoePattern.Count > 0)
        {
            // Explicit AOE patterns - convert each offset using cube coordinates
            var targetCube = OffsetToCube(targetCell);
            
            foreach (var aoeOffset in config.AoePattern)
            {
                var offset = aoeOffset.ToVector2I();
                
                // Convert offset to cube, add to target cube, convert back
                var offsetCube = OffsetToCube(offset);
                var aoeCube = new Vector3(
                    targetCube.X + offsetCube.X,
                    targetCube.Y + offsetCube.Y,
                    targetCube.Z + offsetCube.Z
                );
                var aoeCell = CubeToOffset(aoeCube);
                
                if (stateManager.IsValidGridPosition(aoeCell) && !affectedCells.Contains(aoeCell))
                    affectedCells.Add(aoeCell);
            }
        }
        
        DebugLog($"Total affected cells: {affectedCells.Count}");
        return affectedCells;
    }
    
    private List<Vector2I> CalculateLineAOE(Vector2I origin, Vector2I target, int width, int overshoot = 0)
    {
        var cells = new List<Vector2I>();
        
        // Get the line from origin to target using proper hex line drawing
        var linePath = HexLineDraw(origin, target);
        
        if (linePath.Count <= 1)
        {
            cells.Add(origin);
            return cells;
        }
        
        // Get direction in cube space for accurate extension
        var originCube = OffsetToCube(origin);
        var targetCube = OffsetToCube(target);
        var dirCube = new Vector3(
            targetCube.X - originCube.X,
            targetCube.Y - originCube.Y,
            targetCube.Z - originCube.Z
        );
        
        // Normalize direction in cube space
        float maxComponent = Mathf.Max(Mathf.Abs(dirCube.X), Mathf.Max(Mathf.Abs(dirCube.Y), Mathf.Abs(dirCube.Z)));
        if (maxComponent > 0)
        {
            dirCube = new Vector3(
                dirCube.X / maxComponent,
                dirCube.Y / maxComponent,
                dirCube.Z / maxComponent
            );
        }
        
        // Add all cells in the line path
        foreach (var cell in linePath)
        {
            cells.Add(cell);
        }
        
        // Extend beyond target with overshoot
        if (overshoot > 0)
        {
            var currentCube = targetCube;
            for (int i = 0; i < overshoot; i++)
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
    
    private Vector2I GetHexDirection(Vector2I from, Vector2I to)
    {
        Vector2I offset = to - from;
        var hexDirections = GetDefaultHexPattern();
        
        foreach (var dir in hexDirections)
            if (dir == offset)
                return dir;
        
        var cubeTo = OffsetToCube(to);
        var cubeFrom = OffsetToCube(from);
        var cubeDiff = new Vector3(cubeTo.X - cubeFrom.X, cubeTo.Y - cubeFrom.Y, cubeTo.Z - cubeFrom.Z);
        
        float absX = Mathf.Abs(cubeDiff.X);
        float absY = Mathf.Abs(cubeDiff.Y);
        float absZ = Mathf.Abs(cubeDiff.Z);
        float maxVal = Mathf.Max(absX, Mathf.Max(absY, absZ));
        
        if (absX == maxVal)
            return cubeDiff.X > 0 ? new Vector2I(1, 0) : new Vector2I(-1, 0);
        else if (absY == maxVal)
            return cubeDiff.Y > 0 ? new Vector2I(0, 1) : new Vector2I(0, -1);
        else
            return cubeDiff.Z > 0 ? new Vector2I(1, -1) : new Vector2I(-1, -1);
    }
    
    #endregion
    
    #region Hex Grid Math
    
    private bool HasLineOfSight(Vector2I from, Vector2I to)
    {
        var line = HexLineDraw(from, to);
        for (int i = 1; i < line.Count - 1; i++)
            if (stateManager.IsOccupiedCell(line[i]))
                return false;
        return true;
    }
    
    private List<Vector2I> HexLineDraw(Vector2I from, Vector2I to)
    {
        var line = new List<Vector2I>();
        var cubeFrom = OffsetToCube(from);
        var cubeTo = OffsetToCube(to);
        int distance = HexCubeDistance(cubeFrom, cubeTo);
        
        if (distance == 0)
        {
            line.Add(from);
            return line;
        }
        
        for (int i = 0; i <= distance; i++)
        {
            float t = i / (float)distance;
            var cubeLerp = CubeLerp(cubeFrom, cubeTo, t);
            var cubeRounded = CubeRound(cubeLerp);
            line.Add(CubeToOffset(cubeRounded));
        }
        
        return line;
    }
    
    private Vector3 OffsetToCube(Vector2I offset)
    {
        int col = offset.X;
        int row = offset.Y;
        int x = col;
        int z = row - (col - (col & 1)) / 2;
        int y = -x - z;
        return new Vector3(x, y, z);
    }
    
    private Vector2I CubeToOffset(Vector3 cube)
    {
        int col = (int)cube.X;
        int row = (int)cube.Z + (col - (col & 1)) / 2;
        return new Vector2I(col, row);
    }
    
    private int HexCubeDistance(Vector3 a, Vector3 b)
    {
        return (int)((Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y) + Mathf.Abs(a.Z - b.Z)) / 2);
    }
    
    private Vector3 CubeLerp(Vector3 a, Vector3 b, float t)
    {
        return new Vector3(
            Mathf.Lerp(a.X, b.X, t),
            Mathf.Lerp(a.Y, b.Y, t),
            Mathf.Lerp(a.Z, b.Z, t)
        );
    }
    
    private Vector3 CubeRound(Vector3 cube)
    {
        int rx = Mathf.RoundToInt(cube.X);
        int ry = Mathf.RoundToInt(cube.Y);
        int rz = Mathf.RoundToInt(cube.Z);
        
        float xDiff = Mathf.Abs(rx - cube.X);
        float yDiff = Mathf.Abs(ry - cube.Y);
        float zDiff = Mathf.Abs(rz - cube.Z);
        
        if (xDiff > yDiff && xDiff > zDiff)
            rx = -ry - rz;
        else if (yDiff > zDiff)
            ry = -rx - rz;
        else
            rz = -rx - ry;
        
        return new Vector3(rx, ry, rz);
    }
    
    private List<Vector2I> GenerateRadiusPattern(int radius)
    {
        var pattern = new List<Vector2I>();
        
        // Use cube coordinates for accurate hex distance
        for (int q = -radius; q <= radius; q++)
        {
            for (int r = -radius; r <= radius; r++)
            {
                for (int s = -radius; s <= radius; s++)
                {
                    if (q + r + s == 0)
                    {
                        int cubeDistance = (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(s)) / 2;
                        if (cubeDistance > 0 && cubeDistance <= radius)
                        {
                            // Convert cube to offset coordinates
                            int col = q;
                            int row = s + (q - (q & 1)) / 2;
                            pattern.Add(new Vector2I(col, row));
                        }
                    }
                }
            }
        }
        
        return pattern;
    }
    
    private int CalculateHexDistance(Vector2I from, Vector2I to)
    {
        int q1 = from.X;
        int r1 = from.Y - (from.X - (from.X & 1)) / 2;
        int q2 = to.X;
        int r2 = to.Y - (to.X - (to.X & 1)) / 2;
        int dq = q2 - q1;
        int dr = r2 - r1;
        return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;
    }
    
    private List<Vector2I> GetDefaultHexPattern()
    {
        return new List<Vector2I>
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, -1), new(-1, -1)
        };
    }
    
    private bool CanTargetCell(Vector2I cell, string targetType)
    {
        return targetType.ToLower() switch
        {
            "self" => stateManager.IsPlayerCell(cell),
            "ally" => stateManager.IsPlayerCell(cell),
            "enemy" => stateManager.IsEnemyCell(cell),
            "movement" => !stateManager.IsOccupiedCell(cell),
            "inversetargeting" => !stateManager.IsOccupiedCell(cell),
            "area" or "any" => true,
            _ => true
        };
    }
    
    private bool CanTargetSelf(string targetType)
    {
        return targetType.ToLower() is "self" or "ally";
    }
    
    #endregion
    
    #region Public Access
    
    public List<Vector2I> GetValidTargetsForCurrentAction() => new List<Vector2I>(currentValidTargets);
    public string GetCurrentActionType() => currentActionType;
    public string GetSelectedActionOption() => selectedActionOption;
    public ActionConfig GetCurrentActionConfig() => currentActionConfig;

    #endregion
    
    #region NPC Support Methods

    /// <summary>
    /// Calculate valid targets from an arbitrary position (not just player position)
    /// Used by NPC AI to determine where they can act
    /// </summary>
    public List<Vector2I> CalculateValidTargetsFromPosition(Vector2I sourcePosition, ActionConfig config)
    {
        var validTargets = new HashSet<Vector2I>();
        
        DebugLog($"Calculating targets for '{config.Name}' from {sourcePosition}");
        
        if (config.AllTilesValid)
        {
            var allPositions = stateManager.GetAllValidGridPositions();
            foreach (var pos in allPositions)
                validTargets.Add(pos);
        }
        else if (config.UseRadiusRange)
        {
            var sourceCube = OffsetToCube(sourcePosition);
            for (int q = -config.Range; q <= config.Range; q++)
            {
                for (int r = -config.Range; r <= config.Range; r++)
                {
                    for (int s = -config.Range; s <= config.Range; s++)
                    {
                        if (q + r + s == 0)
                        {
                            int distance = (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(s)) / 2;
                            if (distance > 0 && distance <= config.Range)
                            {
                                var targetCube = new Vector3(sourceCube.X + q, sourceCube.Y + r, sourceCube.Z + s);
                                var targetCell = CubeToOffset(targetCube);
                                validTargets.Add(targetCell);
                            }
                        }
                    }
                }
            }
        }
        else if (config.RangePattern != null && config.RangePattern.Count > 0)
        {
            var sourceCube = OffsetToCube(sourcePosition);
            
            foreach (var pattern in config.RangePattern)
            {
                var offset = pattern.ToVector2I();
                var offsetCube = OffsetToCube(offset);
                var targetCube = new Vector3(
                    sourceCube.X + offsetCube.X,
                    sourceCube.Y + offsetCube.Y,
                    sourceCube.Z + offsetCube.Z
                );
                var targetCell = CubeToOffset(targetCube);
                validTargets.Add(targetCell);
            }
        }
        else
        {
            foreach (var offset in GetDefaultHexPattern())
            {
                var targetCell = sourcePosition + offset;
                validTargets.Add(targetCell);
            }
        }
        
        if (config.Whitelist != null)
        {
            foreach (var pattern in config.Whitelist)
                foreach (var offset in ExpandPattern(pattern))
                {
                    var targetCell = sourcePosition + offset;
                    validTargets.Add(targetCell);
                }
        }
        
        if (config.Blacklist != null)
        {
            foreach (var pattern in config.Blacklist)
                foreach (var offset in ExpandPattern(pattern))
                {
                    var targetCell = sourcePosition + offset;
                    validTargets.Remove(targetCell);
                }
        }
        
        var finalTargets = new List<Vector2I>();
        foreach (var targetCell in validTargets)
        {
            if (!stateManager.IsValidGridPosition(targetCell)) continue;
            if (config.RequiresLineOfSight && !HasLineOfSight(sourcePosition, targetCell)) continue;
            if (!PassesTargetFiltersFromPosition(targetCell, sourcePosition, config)) continue;
            
            finalTargets.Add(targetCell);
        }
        
        if (CanTargetSelf(config.TargetType) && !config.ExcludeSelf)
        {
            if (!finalTargets.Contains(sourcePosition))
                finalTargets.Add(sourcePosition);
        }
        
        DebugLog($"Final valid targets from {sourcePosition}: {finalTargets.Count}");
        return finalTargets;
    }

    /// <summary>
    /// Calculate AOE affected cells from arbitrary source and target
    /// </summary>
    public List<Vector2I> CalculateAffectedCellsFromPosition(
        Vector2I sourcePosition,
        Vector2I targetCell,
        ActionConfig config)
    {
        var affectedCells = new List<Vector2I>();
        
        if (!config.InverseAOE)
            affectedCells.Add(targetCell);
        
        if (config.AoeType == "line")
        {
            var lineCells = CalculateLineAOE(sourcePosition, targetCell, config.AoeWidth, config.AoeOvershoot);
            if (config.ExcludeOrigin)
                lineCells.Remove(sourcePosition);
            
            foreach (var cell in lineCells)
                if (stateManager.IsValidGridPosition(cell) && !affectedCells.Contains(cell))
                    affectedCells.Add(cell);
        }
        else if (config.AoeRadius > 0)
        {
            var targetCube = OffsetToCube(targetCell);
            for (int q = -config.AoeRadius; q <= config.AoeRadius; q++)
            {
                for (int r = -config.AoeRadius; r <= config.AoeRadius; r++)
                {
                    for (int s = -config.AoeRadius; s <= config.AoeRadius; s++)
                    {
                        if (q + r + s == 0)
                        {
                            int distance = (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(s)) / 2;
                            if (distance > 0 && distance <= config.AoeRadius)
                            {
                                var aoeCube = new Vector3(targetCube.X + q, targetCube.Y + r, targetCube.Z + s);
                                var aoeCell = CubeToOffset(aoeCube);
                                if (stateManager.IsValidGridPosition(aoeCell) && !affectedCells.Contains(aoeCell))
                                    affectedCells.Add(aoeCell);
                            }
                        }
                    }
                }
            }
        }
        else if (config.AoePattern != null && config.AoePattern.Count > 0)
        {
            var targetCube = OffsetToCube(targetCell);
            
            foreach (var aoeOffset in config.AoePattern)
            {
                var offset = aoeOffset.ToVector2I();
                var offsetCube = OffsetToCube(offset);
                var aoeCube = new Vector3(
                    targetCube.X + offsetCube.X,
                    targetCube.Y + offsetCube.Y,
                    targetCube.Z + offsetCube.Z
                );
                var aoeCell = CubeToOffset(aoeCube);
                
                if (stateManager.IsValidGridPosition(aoeCell) && !affectedCells.Contains(aoeCell))
                    affectedCells.Add(aoeCell);
            }
        }
        
        DebugLog($"Total affected cells from {sourcePosition} targeting {targetCell}: {affectedCells.Count}");
        return affectedCells;
    }

    private bool PassesTargetFiltersFromPosition(Vector2I targetCell, Vector2I sourcePosition, ActionConfig config)
    {
        if (!CanTargetCellFromPosition(targetCell, sourcePosition, config.TargetType)) return false;
        if (config.ExcludeSelf && targetCell == sourcePosition) return false;
        if (config.ExcludeOccupied && stateManager.IsOccupiedCell(targetCell)) return false;
        if (config.TargetEmptyCellsOnly && stateManager.IsOccupiedCell(targetCell)) return false;
        if (config.TargetSelfOnly && targetCell != sourcePosition) return false;
        return true;
    }

    private bool CanTargetCellFromPosition(Vector2I cell, Vector2I sourcePosition, string targetType)
    {
        var entity = stateManager.GetEntityAt(cell);
        var sourceEntity = stateManager.GetEntityAt(sourcePosition);
        
        if (sourceEntity == null) return true; // Fallback for undefined source
        
        return targetType.ToLower() switch
        {
            "self" => cell == sourcePosition,
            "ally" => entity != null && sourceEntity.IsAllyOf(entity),
            "enemy" => entity != null && sourceEntity.IsEnemyOf(entity),
            "movement" => !stateManager.IsOccupiedCell(cell),
            "area" or "any" => true,
            _ => true
        };
    }

    #endregion
    
    #region Debug

    private void DebugLog(string message)
    {
        if (debugEnabled)
            GD.Print($"[BattleAction] {message}");
    }
    
    #endregion
}