// BattleLogic.cs - Turn-based strategy battle system
using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleLogic
{
    
    #region Core Types
    
    public enum EntityType { Player, Ally, Enemy, NPC, Neutral }
    public enum ActionType { Skill, Move, Talk, Item, Wait }
    public enum TargetType { Self, Single, Area, All, Movement, Enemy, Ally }
    public enum StatusType { Buff, Debuff, Neutral }
    
    public struct Position
    {
        public int X, Y;
        public Position(int x, int y) { X = x; Y = y; }
        
        public int Distance(Position other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        
        public List<Position> GetHexNeighbors()
        {
            // Copy struct members to local variables for use in lambda
            int x = X;
            int y = Y;
            
            var directions = x % 2 == 0
                ? new[] { (0, -1), (1, -1), (1, 0), (0, 1), (-1, 0), (-1, -1) }
                : new[] { (0, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0) };
            
            return directions.Select(d => new Position(x + d.Item1, y + d.Item2)).ToList();
        }
        
        public static Position operator +(Position a, Position b) => new(a.X + b.X, a.Y + b.Y);
        public static bool operator ==(Position a, Position b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Position a, Position b) => !(a == b);
        
        public override bool Equals(object obj) => obj is Position pos && this == pos;
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X},{Y})";
    }
    
    #endregion
    
    #region Status Effects
    
    public class StatusEffect
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public StatusType Type { get; set; }
        public int Duration { get; set; }
        public int Intensity { get; set; }
        
        public StatusEffect(string id, string name, StatusType type, int duration, int intensity = 1)
        {
            Id = id;
            Name = name;
            Type = type;
            Duration = duration;
            Intensity = intensity;
        }
        
        public virtual void ProcessTurnEnd(Entity target, BattleState state) { }
    }
    
    public class PoisonStatus : StatusEffect
    {
        public PoisonStatus(int duration, int damage = 10) 
            : base("poison", "Poison", StatusType.Debuff, duration, damage) { }
        
        public override void ProcessTurnEnd(Entity target, BattleState state)
        {
            target.TakeDamage(Intensity);
            state.Log($"{target.Name} takes {Intensity} poison damage");
        }
    }
    
    public class RegenStatus : StatusEffect
    {
        public RegenStatus(int duration, int healing = 10) 
            : base("regen", "Regeneration", StatusType.Buff, duration, healing) { }
        
        public override void ProcessTurnEnd(Entity target, BattleState state)
        {
            target.Heal(Intensity);
            state.Log($"{target.Name} regenerates {Intensity} HP");
        }
    }
    
    #endregion
    
    #region Entity System
    
    public class Entity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public EntityType Type { get; set; }
        public Position Position { get; set; }
        
        // Core stats - simplified to HP only
        public float HP { get; set; }
        public float MaxHP { get; set; }
        
        // Social mechanics
        public int Friendship { get; set; } = 50;  // 0-100, affects action costs
        public int Reputation { get; set; } = 50;  // 0-100, affects success rates
        
        // Status effects
        public Dictionary<string, StatusEffect> StatusEffects { get; set; } = new();
        
        // Turn state
        public bool HasActed { get; set; } = false;
        
        public Entity(string name, EntityType type, float hp, Position position = default)
        {
            Name = name;
            Type = type;
            HP = MaxHP = hp;
            Position = position;
        }
        
        // Core state
        public bool IsAlive => HP > 0;
        public bool CanAct => IsAlive && !HasStatus("stun");
        
        // Status management
        public bool HasStatus(string statusId) => StatusEffects.ContainsKey(statusId);
        
        public void AddStatus(StatusEffect status)
        {
            if (StatusEffects.ContainsKey(status.Id))
            {
                var existing = StatusEffects[status.Id];
                existing.Duration = Math.Max(existing.Duration, status.Duration);
            }
            else
            {
                StatusEffects[status.Id] = status;
            }
        }
        
        public void RemoveStatus(string statusId) => StatusEffects.Remove(statusId);
        
        // Health management
        public void TakeDamage(float amount) => HP = Math.Max(0, HP - amount);
        public void Heal(float amount) => HP = Math.Min(MaxHP, HP + amount);
        
        // Turn processing
        public void ProcessTurnEnd(BattleState state)
        {
            var statusesToRemove = new List<string>();
            
            foreach (var status in StatusEffects.Values)
            {
                status.ProcessTurnEnd(this, state);
                status.Duration--;
                
                if (status.Duration <= 0)
                    statusesToRemove.Add(status.Id);
            }
            
            foreach (var statusId in statusesToRemove)
                RemoveStatus(statusId);
            
            HasActed = false;
        }
    }
    
    #endregion
    
    #region Action System
    
    public abstract class BattleAction
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ActionType Type { get; set; }
        public TargetType TargetType { get; set; }
        
        // Money cost (shared global resource)
        public int Cost { get; set; } = 0;
        
        // Range and targeting
        public int Range { get; set; } = 1;
        public List<Position> RangePattern { get; set; } = new();
        public List<Position> AOEPattern { get; set; } = new();
        
        protected BattleAction(string id, string name, ActionType type, TargetType targetType)
        {
            Id = id;
            Name = name;
            Type = type;
            TargetType = targetType;
        }
        
        public virtual bool CanExecute(Entity actor, BattleState state)
        {
            return actor.CanAct && state.Money >= GetAdjustedCost(actor, state);
        }
        
        protected virtual int GetAdjustedCost(Entity actor, BattleState state)
        {
            // Friendship affects costs - higher friendship = lower costs
            float friendshipModifier = 1.0f - (actor.Friendship - 50) / 200f; // 50 friendship = 1x, 100 = 0.75x, 0 = 1.25x
            return Math.Max(0, (int)(Cost * friendshipModifier));
        }
        
        public virtual List<Entity> GetValidTargets(Entity actor, BattleState state)
        {
            return state.GetEntitiesInRange(actor.Position, Range)
                .Where(t => IsValidTarget(actor, t, state))
                .ToList();
        }
        
        protected virtual bool IsValidTarget(Entity actor, Entity target, BattleState state)
        {
            switch (TargetType)
            {
                case TargetType.Self: return target == actor;
                case TargetType.Enemy: return state.AreEnemies(actor, target);
                case TargetType.Ally: return state.AreAllies(actor, target);
                default: return target.IsAlive;
            }
        }
        
        public abstract ActionResult Execute(Entity actor, List<Entity> targets, BattleState state);
        
        protected void PayCost(Entity actor, BattleState state)
        {
            state.Money -= GetAdjustedCost(actor, state);
        }
    }
    
    public class ActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        
        public ActionResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }
    }
    
    // Skill Action
    public class SkillAction : BattleAction
    {
        public float Damage { get; set; }
        public float Healing { get; set; }
        public StatusEffect AppliesStatus { get; set; }
        public string RemovesStatus { get; set; }
        
        public SkillAction(string id, string name) : base(id, name, ActionType.Skill, TargetType.Single) { }
        
        public override ActionResult Execute(Entity actor, List<Entity> targets, BattleState state)
        {
            if (!CanExecute(actor, state))
                return new ActionResult(false, $"{actor.Name} cannot use {Name}");
            
            PayCost(actor, state);
            
            var messages = new List<string> { $"{actor.Name} uses {Name}" };
            
            foreach (var target in targets)
            {
                if (Damage > 0)
                {
                    target.TakeDamage(Damage);
                    messages.Add($"{target.Name} takes {Damage} damage");
                    if (!target.IsAlive) messages.Add($"{target.Name} is defeated");
                }
                
                if (Healing > 0)
                {
                    var heal = Math.Min(Healing, target.MaxHP - target.HP);
                    target.Heal(heal);
                    if (heal > 0) messages.Add($"{target.Name} heals {heal} HP");
                }
                
                if (AppliesStatus != null)
                {
                    target.AddStatus(AppliesStatus);
                    messages.Add($"{target.Name} is affected by {AppliesStatus.Name}");
                }
                
                if (!string.IsNullOrEmpty(RemovesStatus) && target.HasStatus(RemovesStatus))
                {
                    target.RemoveStatus(RemovesStatus);
                    messages.Add($"{target.Name}'s {RemovesStatus} is removed");
                }
            }
            
            actor.HasActed = true;
            var result = new ActionResult(true, string.Join(", ", messages));
            state.Log(result.Message);
            return result;
        }
    }
    
    // Move Action
    public class MoveAction : BattleAction
    {
        public MoveAction(string id, string name, int range) : base(id, name, ActionType.Move, TargetType.Movement)
        {
            Range = range;
        }
        
        public override List<Entity> GetValidTargets(Entity actor, BattleState state)
        {
            var validPositions = new List<Entity>();
            var positions = actor.Position.GetHexNeighbors()
                .Where(pos => state.IsValidPosition(pos) && !state.IsOccupied(pos))
                .Take(Range);
            
            foreach (var pos in positions)
            {
                var dummy = new Entity($"Position {pos}", EntityType.Neutral, 0, pos);
                validPositions.Add(dummy);
            }
            
            return validPositions;
        }
        
        public override ActionResult Execute(Entity actor, List<Entity> targets, BattleState state)
        {
            if (!CanExecute(actor, state))
                return new ActionResult(false, $"{actor.Name} cannot move");
            
            var destination = targets.FirstOrDefault();
            if (destination == null)
                return new ActionResult(false, "Invalid destination");
            
            PayCost(actor, state);
            
            var oldPos = actor.Position;
            state.MoveEntity(actor, destination.Position);
            actor.HasActed = true;
            
            var message = $"{actor.Name} moves from {oldPos} to {actor.Position}";
            state.Log(message);
            return new ActionResult(true, message);
        }
    }
    
    // Talk Action
    public class TalkAction : BattleAction
    {
        public string Dialogue { get; set; }
        public int FriendshipChange { get; set; }
        public int ReputationChange { get; set; }
        public int MoneyReward { get; set; }
        
        public TalkAction(string id, string name, string dialogue) : base(id, name, ActionType.Talk, TargetType.Single)
        {
            Dialogue = dialogue;
        }
        
        public override ActionResult Execute(Entity actor, List<Entity> targets, BattleState state)
        {
            if (!CanExecute(actor, state))
                return new ActionResult(false, $"{actor.Name} cannot talk");
            
            PayCost(actor, state);
            
            var messages = new List<string> { $"{actor.Name}: \"{Dialogue}\"" };
            
            foreach (var target in targets)
            {
                if (FriendshipChange != 0)
                {
                    target.Friendship = Math.Clamp(target.Friendship + FriendshipChange, 0, 100);
                    messages.Add($"{target.Name} friendship {(FriendshipChange > 0 ? "+" : "")}{FriendshipChange}");
                }
                
                if (ReputationChange != 0)
                {
                    actor.Reputation = Math.Clamp(actor.Reputation + ReputationChange, 0, 100);
                    messages.Add($"{actor.Name} reputation {(ReputationChange > 0 ? "+" : "")}{ReputationChange}");
                }
            }
            
            if (MoneyReward != 0)
            {
                state.Money += MoneyReward;
                messages.Add($"Money {(MoneyReward > 0 ? "+" : "")}{MoneyReward}");
            }
            
            actor.HasActed = true;
            var result = new ActionResult(true, string.Join(", ", messages));
            state.Log(result.Message);
            return result;
        }
    }
    
    // Item Action
    public class ItemAction : BattleAction
    {
        public float HealAmount { get; set; }
        public float DamageAmount { get; set; }
        public StatusEffect AppliesStatus { get; set; }
        public string RemovesStatus { get; set; }
        public int UsesRemaining { get; set; }
        
        public ItemAction(string id, string name, int uses) : base(id, name, ActionType.Item, TargetType.Single)
        {
            UsesRemaining = uses;
        }
        
        public override bool CanExecute(Entity actor, BattleState state)
        {
            return base.CanExecute(actor, state) && UsesRemaining > 0;
        }
        
        public override ActionResult Execute(Entity actor, List<Entity> targets, BattleState state)
        {
            if (!CanExecute(actor, state))
                return new ActionResult(false, $"{actor.Name} cannot use {Name}");
            
            PayCost(actor, state);
            UsesRemaining--;
            
            var messages = new List<string> { $"{actor.Name} uses {Name}" };
            
            foreach (var target in targets)
            {
                if (HealAmount > 0)
                {
                    var heal = Math.Min(HealAmount, target.MaxHP - target.HP);
                    target.Heal(heal);
                    if (heal > 0) messages.Add($"{target.Name} heals {heal} HP");
                }
                
                if (DamageAmount > 0)
                {
                    target.TakeDamage(DamageAmount);
                    messages.Add($"{target.Name} takes {DamageAmount} damage");
                }
                
                if (AppliesStatus != null)
                {
                    target.AddStatus(AppliesStatus);
                    messages.Add($"{target.Name} is affected by {AppliesStatus.Name}");
                }
                
                if (!string.IsNullOrEmpty(RemovesStatus) && target.HasStatus(RemovesStatus))
                {
                    target.RemoveStatus(RemovesStatus);
                    messages.Add($"{target.Name}'s {RemovesStatus} is removed");
                }
            }
            
            messages.Add($"{Name} remaining: {UsesRemaining}");
            
            actor.HasActed = true;
            var result = new ActionResult(true, string.Join(", ", messages));
            state.Log(result.Message);
            return result;
        }
    }
    
    #endregion
    
    #region Battle State
    
    public class BattleState
    {
        private List<Entity> entities = new();
        private Dictionary<Position, Entity> occupiedPositions = new();
        private int round = 0;
        
        // Core game resource - shared money pool
        public int Money { get; set; } = 100;
        
        public Action<string> LogHandler { get; set; }
        
        // Grid properties
        public int GridWidth { get; set; } = 20;
        public int GridHeight { get; set; } = 20;
        
        // Entity management
        public void AddEntity(Entity entity)
        {
            entities.Add(entity);
            occupiedPositions[entity.Position] = entity;
        }
        
        public List<Entity> GetAllEntities() => entities.ToList();
        public List<Entity> GetAliveEntities() => entities.Where(e => e.IsAlive).ToList();
        public Entity GetPlayer() => entities.FirstOrDefault(e => e.Type == EntityType.Player);
        
        public bool AreEnemies(Entity a, Entity b)
        {
            return (a.Type == EntityType.Player || a.Type == EntityType.Ally) && b.Type == EntityType.Enemy ||
                   a.Type == EntityType.Enemy && (b.Type == EntityType.Player || b.Type == EntityType.Ally);
        }
        
        public bool AreAllies(Entity a, Entity b)
        {
            return a.Type == b.Type || 
                   (a.Type == EntityType.Player && b.Type == EntityType.Ally) ||
                   (a.Type == EntityType.Ally && b.Type == EntityType.Player);
        }
        
        // Position management
        public bool IsValidPosition(Position pos) => pos.X >= 0 && pos.X < GridWidth && pos.Y >= 0 && pos.Y < GridHeight;
        public bool IsOccupied(Position pos) => occupiedPositions.ContainsKey(pos);
        
        public List<Entity> GetEntitiesInRange(Position center, int range)
        {
            return entities.Where(e => e.IsAlive && center.Distance(e.Position) <= range).ToList();
        }
        
        public void MoveEntity(Entity entity, Position newPosition)
        {
            occupiedPositions.Remove(entity.Position);
            entity.Position = newPosition;
            occupiedPositions[newPosition] = entity;
        }
        
        // Battle flow
        public bool IsGameOver()
        {
            var player = GetPlayer();
            if (player?.IsAlive != true) return true;
            return !entities.Any(e => e.Type == EntityType.Enemy && e.IsAlive);
        }
        
        public List<Entity> GetTurnOrder()
        {
            round++;
            Log($"Round {round}");
            
            return entities.Where(e => e.IsAlive && e.CanAct).ToList();
        }
        
        public void EndTurn(Entity entity) => entity.ProcessTurnEnd(this);
        
        public void Log(string message) => LogHandler?.Invoke(message);
    }
    
    #endregion
    
    #region Battle Controller
    
    public class BattleController
    {
        public BattleState State { get; private set; }
        public Entity CurrentActor { get; private set; }
        public bool BattleActive { get; private set; }
        
        public BattleController(BattleState state) => State = state;
        
        public void StartBattle()
        {
            BattleActive = true;
            State.Log("Battle started");
        }
        
        public bool ExecuteAction(Entity actor, BattleAction action, List<Entity> targets)
        {
            if (!BattleActive) return false;
            
            var result = action.Execute(actor, targets, State);
            
            if (result.Success && State.IsGameOver())
            {
                EndBattle();
            }
            
            return result.Success;
        }
        
        public void EndBattle()
        {
            BattleActive = false;
            var player = State.GetPlayer();
            State.Log(player?.IsAlive == true ? "Victory" : "Defeat");
        }
    }
    
    #endregion
}