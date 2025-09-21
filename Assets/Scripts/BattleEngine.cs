// EnhancedBattleEngine.cs - Reusable battle system library
using System;
using System.Collections.Generic;
using System.Linq;

namespace EnhancedBattleSystem
{
    #region Core Types
    public enum EntityType { Player, Ally, Enemy, NPC }
    public enum ActionType { Skill, Move, Talk, Item }
    public enum TargetType { Single, Multi, Self, All }
    
    public struct HexPos
    {
        public int Q, R;
        public HexPos(int q, int r) { Q = q; R = r; }
        public int Distance(HexPos other) => (Math.Abs(Q - other.Q) + Math.Abs(Q + R - other.Q - other.R) + Math.Abs(R - other.R)) / 2;
        public List<HexPos> GetNeighbors(int range = 1)
        {
            var neighbors = new List<HexPos>();
            for (int q = -range; q <= range; q++)
                for (int r = Math.Max(-range, -q - range); r <= Math.Min(range, -q + range); r++)
                    if (q != 0 || r != 0)
                        neighbors.Add(new HexPos(Q + q, R + r));
            return neighbors;
        }
        public override string ToString() => $"({Q},{R})";
    }
    #endregion

    #region Entities
    public class Entity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public EntityType Type { get; set; }
        public float HP { get; set; }
        public float MaxHP { get; set; }
        public float Speed { get; set; }
        public HexPos Position { get; set; }
        public Dictionary<string, int> Statuses { get; set; } = new();
        public Dictionary<string, object> Properties { get; set; } = new();
        
        // Relationships
        public int Friendship { get; set; } = 50;  // 0-100
        public int Reputation { get; set; } = 50;  // 0-100
        public int Money { get; set; } = 0;
        
        public bool IsAlive => HP > 0;
        public bool CanAct => IsAlive && !HasStatus("Stun");
        
        public Entity(string name, EntityType type, float hp, float speed)
        {
            Name = name;
            Type = type;
            HP = MaxHP = hp;
            Speed = speed;
        }
        
        public bool HasStatus(string status) => Statuses.ContainsKey(status);
        public void AddStatus(string status, int duration) => Statuses[status] = duration;
        public void RemoveStatus(string status) => Statuses.Remove(status);
        
        public T GetProperty<T>(string key, T defaultValue = default)
        {
            return Properties.ContainsKey(key) ? (T)Properties[key] : defaultValue;
        }
        
