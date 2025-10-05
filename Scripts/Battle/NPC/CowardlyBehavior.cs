using Godot;
using System.Collections.Generic;
using System.Linq;
using CustomJsonSystem;

public class CowardlyBehavior : INPCBehavior
{
    public string GetBehaviorName() => "cowardly";
    
    public NPCDecision DecideAction(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        // Always try to flee from enemies
        var moveOptions = configLoader.GetMoveOptionsForEntity(actor.Type.ToString());
        
        // Filter by entity's available move options
        if (actor.AvailableMoveOptions.Count > 0)
            moveOptions = moveOptions.Where(m => 
                actor.AvailableMoveOptions.Contains(m.Id) || 
                actor.AvailableMoveOptions.Contains(m.Name)
            ).ToList();
        
        var nearestEnemy = GetNearestEnemyPosition(actor, stateManager);
        
        if (nearestEnemy != Vector2I.Zero)
        {
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
                        Priority = 15
                    };
                }
            }
        }
        
        return NPCDecision.Skip();
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
}