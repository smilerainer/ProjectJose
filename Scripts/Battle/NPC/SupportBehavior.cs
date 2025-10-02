// SupportBehavior.cs - Support/healing focused AI behavior
using Godot;
using System.Collections.Generic;
using System.Linq;
using CustomJsonSystem;

public class SupportBehavior : INPCBehavior
{
    public string GetBehaviorName() => "support";
    
    public NPCDecision DecideAction(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        // Priority: Heal wounded ally > Buff allies > Attack if no support needed > Skip
        
        var healDecision = TryHealAlly(actor, stateManager, configLoader, actionHandler);
        if (healDecision.IsValid) return healDecision;
        
        var buffDecision = TryBuffAlly(actor, stateManager, configLoader, actionHandler);
        if (buffDecision.IsValid) return buffDecision;
        
        var attackDecision = TryAttack(actor, stateManager, configLoader, actionHandler);
        if (attackDecision.IsValid) return attackDecision;
        
        return NPCDecision.Skip();
    }
    
    private NPCDecision TryHealAlly(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        var skills = configLoader.GetSkillsForEntity(actor.Type.ToString());
        
        if (actor.AvailableSkills.Count > 0)
            skills = skills.Where(s => actor.AvailableSkills.Contains(s.Id) || actor.AvailableSkills.Contains(s.Name)).ToList();
        
        foreach (var skill in skills.Where(s => s.HealAmount > 0).OrderByDescending(s => s.HealAmount))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(actor.Position, skill);
            var allyTargets = validTargets.Where(t => IsAllyTarget(actor, t, stateManager) && IsWounded(t, stateManager)).ToList();
            
            if (allyTargets.Count > 0)
            {
                // Prioritize most wounded ally
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
        
        // Try items
        var items = configLoader.GetItemsForEntity(actor.Type.ToString());
        
        if (actor.AvailableItems.Count > 0)
            items = items.Where(i => actor.AvailableItems.Contains(i.Id) || actor.AvailableItems.Contains(i.Name)).ToList();
        
        foreach (var item in items.Where(i => i.HealAmount > 0).OrderByDescending(i => i.HealAmount))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(actor.Position, item);
            var allyTargets = validTargets.Where(t => IsAllyTarget(actor, t, stateManager) && IsWounded(t, stateManager)).ToList();
            
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
    
    private NPCDecision TryBuffAlly(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        var skills = configLoader.GetSkillsForEntity(actor.Type.ToString());
        
        if (actor.AvailableSkills.Count > 0)
            skills = skills.Where(s => actor.AvailableSkills.Contains(s.Id) || actor.AvailableSkills.Contains(s.Name)).ToList();
        
        foreach (var skill in skills.Where(s => !string.IsNullOrEmpty(s.StatusEffect) && s.Damage == 0))
        {
            var validTargets = actionHandler.CalculateValidTargetsFromPosition(actor.Position, skill);
            var allyTargets = validTargets.Where(t => IsAllyTarget(actor, t, stateManager)).ToList();
            
            if (allyTargets.Count > 0)
            {
                // Buff closest ally or self
                var target = allyTargets.OrderBy(t => HexDistance(actor.Position, t)).First();
                
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
        
        foreach (var skill in skills.Where(s => s.Damage > 0).OrderBy(s => s.Damage))
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
                    Priority = 5
                };
            }
        }
        
        return NPCDecision.Invalid();
    }
    
    #region Helper Methods
    
    private bool IsAllyTarget(Entity actor, Vector2I target, BattleStateManager stateManager)
    {
        var entityAt = stateManager.GetEntityAt(target);
        if (entityAt == null) return false;
        return actor.IsAllyOf(entityAt);
    }
    
    private bool IsEnemyTarget(Entity actor, Vector2I target, BattleStateManager stateManager)
    {
        var entityAt = stateManager.GetEntityAt(target);
        if (entityAt == null) return false;
        return actor.IsEnemyOf(entityAt);
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