        public void SetProperty(string key, object value) => Properties[key] = value;
    }
    #endregion

    #region Rules System
    public interface ICondition
    {
        bool Evaluate(BattleContext context);
        string GetDescription();
    }
    
    public interface IEffect
    {
        void Apply(BattleContext context);
        string GetDescription();
    }
    
    public class Rule
    {
        public string Name { get; set; }
        public ICondition Condition { get; set; }
        public IEffect Effect { get; set; }
        public int Priority { get; set; }
        public bool Active { get; set; } = true;
        
        public Rule(string name, ICondition condition, IEffect effect, int priority = 0)
        {
            Name = name;
            Condition = condition;
            Effect = effect;
            Priority = priority;
        }
    }
    
    public class BattleContext
    {
        public Entity Actor { get; set; }
        public List<Entity> Targets { get; set; } = new();
        public BattleAction Action { get; set; }
        public BattleState State { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
        
        public T Get<T>(string key, T defaultValue = default)
        {
            return Data.ContainsKey(key) ? (T)Data[key] : defaultValue;
        }
        
        public void Set(string key, object value) => Data[key] = value;
    }
    
    // Common Conditions
    public class EntityTypeCondition : ICondition
    {
        private EntityType type;
        private bool checkActor;
        
        public EntityTypeCondition(EntityType type, bool actor = true)
        {
            this.type = type;
            checkActor = actor;
        }
        
        public bool Evaluate(BattleContext ctx)
        {
            var entity = checkActor ? ctx.Actor : ctx.Targets?.FirstOrDefault();
            return entity?.Type == type;
        }
        
        public string GetDescription() => $"{(checkActor ? "Actor" : "Target")} is {type}";
    }
    
    public class FriendshipCondition : ICondition
    {
        private int threshold;
        private bool above;
        
        public FriendshipCondition(int threshold, bool above = true)
        {
            this.threshold = threshold;
            this.above = above;
        }
        
        public bool Evaluate(BattleContext ctx)
        {
            if (ctx.Actor == null) return false;
            return above ? ctx.Actor.Friendship >= threshold : ctx.Actor.Friendship < threshold;
        }
        
        public string GetDescription() => $"Friendship {(above ? ">=" : "<")} {threshold}";
    }
    
    // Common Effects
    public class ModifyCostEffect : IEffect
    {
        private float multiplier;
        private int flat;
        
        public ModifyCostEffect(float mult = 1f, int flat = 0)
        {
            multiplier = mult;
            this.flat = flat;
        }
        
        public void Apply(BattleContext ctx)
        {
            var cost = ctx.Get<int>("Cost", 0);
            var newCost = Math.Max(0, (int)(cost * multiplier) + flat);
            ctx.Set("Cost", newCost);
            ctx.Set("CostModified", true);
        }
        
        public string GetDescription() => $"Cost x{multiplier} {(flat >= 0 ? "+" : "")}{flat}";
    }
    
    public class LogEffect : IEffect
    {
        private string message;
        
        public LogEffect(string msg) { message = msg; }
        
        public void Apply(BattleContext ctx)
        {
            ctx.State?.Log(message);
        }
        
        public string GetDescription() => message;
    }
    #endregion

    #region Actions
    public abstract class BattleAction
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int BaseCost { get; set; }
        public TargetType TargetType { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
        
        public abstract bool CanExecute(Entity actor, BattleState state);
        public abstract ActionResult Execute(Entity actor, List<Entity> targets, BattleState state);
        public abstract List<Entity> GetValidTargets(Entity actor, BattleState state);
        
        public int CalculateCost(Entity actor, BattleState state)
        {
            var context = new BattleContext
            {
                Actor = actor,
                Action = this,
                State = state
            };
            context.Set("Cost", BaseCost);
            
            // Apply rule modifications
            state.RuleEngine.ProcessRules(context, "CostCalculation");
            
            return context.Get<int>("Cost", BaseCost);
        }
    }
    
    public class ActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }
    
    // Skill Action
    public class SkillAction : BattleAction
    {
        public float Damage { get; set; }
        public string AppliesStatus { get; set; }
        public int StatusDuration { get; set; }
        public int Range { get; set; } = 1;
        
        public override bool CanExecute(Entity actor, BattleState state)
        {
            return actor.CanAct;
        }
        
        public override ActionResult Execute(Entity actor, List<Entity> targets, BattleState state)
        {
            var result = new ActionResult { Success = true };
            var messages = new List<string>();
            
            messages.Add($"{actor.Name} uses {Name}!");
            
            foreach (var target in targets.Where(t => t != null))
            {
                if (Damage > 0)
                {
                    target.HP = Math.Max(0, target.HP - Damage);
                    messages.Add($"  {target.Name} takes {Damage} damage!");
                    if (!target.IsAlive)
                        messages.Add($"  {target.Name} is defeated!");
                }
                else if (Damage < 0)
                {
                    var heal = Math.Min(-Damage, target.MaxHP - target.HP);
                    target.HP += heal;
                    messages.Add($"  {target.Name} heals {heal} HP!");
                }
                
                if (!string.IsNullOrEmpty(AppliesStatus) && target.IsAlive)
                {
                    target.AddStatus(AppliesStatus, StatusDuration);
                    messages.Add($"  {target.Name} is affected by {AppliesStatus}!");
                }
            }
            
            // Handle costs
            int cost = CalculateCost(actor, state);
            if (cost > 0 && actor.Type != EntityType.Player)
            {
                state.PlayerMoney -= cost;
                messages.Add($"  💰 Cost: {cost}P");
            }
            
            result.Message = string.Join("\n", messages);
            state.Log(result.Message);
            return result;
        }
        
        public override List<Entity> GetValidTargets(Entity actor, BattleState state)
        {
            return TargetType switch
            {
                TargetType.Self => new List<Entity> { actor },
                TargetType.All => state.GetAllEntities(),
                _ => state.GetAllEntities()
                    .Where(e => e.IsAlive && actor.Position.Distance(e.Position) <= Range)
                    .ToList()
            };
        }
    }
    
    // Move Action
    public class MoveAction : BattleAction
    {
        public int MoveRange { get; set; }
        
        public override bool CanExecute(Entity actor, BattleState state)
        {
            return actor.CanAct && !actor.HasStatus("Root");
        }
        
        public override ActionResult Execute(Entity actor, List<Entity> targets, BattleState state)
        {
            var result = new ActionResult { Success = true };
            
            // Target is actually a position encoded as entity properties
            var destination = targets.FirstOrDefault();
            if (destination == null)
            {
                result.Success = false;
                result.Message = "Invalid destination";
                return result;
            }
            
            var newPos = new HexPos(
                destination.GetProperty<int>("DestX"),
                destination.GetProperty<int>("DestY")
            );
            
            var oldPos = actor.Position;
            actor.Position = newPos;
            
            string message = $"{actor.Name} {Name} from {oldPos} to {newPos}";
            
            // Handle costs
            int cost = CalculateCost(actor, state);
            if (cost > 0 && actor.Type != EntityType.Player)
            {
                state.PlayerMoney -= cost;
                message += $" (Cost: {cost}P)";
            }
            
            result.Message = message;
            state.Log(message);
            return result;
        }
        
        public override List<Entity> GetValidTargets(Entity actor, BattleState state)
        {
            // Return valid positions as dummy entities
            var validPositions = new List<Entity>();
            var neighbors = actor.Position.GetNeighbors(MoveRange);
            
            foreach (var pos in neighbors)
            {
                if (!state.GetAllEntities().Any(e => e.Position.Q == pos.Q && e.Position.R == pos.R))
                {
                    var dummy = new Entity($"Position {pos}", EntityType.NPC, 0, 0);
                    dummy.SetProperty("DestX", pos.Q);
                    dummy.SetProperty("DestY", pos.R);
                    validPositions.Add(dummy);
                }
            }
            
            return validPositions;
        }
    }
    
    // Talk Action
    public class TalkAction : BattleAction
    {
        public int FriendshipChange { get; set; }
        public int ReputationChange { get; set; }
        public string Dialogue { get; set; }
        
        public override bool CanExecute(Entity actor, BattleState state)
        {
            return actor.CanAct && !actor.HasStatus("Silence");
        }
        
        public override ActionResult Execute(Entity actor, List<Entity> targets, BattleState state)
        {
            var result = new ActionResult { Success = true };
            var messages = new List<string>();
            
            messages.Add($"{actor.Name} says: \"{Dialogue}\"");
            
            foreach (var target in targets.Where(t => t != null))
            {
                if (FriendshipChange != 0)
                {
                    target.Friendship = Math.Clamp(target.Friendship + FriendshipChange, 0, 100);
                    messages.Add($"  {target.Name}'s friendship {(FriendshipChange > 0 ? "+" : "")}{FriendshipChange}");
                }
                
                if (ReputationChange != 0)
                {
                    target.Reputation = Math.Clamp(target.Reputation + ReputationChange, 0, 100);
                    messages.Add($"  {target.Name}'s reputation {(ReputationChange > 0 ? "+" : "")}{ReputationChange}");
                }
            }
            
            // Handle costs (e.g., bribes)
            int cost = CalculateCost(actor, state);
            if (cost > 0)
            {
                if (actor.Type == EntityType.Player)
                {
                    state.PlayerMoney -= cost;
                    messages.Add($"  💰 You pay {cost}P");
                }
                else
                {
                    state.PlayerMoney -= cost;
                    messages.Add($"  💰 Cost: {cost}P");
                }
            }
            
            result.Message = string.Join("\n", messages);
            state.Log(result.Message);
            return result;
        }
        
        public override List<Entity> GetValidTargets(Entity actor, BattleState state)
        {
            return state.GetAllEntities().Where(e => e != actor && e.IsAlive).ToList();
        }
    }
    
    // Item Action
    public class ItemAction : BattleAction
    {
        public float HealAmount { get; set; }
        public float DamageAmount { get; set; }
        public string RemovesStatus { get; set; }
        public string AppliesStatus { get; set; }
        public int StatusDuration { get; set; }
        public int UsesRemaining { get; set; }
        
        public override bool CanExecute(Entity actor, BattleState state)
        {
            return actor.CanAct && UsesRemaining > 0;
        }
        
        public override ActionResult Execute(Entity actor, List<Entity> targets, BattleState state)
        {
            var result = new ActionResult { Success = true };
            var messages = new List<string>();
            
            messages.Add($"{actor.Name} uses {Name}!");
            UsesRemaining--;
            
            foreach (var target in targets.Where(t => t != null))
            {
                if (HealAmount > 0)
                {
                    var heal = Math.Min(HealAmount, target.MaxHP - target.HP);
                    target.HP += heal;
                    messages.Add($"  {target.Name} heals {heal} HP!");
                }
                
                if (DamageAmount > 0)
                {
                    target.HP = Math.Max(0, target.HP - DamageAmount);
                    messages.Add($"  {target.Name} takes {DamageAmount} damage!");
                }
                
                if (!string.IsNullOrEmpty(RemovesStatus))
                {
                    target.RemoveStatus(RemovesStatus);
                    messages.Add($"  {target.Name}'s {RemovesStatus} is cured!");
                }
                
                if (!string.IsNullOrEmpty(AppliesStatus))
                {
                    target.AddStatus(AppliesStatus, StatusDuration);
                    messages.Add($"  {target.Name} is affected by {AppliesStatus}!");
                }
            }
            
            messages.Add($"  {Name} remaining: {UsesRemaining}");
            
            // Handle ally fees for items
            int cost = CalculateCost(actor, state);
            if (cost > 0 && actor.Type != EntityType.Player)
            {
                state.PlayerMoney -= cost;
                messages.Add($"  💰 Cost: {cost}P");
            }
            
            result.Message = string.Join("\n", messages);
            state.Log(result.Message);
            return result;
        }
        
        public override List<Entity> GetValidTargets(Entity actor, BattleState state)
        {
            return TargetType switch
            {
                TargetType.Self => new List<Entity> { actor },
                TargetType.All => state.GetAllEntities(),
                _ => state.GetAllEntities().Where(e => e.IsAlive).ToList()
            };
        }
    }
    #endregion

    #region Battle State
    public class RuleEngine
    {
        private Dictionary<string, List<Rule>> rules = new();
        
        public void AddRule(string phase, Rule rule)
        {
            if (!rules.ContainsKey(phase))
                rules[phase] = new List<Rule>();
            rules[phase].Add(rule);
            rules[phase].Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
        
        public void ProcessRules(BattleContext context, string phase)
        {
            if (!rules.ContainsKey(phase)) return;
            
            foreach (var rule in rules[phase].Where(r => r.Active))
            {
                if (rule.Condition.Evaluate(context))
                {
                    rule.Effect.Apply(context);
                }
            }
        }
    }
    
    public class BattleState
    {
        private List<Entity> entities = new();
        private List<BattleAction> actionHistory = new();
        private int round = 0;
        
        public int PlayerMoney { get; set; } = 100;
        public RuleEngine RuleEngine { get; set; } = new();
        public Action<string> LogHandler { get; set; }
        
        public void AddEntity(Entity entity)
        {
            entities.Add(entity);
        }
        
        public List<Entity> GetAllEntities() => entities.ToList();
        public List<Entity> GetAliveEntities() => entities.Where(e => e.IsAlive).ToList();
        
        public Entity GetPlayer() => entities.FirstOrDefault(e => e.Type == EntityType.Player);
        
        public void Log(string message)
        {
            LogHandler?.Invoke(message);
        }
        
        public bool IsGameOver()
        {
            var player = GetPlayer();
            if (player == null || !player.IsAlive) return true;
            
            // Check if all enemies defeated
            return !entities.Any(e => e.Type == EntityType.Enemy && e.IsAlive);
        }
        
        public List<Entity> GetTurnOrder()
        {
            round++;
            Log($"\n=== ROUND {round} ===");
            
            // Process status effects
            foreach (var entity in entities.Where(e => e.IsAlive))
            {
                ProcessStatuses(entity);
            }
            
            // Return sorted by speed
            return entities
                .Where(e => e.IsAlive)
                .OrderByDescending(e => e.Speed)
                .ToList();
        }
        
        private void ProcessStatuses(Entity entity)
        {
            var toRemove = new List<string>();
            
            foreach (var status in entity.Statuses.ToList())
            {
                // Apply status effects
                switch (status.Key)
                {
                    case "Poison":
                        entity.HP = Math.Max(0, entity.HP - 10);
                        Log($"  {entity.Name} takes 10 poison damage!");
                        break;
                    case "Regen":
                        var heal = Math.Min(10, entity.MaxHP - entity.HP);
                        entity.HP += heal;
                        if (heal > 0) Log($"  {entity.Name} regenerates {heal} HP!");
                        break;
                }
                
                // Reduce duration
                entity.Statuses[status.Key]--;
                if (entity.Statuses[status.Key] <= 0)
                {
                    toRemove.Add(status.Key);
                    Log($"  {entity.Name}'s {status.Key} wore off");
                }
            }
            
            foreach (var status in toRemove)
                entity.RemoveStatus(status);
        }
        
        public void RecordAction(BattleAction action)
        {
            actionHistory.Add(action);
        }
        
        public CommandRequest CreateCommandRequest(Entity commander, Entity target, BattleAction action)
        {
            return new CommandRequest
            {
                Commander = commander,
                Target = target,
                Action = action,
                OfferedPayment = CalculateCommandPayment(commander, target, action)
            };
        }
        
        private int CalculateCommandPayment(Entity commander, Entity target, BattleAction action)
        {
            // NPCs pay player to perform actions
            if (target.Type == EntityType.Player && commander.Type == EntityType.NPC)
            {
                int basePayment = 10;
                
                // Modify based on reputation
                if (target.Reputation > 70) basePayment += 5;
                if (target.Reputation < 30) basePayment -= 5;
                
                // Modify based on action type
                if (action is SkillAction skill && skill.Damage > 0)
                    basePayment += 10; // Combat actions pay more
                
                return Math.Max(5, basePayment);
            }
            
            // Player pays to command allies/NPCs
            if (commander.Type == EntityType.Player)
            {
                int baseCost = action.BaseCost;
                
                // Modify based on friendship
                float friendshipMod = 2f - (target.Friendship / 100f); // 0-100 friendship = 2x to 1x cost
                baseCost = (int)(baseCost * friendshipMod);
                
                // Modify based on reputation
                if (commander.Reputation < 30) baseCost += 10;
                if (commander.Reputation > 70) baseCost -= 5;
                
                return Math.Max(0, baseCost);
            }
            
            return 0;
        }
    }
    
    public class CommandRequest
    {
        public Entity Commander { get; set; }
        public Entity Target { get; set; }
        public BattleAction Action { get; set; }
        public int OfferedPayment { get; set; }
        
        public bool Accept(BattleState state)
        {
            if (Commander.Type == EntityType.Player)
            {
                // Player pays to command
                if (state.PlayerMoney >= OfferedPayment)
                {
                    state.PlayerMoney -= OfferedPayment;
                    state.Log($"💰 You pay {OfferedPayment}P to command {Target.Name}");
                    return true;
                }
                state.Log($"⚠️ Not enough money! Need {OfferedPayment}P");
                return false;
            }
            else if (Target.Type == EntityType.Player)
            {
                // NPC pays player
                state.PlayerMoney += OfferedPayment;
                state.Log($"💰 {Commander.Name} pays you {OfferedPayment}P!");
                return true;
            }
            
            return false;
        }
    }
    #endregion
}