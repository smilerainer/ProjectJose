// BattleStateManager.cs - Manages current battle state and entities (Updated with Entity System)
using Godot;
using System.Collections.Generic;
using System.Linq;
using CustomJsonSystem;

public class BattleStateManager
{
    #region State Data
    
    private List<Entity> allEntities = new();
    private Dictionary<Vector2I, Entity> entityPositionMap = new();
    private HexGrid hexGrid;
    
    #endregion
    
    #region Battle Flow State
    
    public enum BattlePhase
    {
        Setup,
        PlayerTurn,
        EnemyTurn,
        ActionSelection,
        TargetSelection,
        ActionExecution,
        TurnEnd,
        BattleEnd
    }
    
    private BattlePhase currentPhase = BattlePhase.Setup;
    
    #endregion
    
    #region Initialization
    
    public void Initialize(BattleManager battleManager)
    {
        FindRequiredNodes(battleManager);
    }
    
    private void FindRequiredNodes(BattleManager battleManager)
    {
        hexGrid = battleManager.GetNode<HexGrid>("../HexGrid");
    }
    
    public void SetupInitialBattleState()
    {
        SetupDefaultEntities();
        currentPhase = BattlePhase.PlayerTurn;
    }
    
    public void SetupBattleFromConfig(BattleConfigData config)
    {
        ClearAllEntities();
        
        if (config.Entities != null && config.Entities.Count > 0)
        {
            foreach (var entityDef in config.Entities)
            {
                var entity = Entity.FromDefinition(entityDef);
                AddEntity(entity);
            }
            GD.Print($"[BattleState] Loaded {allEntities.Count} entities from config");
        }
        else
        {
            SetupDefaultEntities();
        }
        
        currentPhase = BattlePhase.PlayerTurn;
    }
    
    private void SetupDefaultEntities()
    {
        // Default player
        var player = new Entity(
            "player_1",
            "Player",
            EntityType.Player,
            new Vector2I(0, 0),
            100f,
            50,
            5
        );
        AddEntity(player);
        
        // Default enemy
        var enemy = new Entity(
            "enemy_1",
            "Enemy Warrior",
            EntityType.Enemy,
            new Vector2I(3, 0),
            80f,
            45,
            4
        );
        enemy.BehaviorType = "balanced";
        enemy.AvailableSkills = new List<string> { "direct_hit" };  // ← Add this line
        AddEntity(enemy);
        
        GD.Print($"[BattleState] Setup default entities - Player: 100 HP, Enemy: 80 HP");
    }
        
    #endregion
    
    #region Entity Management
    
    public void AddEntity(Entity entity)
    {
        if (entity == null) return;
        
        allEntities.Add(entity);
        entityPositionMap[entity.Position] = entity;
        
        if (hexGrid != null)
        {
            var atlasCoords = GetAtlasCoordsForEntityType(entity.Type);
            hexGrid.SetTileWithCoords(entity.Position, CellLayer.Entity, atlasCoords);
            hexGrid.SetCellOccupied(entity.Position, true);
            hexGrid.SetCellMetadata(entity.Position, "description", entity.Name);
        }
        
        GD.Print($"[BattleState] Added entity: {entity.Name} ({entity.Type}) at {entity.Position}");
    }
    
    public void RemoveEntity(Entity entity)
    {
        if (entity == null) return;
        
        allEntities.Remove(entity);
        entityPositionMap.Remove(entity.Position);
        
        if (hexGrid != null)
        {
            hexGrid.ClearTile(entity.Position, CellLayer.Entity);
            hexGrid.SetCellOccupied(entity.Position, false);
            hexGrid.SetCellMetadata(entity.Position, "description", "");
        }
        
        GD.Print($"[BattleState] Removed entity: {entity.Name}");
    }
    
    public void ClearAllEntities()
    {
        foreach (var entity in allEntities.ToList())
        {
            RemoveEntity(entity);
        }
        allEntities.Clear();
        entityPositionMap.Clear();
    }
    
    public void MoveEntity(Entity entity, Vector2I newPosition)
    {
        if (entity == null || hexGrid == null) return;
        
        var oldPosition = entity.Position;
        
        // Clear old position
        hexGrid.ClearTile(oldPosition, CellLayer.Entity);
        hexGrid.SetCellOccupied(oldPosition, false);
        hexGrid.SetCellMetadata(oldPosition, "description", "");
        entityPositionMap.Remove(oldPosition);
        
        // Update entity
        entity.Position = newPosition;
        
        // Set new position
        var atlasCoords = GetAtlasCoordsForEntityType(entity.Type);
        hexGrid.SetTileWithCoords(newPosition, CellLayer.Entity, atlasCoords);
        hexGrid.SetCellOccupied(newPosition, true);
        hexGrid.SetCellMetadata(newPosition, "description", entity.Name);
        entityPositionMap[newPosition] = entity;
        
        GD.Print($"[BattleState] {entity.Name} moved from {oldPosition} to {newPosition}");
    }
    
