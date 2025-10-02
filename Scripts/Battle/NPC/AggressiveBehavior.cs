using Godot;
using System.Collections.Generic;
using System.Linq;
using CustomJsonSystem;

public class AggressiveBehavior : INPCBehavior
{
    public string GetBehaviorName() => "aggressive";
    
    public NPCDecision DecideAction(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        // Simple aggressive AI - just attack nearest enemy
        var skills = configLoader.GetSkillsForEntity(actor.Type.ToString());
        
        if (actor.AvailableSkills.Count > 0)
            skills = skills.Where(s => actor.AvailableSkills.Contains(s.Id) || actor.AvailableSkills.Contains(s.Name)).ToList();
        
        foreach (var skill in skills.Where(s => s.Damage > 0).OrderByDescending(s => s.Damage))
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
                    Priority = 10
                };
            }
        }
        
        return NPCDecision.Skip();
    }
    
    private bool IsEnemyTarget(Entity actor, Vector2I target, BattleStateManager stateManager)
    {
        var entityAt = stateManager.GetEntityAt(target);
        return entityAt != null && actor.IsEnemyOf(entityAt);
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
}