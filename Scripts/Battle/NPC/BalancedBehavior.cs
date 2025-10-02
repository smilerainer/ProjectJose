// BalancedBehavior.cs - Balanced/adaptive AI behavior
using Godot;
using System.Collections.Generic;
using System.Linq;
using CustomJsonSystem;

public class BalancedBehavior : INPCBehavior
{
    public string GetBehaviorName() => "balanced";
    
    public NPCDecision DecideAction(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        // Adaptive behavior based on situation
        
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
        if (attackDecision.IsValid && attackDecision.Priority >= 8) return attackDecision;
        
        // Move into better position
        var repositionDecision = TryReposition(actor, stateManager, configLoader, actionHandler);
        if (repositionDecision.IsValid) return repositionDecision;
        
        // Attack even if not ideal
        if (attackDecision.IsValid) return attackDecision;
        
        return NPCDecision.Skip();
    }
    
    private NPCDecision TryHealSelf(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        var skills = configLoader.GetSkillsForEntity(actor.Type.ToString());
        
        if (actor.AvailableSkills.Count > 0)
            skills = skills.Where(s => actor.AvailableSkills.Contains(s.Id) || actor.AvailableSkills.Contains(s.Name)).ToList();
        
        foreach (var skill in skills.Where(s => s.HealAmount > 0 && s.TargetType.ToLower() == "self").OrderByDescending(s => s.HealAmount))
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
        
        NPCDecision bestDecision = NPCDecision.Invalid();
        int bestPriority = 0;
        
        foreach (var skill in skills.Where(s => s.Damage > 0))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(actor.Position, skill);
            var enemyTargets = validTargets.Where(t => IsEnemyTarget(actor, t, stateManager)).ToList();
            
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
    
    private NPCDecision TryReposition(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        var moveOptions = configLoader.GetMoveOptionsForEntity(actor.Type.ToString());
        var nearestEnemy = GetNearestEnemyPosition(actor, stateManager);
        
        if (nearestEnemy == Vector2I.Zero) return NPCDecision.Invalid();
        
        int currentDistance = HexDistance(actor.Position, nearestEnemy);
        int idealDistance = 2; // Prefer medium range
        
        foreach (var move in moveOptions.OrderByDescending(m => m.Range))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(actor.Position, move);
            
            if (validTargets.Count > 0)
            {
                var target = validTargets
                    .OrderBy(t => Mathf.Abs(HexDistance(t, nearestEnemy) - idealDistance))
                    .First();
                
                int newDistance = HexDistance(target, nearestEnemy);
                
                // Only move if it improves position
                if (Mathf.Abs(newDistance - idealDistance) < Mathf.Abs(currentDistance - idealDistance))
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
    
    #region Helper Methods
    
    private int CalculateAttackPriority(Entity actor, ActionConfig skill, Vector2I target, BattleStateManager stateManager)
    {
        int priority = 5;
        
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
    
    private Vector2I GetBestTarget(Entity actor, List<Vector2I> targets, BattleStateManager stateManager)
    {
        return targets
            .Select(t => new 
            { 
                Position = t, 
                Entity = stateManager.GetEntityAt(t),
                Distance = HexDistance(actor.Position, t)
            })
            .Where(x => x.Entity != null)
            .OrderBy(x => x.Entity.CurrentHP)
            .ThenBy(x => x.Distance)
            .First()
            .Position;
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