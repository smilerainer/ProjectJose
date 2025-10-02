using Godot;
using System.Collections.Generic;

public class NPCBehaviorManager
{
    private Dictionary<string, INPCBehavior> behaviors = new();
    private BattleStateManager stateManager;
    private BattleConfigurationLoader configLoader;
    private BattleActionHandler actionHandler;

    public void Initialize(
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler)
    {
        this.stateManager = stateManager;
        this.configLoader = configLoader;
        this.actionHandler = actionHandler;
        
        RegisterDefaultBehaviors();
        GD.Print("[NPCBehavior] Manager initialized");
    }

    private void RegisterDefaultBehaviors()
    {
        RegisterBehavior(new AggressiveBehavior());
        RegisterBehavior(new DefensiveBehavior());
        RegisterBehavior(new SupportBehavior());
        RegisterBehavior(new BalancedBehavior());
        RegisterBehavior(new CowardlyBehavior());
    }

    public void RegisterBehavior(INPCBehavior behavior)
    {
        behaviors[behavior.GetBehaviorName()] = behavior;
        GD.Print($"[NPCBehavior] Registered: {behavior.GetBehaviorName()}");
    }

    public NPCDecision GetDecisionForEntity(Entity entity)
    {
        if (!entity.IsAlive || !entity.CanAct)
        {
            return NPCDecision.Skip();
        }

        var behaviorType = entity.BehaviorType ?? "balanced";

        if (!behaviors.TryGetValue(behaviorType, out var behavior))
        {
            GD.PrintErr($"[NPCBehavior] Unknown behavior: {behaviorType}, using balanced");
            behavior = behaviors["balanced"];
        }

        return behavior.DecideAction(entity, stateManager, configLoader, actionHandler);
    }

    public void ExecuteDecision(Entity entity, NPCDecision decision)
{
    if (!decision.IsValid || decision.ActionType == "skip")
    {
        GD.Print($"[NPCBehavior] {entity.Name} skips turn");
        return;
    }

    GD.Print($"[NPCBehavior] Executing {entity.Name}'s action: {decision.ActionType} - {decision.ActionName}");

    try
    {
        // Don't use the player's action handler - execute directly
        if (decision.ActionType == "move")
        {
            stateManager.MoveEntity(entity, decision.TargetCell);
        }
        else if (decision.ActionType == "skill")
        {
            var config = configLoader.GetActionConfig(decision.ActionName);
            if (config != null)
            {
                var affectedCells = actionHandler.CalculateAffectedCellsFromPosition(
                    entity.Position, 
                    decision.TargetCell, 
                    config
                );
                
                foreach (var cell in affectedCells)
                {
                    if (config.Damage > 0)
                        stateManager.ApplyDamageToEntity(cell, config.Damage);
                    if (config.HealAmount > 0)
                        stateManager.ApplyHealingToEntity(cell, config.HealAmount);
                }
            }
        }
        
        entity.HasActedThisTurn = true;
    }
    catch (System.Exception ex)
    {
        GD.PrintErr($"[NPCBehavior] Error: {ex.Message}");
    }
}
}