// DefensiveBehavior.cs - Defensive/cautious AI behavior
using Godot;
using System.Collections.Generic;
using System.Linq;
using CustomJsonSystem;

public class DefensiveBehavior : INPCBehavior
{
    public string GetBehaviorName() => "defensive";
    
    public NPCDecision DecideAction(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        // Priority: Self-heal if low > Move away if threatened > Attack if safe > Skip
        
        // If low health, try to heal
        if (actor.IsLowHealth(actor.BehaviorConfig.HealthThreshold))
        {
            var healDecision = TryHeal(actor, stateManager, configLoader, actionHandler);
            if (healDecision.IsValid) return healDecision;
        }
        
        // If enemies are too close, retreat
        if (IsThreatenedByNearbyEnemies(actor, stateManager))
        {
            var retreatDecision = TryRetreat(actor, stateManager, configLoader, actionHandler);
            if (retreatDecision.IsValid) return retreatDecision;
        }
        
        // If safe, attack
        var attackDecision = TryAttack(actor, stateManager, configLoader, actionHandler);
        if (attackDecision.IsValid) return attackDecision;
        
        return NPCDecision.Skip();
    }
    
    private NPCDecision TryHeal(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        // Try healing skills first
        var skills = configLoader.GetSkillsForEntity(actor.Type.ToString());
        
        if (actor.AvailableSkills.Count > 0)
            skills = skills.Where(s => actor.AvailableSkills.Contains(s.Id) || actor.AvailableSkills.Contains(s.Name)).ToList();
        
        foreach (var skill in skills.Where(s => s.HealAmount > 0).OrderByDescending(s => s.HealAmount))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(actor.Position, skill);
            
            if (validTargets.Contains(actor.Position))
            {
                return new NPCDecision
                {
                    ActionType = "skill",
                    ActionName = skill.Name,
                    TargetCell = actor.Position,
                    IsValid = true,
                    Priority = 15
                };
            }
        }
        
        // Try healing items
        var items = configLoader.GetItemsForEntity(actor.Type.ToString());
        
        if (actor.AvailableItems.Count > 0)
            items = items.Where(i => actor.AvailableItems.Contains(i.Id) || actor.AvailableItems.Contains(i.Name)).ToList();
        
        foreach (var item in items.Where(i => i.HealAmount > 0).OrderByDescending(i => i.HealAmount))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(actor.Position, item);
            
            if (validTargets.Contains(actor.Position))
            {
                return new NPCDecision
                {
                    ActionType = "item",
                    ActionName = item.Name,
                    TargetCell = actor.Position,
                    IsValid = true,
                    Priority = 14
                };
            }
        }
        
        return NPCDecision.Invalid();
    }
    
    private NPCDecision TryRetreat(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        var moveOptions = configLoader.GetMoveOptionsForEntity(actor.Type.ToString());
        var nearestEnemy = GetNearestEnemyPosition(actor, stateManager);
        
        if (nearestEnemy == Vector2I.Zero) return NPCDecision.Invalid();
        
        foreach (var move in moveOptions.OrderByDescending(m => m.Range))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(actor.Position, move);
            
            if (validTargets.Count > 0)
            {
                // Pick cell farthest from nearest enemy
                var target = validTargets.OrderByDescending(t => HexDistance(t, nearestEnemy)).First();
                
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
    
    private NPCDecision TryAttack(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        var skills = configLoader.GetSkillsForEntity(actor.Type.ToString());
        
        if (actor.AvailableSkills.Count > 0)
            skills = skills.Where(s => actor.AvailableSkills.Contains(s.Id) || actor.AvailableSkills.Contains(s.Name)).ToList();
        
        // Prefer longer range attacks
        foreach (var skill in skills.Where(s => s.Damage > 0).OrderByDescending(s => s.Range).ThenByDescending(s => s.Damage))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(actor.Position, skill);
            var enemyTargets = validTargets.Where(t => IsEnemyTarget(actor, t, stateManager)).ToList();
            
            if (enemyTargets.Count > 0)
            {
                var target = enemyTargets.OrderBy(t => HexDistance(actor.Position, t)).First();
                
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
    
    #region Helper Methods
    
    private bool IsThreatenedByNearbyEnemies(Entity actor, BattleStateManager stateManager)
    {
        var enemies = stateManager.GetAllEntities()
            .Where(e => e.IsAlive && actor.IsEnemyOf(e))
            .ToList();
        
        return enemies.Any(e => HexDistance(actor.Position, e.Position) <= 2);
    }
    
    private bool IsEnemyTarget(Entity actor, Vector2I target, BattleStateManager stateManager)
    {
        var entityAt = stateManager.GetEntityAt(target);
        if (entityAt == null) return false;
        return actor.IsEnemyOf(entityAt);
    }
    
    private Vector2I GetNearestEnemyPosition(Entity actor, BattleStateManager stateManager)
    {
        var enemies = stateManager.GetAllEntities()
            .Where(e => e.IsAlive && actor.IsEnemyOf(e))
            .ToList();
        
        if (enemies.Count == 0) return Vector2I.Zero;
        
        return enemies.OrderBy(e => HexDistance(actor.Position, e.Position)).First().Position;
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
    
    #endregion
}