// Entity.cs - Core entity data structure for battle system
using Godot;
using System.Collections.Generic;
using CustomJsonSystem;

public enum EntityType
{
    Player,
    Ally,
    Enemy,
    Neutral,
    NPC
}

public class Entity
{
    #region Core Properties
    
    public string Id { get; set; }
    public string Name { get; set; }
    public EntityType Type { get; set; }
    public Vector2I Position { get; set; }
    
    #endregion
    
    #region Stats
    
    public float CurrentHP { get; set; }
    public float MaxHP { get; set; }
    public int Initiative { get; set; }
    public int Speed { get; set; }
    
    #endregion
    
    #region AI/Behavior
    
    public string BehaviorType { get; set; }
    public NPCBehaviorConfig BehaviorConfig { get; set; }
    
    #endregion
    
    #region Status and State
    
    public List<StatusEffect> ActiveStatuses { get; set; } = new();
    public bool HasActedThisTurn { get; set; } = false;
    public bool IsAlive => CurrentHP > 0;
    public bool CanAct => IsAlive && !HasStatus("stun");
    
    #endregion
    
    #region Available Actions

    public List<string> AvailableSkills { get; set; } = new();
    public List<string> AvailableItems { get; set; } = new();
    public List<string> AvailableMoveOptions { get; set; } = new();  // ← ADD
    public List<string> AvailableTalkOptions { get; set; } = new();  // ← ADD

    #endregion
    
    #region Constructors
    
    public Entity(string id, string name, EntityType type, Vector2I position, float maxHP, int initiative, int speed)
    {
        Id = id;
        Name = name;
        Type = type;
        Position = position;
        MaxHP = maxHP;
        CurrentHP = maxHP;
        Initiative = initiative;
        Speed = speed;
        BehaviorType = "balanced";
        BehaviorConfig = new NPCBehaviorConfig();
    }
    
    public static Entity FromDefinition(EntityDefinition def)
    {
        var type = def.EntityType.ToLower() switch
        {
            "player" => EntityType.Player,
            "ally" => EntityType.Ally,
            "enemy" => EntityType.Enemy,
            "npc" => EntityType.NPC,
            "neutral" => EntityType.Neutral,
            _ => EntityType.Enemy
        };
        
        var entity = new Entity(
            def.Id,
            def.Name,
            type,
            def.StartPosition.ToVector2I(),
            def.MaxHP,
            def.Initiative,
            def.Speed
        );
        
        entity.BehaviorType = def.BehaviorConfig.BehaviorType;
        entity.BehaviorConfig = def.BehaviorConfig;
        entity.AvailableSkills = new List<string>(def.AvailableSkills);
        entity.AvailableItems = new List<string>(def.AvailableItems);
        entity.AvailableMoveOptions = new List<string>(def.AvailableMoveOptions);  // ← ADD
        entity.AvailableTalkOptions = new List<string>(def.AvailableTalkOptions);  // ← ADD
        
        return entity;
    }
    
    #endregion
    
    #region Status Effects
    
    public bool HasStatus(string statusName)
    {
        return ActiveStatuses.Exists(s => s.Name.Equals(statusName, System.StringComparison.OrdinalIgnoreCase));
    }
    
    public void AddStatus(StatusEffect status)
    {
        var existing = ActiveStatuses.Find(s => s.Name == status.Name);
        if (existing != null)
        {
            existing.RemainingDuration = Mathf.Max(existing.RemainingDuration, status.RemainingDuration);
        }
        else
        {
            ActiveStatuses.Add(status);
        }
    }
    
    public void RemoveStatus(string statusName)
    {
        ActiveStatuses.RemoveAll(s => s.Name.Equals(statusName, System.StringComparison.OrdinalIgnoreCase));
    }
    
    #endregion
    
    #region Health Management
    
    public void TakeDamage(float amount)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
    }
    
    public void Heal(float amount)
    {
        CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
    }
    
    #endregion
    
    #region Turn Processing
    
    public void ProcessTurnEnd()
    {
        var statusesToRemove = new List<StatusEffect>();
        
        foreach (var status in ActiveStatuses)
        {
            if (status.DamagePerTurn > 0)
                TakeDamage(status.DamagePerTurn);
            
            if (status.HealPerTurn > 0)
                Heal(status.HealPerTurn);
            
            status.RemainingDuration--;
            
            if (status.RemainingDuration <= 0)
                statusesToRemove.Add(status);
        }
        
        foreach (var status in statusesToRemove)
            ActiveStatuses.Remove(status);
        
        HasActedThisTurn = false;
    }
    
    #endregion
    
    #region Helper Methods
    
    public float GetHealthPercentage() => MaxHP > 0 ? CurrentHP / MaxHP : 0f;
    
    public bool IsLowHealth(float threshold = 0.3f) => GetHealthPercentage() < threshold;
    
    public bool IsEnemyOf(Entity other)
    {
        return (Type == EntityType.Player || Type == EntityType.Ally) && other.Type == EntityType.Enemy ||
               Type == EntityType.Enemy && (other.Type == EntityType.Player || other.Type == EntityType.Ally);
    }
    
    public bool IsAllyOf(Entity other)
    {
        return Type == other.Type ||
               (Type == EntityType.Player && other.Type == EntityType.Ally) ||
               (Type == EntityType.Ally && other.Type == EntityType.Player);
    }
    
    #endregion
}

public class StatusEffect
{
    public string Name { get; set; }
    public int RemainingDuration { get; set; }
    public float DamagePerTurn { get; set; }
    public float HealPerTurn { get; set; }
    
    public StatusEffect(string name, int duration, float damagePerTurn = 0, float healPerTurn = 0)
    {
        Name = name;
        RemainingDuration = duration;
        DamagePerTurn = damagePerTurn;
        HealPerTurn = healPerTurn;
    }
}