    private Vector2I GetAtlasCoordsForEntityType(EntityType type)
    {
        return type switch
        {
            EntityType.Player => new Vector2I(0, 0),
            EntityType.Ally => new Vector2I(2, 0),
            EntityType.Enemy => new Vector2I(1, 0),
            EntityType.NPC => new Vector2I(3, 0),
            EntityType.Neutral => new Vector2I(4, 0),
            _ => new Vector2I(0, 0)
        };
    }
    
    #endregion
    
    #region Entity Queries
    
    public List<Entity> GetAllEntities() => new List<Entity>(allEntities);
    public List<Entity> GetAliveEntities() => allEntities.Where(e => e.IsAlive).ToList();
    public Entity GetPlayer() => allEntities.FirstOrDefault(e => e.Type == EntityType.Player);
    public Entity GetEntityAt(Vector2I position) => entityPositionMap.GetValueOrDefault(position);
    
    public List<Entity> GetEntitiesOfType(EntityType type)
    {
        return allEntities.Where(e => e.Type == type && e.IsAlive).ToList();
    }
    
    public bool IsOccupiedCell(Vector2I cell) => entityPositionMap.ContainsKey(cell);
    
    #endregion
    
    #region Legacy Compatibility (for existing code)
    
    public Vector2I GetPlayerPosition() => GetPlayer()?.Position ?? Vector2I.Zero;
    public Vector2I GetEnemyPosition() => allEntities.FirstOrDefault(e => e.Type == EntityType.Enemy)?.Position ?? Vector2I.Zero;
    
    public bool IsPlayerCell(Vector2I cell)
    {
        var entity = GetEntityAt(cell);
        return entity?.Type == EntityType.Player;
    }
    
    public bool IsEnemyCell(Vector2I cell)
    {
        var entity = GetEntityAt(cell);
        return entity?.Type == EntityType.Enemy;
    }
    
    public void MovePlayer(Vector2I newPosition)
    {
        var player = GetPlayer();
        if (player != null)
        {
            MoveEntity(player, newPosition);
        }
    }
    
    #endregion
    
    #region Combat Actions
    
    public void ApplyDamageToEntity(Vector2I position, float damage)
    {
        var entity = GetEntityAt(position);
        if (entity == null) return;
        
        entity.TakeDamage(damage);
        GD.Print($"[BattleState] {entity.Name} at {position} takes {damage} damage -> {entity.CurrentHP}/{entity.MaxHP} HP");
        
        if (!entity.IsAlive)
        {
            GD.Print($"[BattleState] {entity.Name} defeated!");
            OnEntityDefeated(entity);
        }
    }
    
    public void ApplyHealingToEntity(Vector2I position, float healing)
    {
        var entity = GetEntityAt(position);
        if (entity == null) return;
        
        entity.Heal(healing);
        GD.Print($"[BattleState] {entity.Name} at {position} heals {healing} HP -> {entity.CurrentHP}/{entity.MaxHP} HP");
    }
    
    public void ApplyStatusEffectToEntity(Vector2I position, string statusEffect)
    {
        var entity = GetEntityAt(position);
        if (entity == null) return;
        
        var status = new StatusEffect(statusEffect, 3, 0, 0);
        entity.AddStatus(status);
        GD.Print($"[BattleState] {entity.Name} at {position} gains {statusEffect} status");
    }
    
    public void RemoveStatusEffectFromEntity(Vector2I position, string statusEffect)
    {
        var entity = GetEntityAt(position);
        if (entity == null) return;
        
        entity.RemoveStatus(statusEffect);
        GD.Print($"[BattleState] {entity.Name} at {position} loses {statusEffect} status");
    }
    
    private void OnEntityDefeated(Entity entity)
    {
        // Keep entity in list for now, just marked as dead
        // Remove visual representation after a delay if needed
    }
    
    #endregion
    
    #region Turn Processing
    
    public void ProcessTurnEndEffects()
    {
        foreach (var entity in allEntities.Where(e => e.IsAlive))
        {
            entity.ProcessTurnEnd();
        }
        GD.Print("[BattleState] Processed turn end effects for all entities");
    }
    
    public bool CheckBattleEndConditions()
    {
        var player = GetPlayer();
        if (player == null || !player.IsAlive)
            return true;
        
        var aliveEnemies = allEntities.Any(e => e.Type == EntityType.Enemy && e.IsAlive);
        if (!aliveEnemies)
            return true;
        
        return false;
    }
    
    #endregion
    
    #region State Access
    
    public BattlePhase GetCurrentPhase() => currentPhase;
    public void SetCurrentPhase(BattlePhase phase) => currentPhase = phase;
    
    #endregion
    
    #region Grid Access
    
    public HexGrid GetHexGrid() => hexGrid;
    
    public bool IsValidGridPosition(Vector2I cell)
    {
        return hexGrid?.IsValidCell(cell) ?? false;
    }
    
    public List<Vector2I> GetAllValidGridPositions()
    {
        var positions = new List<Vector2I>();
        
        if (hexGrid == null)
            return positions;
        
        int maxSearch = 20;
        
        for (int x = -maxSearch; x <= maxSearch; x++)
        {
            for (int y = -maxSearch; y <= maxSearch; y++)
            {
                var cell = new Vector2I(x, y);
                if (hexGrid.IsValidCell(cell))
                {
                    positions.Add(cell);
                }
            }
        }
        
        return positions;
    }
    
    #endregion
}