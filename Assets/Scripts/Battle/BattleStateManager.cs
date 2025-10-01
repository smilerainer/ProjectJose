// BattleStateManager.cs - Manages current battle state and entities
using Godot;
using System.Collections.Generic;

public class BattleStateManager
{
    #region State Data
    
    private Vector2I playerPosition = new Vector2I(0, 0);
    private Vector2I enemyPosition = new Vector2I(3, 0);
    private Dictionary<Vector2I, string> entityPositions = new();
    private HexGrid hexGrid;
    
    // Track HP for each entity
    private Dictionary<string, (float current, float max)> entityHP = new();
    
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
        SetupEntities();
        currentPhase = BattlePhase.PlayerTurn;
    }
    
    private void SetupEntities()
    {
        if (hexGrid == null) return;
        
        // Setup player
        hexGrid.SetTileWithCoords(playerPosition, CellLayer.Entity, new Vector2I(0, 0));
        hexGrid.SetCellOccupied(playerPosition, true);
        hexGrid.SetCellMetadata(playerPosition, "description", "Player Character");
        entityPositions[playerPosition] = "player";
        entityHP["player"] = (100f, 100f);
        
        // Setup enemy
        hexGrid.SetTileWithCoords(enemyPosition, CellLayer.Entity, new Vector2I(1, 0));
        hexGrid.SetCellOccupied(enemyPosition, true);
        hexGrid.SetCellMetadata(enemyPosition, "description", "Enemy Warrior");
        entityPositions[enemyPosition] = "enemy";
        entityHP["enemy"] = (80f, 80f);
        
        GD.Print($"[BattleState] Entities placed - Player: 100/100 HP, Enemy: 80/80 HP");
    }
    
    #endregion
    
    #region State Access
    
    public BattlePhase GetCurrentPhase() => currentPhase;
    public void SetCurrentPhase(BattlePhase phase) => currentPhase = phase;
    
    public Vector2I GetPlayerPosition() => playerPosition;
    public Vector2I GetEnemyPosition() => enemyPosition;
    
    public bool IsPlayerCell(Vector2I cell) => cell == playerPosition;
    public bool IsEnemyCell(Vector2I cell) => cell == enemyPosition;
    public bool IsOccupiedCell(Vector2I cell) => entityPositions.ContainsKey(cell);
    
    public Dictionary<Vector2I, string> GetAllEntityPositions() => new(entityPositions);
    
    #endregion
    
    #region Entity Management
    
    public void MovePlayer(Vector2I newPosition)
    {
        if (hexGrid == null) return;
        
        // Clear old position
        hexGrid.ClearTile(playerPosition, CellLayer.Entity);
        hexGrid.SetCellOccupied(playerPosition, false);
        hexGrid.SetCellMetadata(playerPosition, "description", "");
        entityPositions.Remove(playerPosition);
        
        // Set new position
        playerPosition = newPosition;
        hexGrid.SetTileWithCoords(playerPosition, CellLayer.Entity, new Vector2I(0, 0));
        hexGrid.SetCellOccupied(playerPosition, true);
        hexGrid.SetCellMetadata(playerPosition, "description", "Player Character");
        entityPositions[playerPosition] = "player";
        
        GD.Print($"[BattleState] Player moved to {playerPosition}");
    }
    
    public void ApplyDamageToEntity(Vector2I position, float damage)
    {
        var entityType = GetEntityTypeAt(position);
        if (!entityHP.ContainsKey(entityType)) return;
        
        var (current, max) = entityHP[entityType];
        current = Mathf.Max(0, current - damage);
        entityHP[entityType] = (current, max);
        
        GD.Print($"[BattleState] {entityType} at {position} takes {damage} damage -> {current}/{max} HP");
        
        if (current <= 0)
        {
            GD.Print($"[BattleState] {entityType} defeated!");
        }
    }
    
    public void ApplyHealingToEntity(Vector2I position, float healing)
    {
        var entityType = GetEntityTypeAt(position);
        if (!entityHP.ContainsKey(entityType)) return;
        
        var (current, max) = entityHP[entityType];
        current = Mathf.Min(max, current + healing);
        entityHP[entityType] = (current, max);
        
        GD.Print($"[BattleState] {entityType} at {position} heals {healing} HP -> {current}/{max} HP");
    }
    
    public void ApplyStatusEffectToEntity(Vector2I position, string statusEffect)
    {
        // TODO: Integrate with BattleLogic Entity.AddStatus()
        var entityType = GetEntityTypeAt(position);
        GD.Print($"[BattleState] {entityType} at {position} gains {statusEffect} status");
    }
    
    public void RemoveStatusEffectFromEntity(Vector2I position, string statusEffect)
    {
        // TODO: Integrate with BattleLogic Entity.RemoveStatus()
        var entityType = GetEntityTypeAt(position);
        GD.Print($"[BattleState] {entityType} at {position} loses {statusEffect} status");
    }
    
    public void ProcessTurnEndEffects()
    {
        // TODO: Integrate with BattleLogic Entity.ProcessTurnEnd()
        GD.Print("[BattleState] Processing turn end effects for all entities");
    }
    
    public bool CheckBattleEndConditions()
    {
        // Check if player is defeated
        if (entityHP.ContainsKey("player") && entityHP["player"].current <= 0)
            return true;
        
        // Check if all enemies are defeated
        if (entityHP.ContainsKey("enemy") && entityHP["enemy"].current <= 0)
            return true;
        
        return false;
    }
    
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
        
        // Get grid dimensions from HexGrid
        // Assuming HexGrid has a way to get its bounds
        // If not, you may need to add GetGridWidth() and GetGridHeight() methods to HexGrid
        
        // For now, using a reasonable search range
        // Adjust these values based on your actual grid size
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
    
    #region Helper Methods
    
    private string GetEntityTypeAt(Vector2I position)
    {
        if (entityPositions.TryGetValue(position, out string entityType))
            return entityType;
        return "unknown";
    }
    
    #endregion
}