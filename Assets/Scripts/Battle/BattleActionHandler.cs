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
    
    #region Action State
    
    private string currentActionType = "";
    private string selectedActionOption = "";
    private ActionConfig currentActionConfig;
    private List<Vector2I> currentValidTargets = new();
    
    #endregion
    
    #region Initialization
    
    public void Initialize(BattleStateManager stateManager, BattleConfigurationLoader configLoader)
    {
        this.stateManager = stateManager;
        this.configLoader = configLoader;
        
        GD.Print("[BattleAction] Action handler initialized");
    }
    
    #endregion
    
    #region Action Processing
    
    public void ProcessActionRequest(string actionType, string actionName)
    {
        currentActionType = actionType;
        // Don't clear the action here - we need to keep the action type
        selectedActionOption = "";
        currentActionConfig = null;
        currentValidTargets.Clear();
        
        GD.Print($"[BattleAction] Action request: {actionType}");
    }
    
    public void ProcessSubmenuSelection(string actionName)
    {
        selectedActionOption = actionName;
        currentActionConfig = configLoader.GetActionConfig(actionName);
        
        if (currentActionConfig != null)
        {
            currentValidTargets = CalculateValidTargets(currentActionConfig);
            GD.Print($"[BattleAction] Action selected: {actionName}, {currentValidTargets.Count} valid targets");
            GD.Print($"[BattleAction] Current action type: '{currentActionType}'");
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
        GD.Print("[BattleAction] Current action cancelled");
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
    
    private void ExecuteAction(Vector2I targetCell, ActionConfig actionConfig)
    {
        GD.Print($"[BattleAction] Executing {currentActionType}: {actionConfig.Name} on {targetCell}");
        
        // Debug: Check if currentActionType is empty
        if (string.IsNullOrEmpty(currentActionType))
        {
            GD.PrintErr("[BattleAction] ERROR: currentActionType is empty! Cannot determine action type.");
            return;
        }
        
        switch (currentActionType)
        {
            case "move":
                ExecuteMoveAction(targetCell, actionConfig);
                break;
            case "skill":
                ExecuteSkillAction(targetCell, actionConfig);
                break;
            case "item":
                ExecuteItemAction(targetCell, actionConfig);
                break;
            case "talk":
                ExecuteTalkAction(targetCell, actionConfig);
                break;
            default:
                GD.PrintErr($"[BattleAction] Unknown action type: '{currentActionType}'");
                break;
        }
    }
    
    private void ExecuteMoveAction(Vector2I targetCell, ActionConfig actionConfig)
    {
        if (!currentValidTargets.Contains(targetCell))
        {
            GD.PrintErr($"[BattleAction] Invalid move target: {targetCell}");
            return;
        }
        
        GD.Print($"[BattleAction] Moving player to {targetCell}");
        stateManager.MovePlayer(targetCell);
        GD.Print($"[BattleAction] Move '{actionConfig.Name}' executed successfully");
        
        // Apply movement costs and effects
        if (actionConfig.Cost > 0)
        {
            GD.Print($"[BattleAction] Movement cost: {actionConfig.Cost}");
            // TODO: Integrate with BattleLogic money system
        }
        
        // TODO: Integrate with BattleLogic MoveAction class
    }
    
    private void ExecuteSkillAction(Vector2I targetCell, ActionConfig actionConfig)
    {
        GD.Print($"[BattleAction] Skill '{actionConfig.Name}' targeting {targetCell}");
        GD.Print($"[BattleAction] Description: {actionConfig.Description}");
        
        // Calculate affected cells (target + AOE)
        var affectedCells = CalculateAffectedCells(targetCell, actionConfig);
        
        foreach (var cell in affectedCells)
        {
            // Apply damage
            if (actionConfig.Damage > 0)
            {
                stateManager.ApplyDamageToEntity(cell, actionConfig.Damage);
            }
            
            // Apply status effects
            if (!string.IsNullOrEmpty(actionConfig.StatusEffect))
            {
                stateManager.ApplyStatusEffectToEntity(cell, actionConfig.StatusEffect);
            }
            
            // Remove status effects (if property exists in your ActionConfig)
            // if (!string.IsNullOrEmpty(actionConfig.RemovesStatus))
            // {
            //     stateManager.RemoveStatusEffectFromEntity(cell, actionConfig.RemovesStatus);
            // }
        }
        
        // Apply skill costs
        if (actionConfig.Cost > 0)
        {
            GD.Print($"[BattleAction] Skill cost: {actionConfig.Cost}");
            // TODO: Integrate with BattleLogic money system
        }
        
        // TODO: Integrate with BattleLogic SkillAction class
    }
    
    private void ExecuteItemAction(Vector2I targetCell, ActionConfig actionConfig)
    {
        GD.Print($"[BattleAction] Item '{actionConfig.Name}' used on {targetCell}");
        GD.Print($"[BattleAction] Description: {actionConfig.Description}");
        
        // Calculate affected cells
        var affectedCells = CalculateAffectedCells(targetCell, actionConfig);
        
        foreach (var cell in affectedCells)
        {
            // Apply healing
            if (actionConfig.HealAmount > 0)
            {
                stateManager.ApplyHealingToEntity(cell, actionConfig.HealAmount);
            }
            
            // Apply damage (for offensive items)
            if (actionConfig.Damage > 0)
            {
                stateManager.ApplyDamageToEntity(cell, actionConfig.Damage);
            }
            
            // Apply status effects
            if (!string.IsNullOrEmpty(actionConfig.StatusEffect))
            {
                stateManager.ApplyStatusEffectToEntity(cell, actionConfig.StatusEffect);
            }
            
            // Remove status effects (if property exists in your ActionConfig)
            // if (!string.IsNullOrEmpty(actionConfig.RemovesStatus))
            // {
            //     stateManager.RemoveStatusEffectFromEntity(cell, actionConfig.RemovesStatus);
            // }
        }
        
        // Handle item consumption
        if (actionConfig.UsesRemaining > 0)
        {
            actionConfig.UsesRemaining--;
            GD.Print($"[BattleAction] {actionConfig.Name} remaining uses: {actionConfig.UsesRemaining}");
            
            if (actionConfig.UsesRemaining == 0)
            {
                GD.Print($"[BattleAction] {actionConfig.Name} is depleted!");
            }
        }
        
        // Apply item costs
        if (actionConfig.Cost > 0)
        {
            GD.Print($"[BattleAction] Item cost: {actionConfig.Cost}");
            // TODO: Integrate with BattleLogic money system
        }
        
        // TODO: Integrate with BattleLogic ItemAction class
    }
    
    private void ExecuteTalkAction(Vector2I targetCell, ActionConfig actionConfig)
    {
        GD.Print($"[BattleAction] Talk action '{actionConfig.Name}' targeting {targetCell}");
        
        // Display dialogue
        if (!string.IsNullOrEmpty(actionConfig.Dialogue))
        {
            GD.Print($"[BattleAction] Player says: \"{actionConfig.Dialogue}\"");
        }
        
        // Apply social effects to target
        if (stateManager.IsEnemyCell(targetCell) || stateManager.IsPlayerCell(targetCell))
        {
            // Apply friendship changes
            if (actionConfig.FriendshipChange != 0)
            {
                GD.Print($"[BattleAction] Friendship change: {(actionConfig.FriendshipChange > 0 ? "+" : "")}{actionConfig.FriendshipChange}");
                // TODO: Integrate with BattleLogic Entity friendship system
            }
            
            // Apply reputation changes
            if (actionConfig.ReputationChange != 0)
            {
                GD.Print($"[BattleAction] Reputation change: {(actionConfig.ReputationChange > 0 ? "+" : "")}{actionConfig.ReputationChange}");
                // TODO: Integrate with BattleLogic Entity reputation system
            }
            
            // Award money from successful negotiations (if property exists in your ActionConfig)
            // if (actionConfig.MoneyReward != 0)
            // {
            //     GD.Print($"[BattleAction] Money reward: {(actionConfig.MoneyReward > 0 ? "+" : "")}{actionConfig.MoneyReward}");
            //     // TODO: Integrate with BattleLogic money system
            // }
        }
        
        // Apply talk action costs
        if (actionConfig.Cost > 0)
        {
            GD.Print($"[BattleAction] Talk cost: {actionConfig.Cost}");
            // TODO: Integrate with BattleLogic money system
        }
        
        // TODO: Integrate with BattleLogic TalkAction class
    }
    
    #endregion
    
    #region Target Calculation
    
    private List<Vector2I> CalculateValidTargets(ActionConfig actionConfig)
    {
        var validTargets = new List<Vector2I>();
        var playerPosition = stateManager.GetPlayerPosition();
        var rangePattern = actionConfig.RangePattern.Select(p => p.ToVector2I()).ToList();
        
        // Use default hex pattern if none specified
        if (rangePattern.Count == 0)
        {
            rangePattern = GetDefaultHexPattern();
        }
        
        foreach (var offset in rangePattern)
        {
            var adjustedOffset = AdjustOffsetForHexGrid(offset, playerPosition);
            var targetCell = playerPosition + adjustedOffset;
            
            if (stateManager.IsValidGridPosition(targetCell) && CanTargetCell(targetCell, actionConfig.TargetType))
            {
                validTargets.Add(targetCell);
            }
        }
        
        // Add self-targeting if applicable
        if (CanTargetSelf(actionConfig.TargetType))
        {
            if (!validTargets.Contains(playerPosition))
            {
                validTargets.Add(playerPosition);
            }
        }
        
        return validTargets;
    }
    
    private List<Vector2I> CalculateAffectedCells(Vector2I targetCell, ActionConfig actionConfig)
    {
        var affectedCells = new List<Vector2I> { targetCell };
        
        // Add AOE pattern cells
        if (actionConfig.AoePattern != null && actionConfig.AoePattern.Count > 0)
        {
            foreach (var aoeOffset in actionConfig.AoePattern)
            {
                var aoeCell = targetCell + aoeOffset.ToVector2I();
                if (stateManager.IsValidGridPosition(aoeCell) && !affectedCells.Contains(aoeCell))
                {
                    affectedCells.Add(aoeCell);
                }
            }
        }
        
        return affectedCells;
    }
    
    private List<Vector2I> GetDefaultHexPattern()
    {
        return new List<Vector2I>
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, -1), new(-1, -1)
        };
    }
    
    private Vector2I AdjustOffsetForHexGrid(Vector2I offset, Vector2I playerPosition)
    {
        // Adjust for hex grid offset columns (odd columns are shifted)
        if (playerPosition.X % 2 != 0 && offset.X != 0)
        {
            return new Vector2I(offset.X, offset.Y + 1);
        }
        return offset;
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
    
    public List<Vector2I> GetValidTargetsForCurrentAction()
    {
        return new List<Vector2I>(currentValidTargets);
    }
    
    public string GetCurrentActionType() => currentActionType;
    public string GetSelectedActionOption() => selectedActionOption;
    public ActionConfig GetCurrentActionConfig() => currentActionConfig;
    
    #endregion
}