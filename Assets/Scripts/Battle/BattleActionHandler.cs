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
                validTargets.Add(pos - playerPos);
        }
        else if (config.UseRadiusRange)
        {
            foreach (var offset in GenerateRadiusPattern(config.Range))
                validTargets.Add(offset);
        }
        else if (config.RangePattern != null && config.RangePattern.Count > 0)
        {
            foreach (var pattern in config.RangePattern)
                validTargets.Add(pattern.ToVector2I());
        }
        else
        {
            foreach (var offset in GetDefaultHexPattern())
                validTargets.Add(offset);
        }
        
        if (config.Whitelist != null)
        {
            foreach (var pattern in config.Whitelist)
                foreach (var tile in ExpandPattern(pattern))
                    validTargets.Add(tile);
        }
        
        if (config.Blacklist != null)
        {
            foreach (var pattern in config.Blacklist)
                foreach (var tile in ExpandPattern(pattern))
                    validTargets.Remove(tile);
        }
        
        var finalTargets = new List<Vector2I>();
        foreach (var offset in validTargets)
        {
            var adjustedOffset = AdjustOffsetForHexGrid(offset, playerPos);
            var targetCell = playerPos + adjustedOffset;
            
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
            foreach (var tile in GenerateRadiusPattern(pattern.Radius))
                tiles.Add(centerOffset + tile);
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
            foreach (var offset in GenerateRadiusPattern(config.AoeRadius))
            {
                var adjustedOffset = AdjustOffsetForHexGrid(offset, targetCell);
                var aoeCell = targetCell + adjustedOffset;
                if (stateManager.IsValidGridPosition(aoeCell) && !affectedCells.Contains(aoeCell))
                    affectedCells.Add(aoeCell);
            }
        }
        else if (config.AoePattern != null && config.AoePattern.Count > 0)
        {
            foreach (var aoeOffset in config.AoePattern)
            {
                var adjustedOffset = AdjustOffsetForHexGrid(aoeOffset.ToVector2I(), targetCell);
                var aoeCell = targetCell + adjustedOffset;
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
        Vector2I direction = GetHexDirection(origin, target);
        
        if (direction == Vector2I.Zero)
        {
            cells.Add(origin);
            return cells;
        }
        
        int totalSteps = 1 + overshoot;
        Vector2I current = origin;
        
        for (int i = 0; i <= totalSteps; i++)
        {
            cells.Add(current);
            current = current + AdjustOffsetForHexGrid(direction, current);
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
        int searchRange = radius + 2;
        
        for (int x = -searchRange; x <= searchRange; x++)
        {
            for (int y = -searchRange; y <= searchRange; y++)
            {
                if (x == 0 && y == 0) continue;
                int hexDist = CalculateHexDistance(new Vector2I(0, 0), new Vector2I(x, y));
                if (hexDist <= radius)
                    pattern.Add(new Vector2I(x, y));
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

    private Vector2I AdjustOffsetForHexGrid(Vector2I offset, Vector2I position)
    {
        if (position.X % 2 != 0 && offset.X != 0)
            return new Vector2I(offset.X, offset.Y + 1);
        return offset;
        //Possibly redundant, this only fixes aoe previews. the valid tile calculations are correct.
        //This breaks line AOE calculations, specifically it displaces the upper left and right hexes 1 step below.
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
    
    #region Debug
    
    private void DebugLog(string message)
    {
        if (debugEnabled)
            GD.Print($"[BattleAction] {message}");
    }
    
    #endregion
}