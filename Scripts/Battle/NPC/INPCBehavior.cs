// INPCBehavior.cs - Interface for NPC AI behaviors
using Godot;

public interface INPCBehavior
{
    /// <summary>
    /// Decide what action the NPC should take this turn
    /// </summary>
    NPCDecision DecideAction(
        Entity actor,
        BattleStateManager stateManager,
        BattleConfigurationLoader configLoader,
        BattleActionHandler actionHandler
    );
    
    /// <summary>
    /// Get the name of this behavior for registration
    /// </summary>
    string GetBehaviorName();
}

public struct NPCDecision
{
    public string ActionType;  // "skill", "move", "item", "talk", "skip"
    public string ActionName;  // Name of the specific action
    public Vector2I TargetCell; // Where to target the action
    public bool IsValid;       // Whether this decision is executable
    public int Priority;       // For comparing multiple valid options (higher = better)
    
    /// <summary>
    /// Create an invalid decision (no action available)
    /// </summary>
    public static NPCDecision Invalid() => new NPCDecision { IsValid = false };
    
    /// <summary>
    /// Create a skip turn decision
    /// </summary>
    public static NPCDecision Skip() => new NPCDecision 
    { 
        ActionType = "skip", 
        IsValid = true,
        Priority = 0
    };
}