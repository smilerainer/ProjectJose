// BattleManager.cs - Minimal battle system using existing HexGrid and MenuControls
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class BattleManager : Node
{
    [Export] private HexGrid hexGrid;
    [Export] private MenuControls menuControls;
    [Export] private string saveFilePath = "user://battle_save.json";
    
    private List<BattleEntity> entities = new();
    private BattleEntity currentEntity;
    private int currentTurnIndex = 0;
    private int round = 0;
    
    public override void _Ready()
    {
        InitializeBattle();
        ConnectSignals();
        LoadBattleData();
        StartTurn();
    }
    
    private void InitializeBattle()
    {
        // Create player
        var player = new BattleEntity
        {
            Name = "Hero",
            Type = EntityType.Player,
            HP = 100,
            Position = new Vector2I(0, 0),
            Speed = 10
        };
        
        // Create enemy
        var enemy = new BattleEntity
        {
            Name = "Goblin", 
            Type = EntityType.Enemy,
            HP = 50,
            Position = new Vector2I(3, 0),
            Speed = 8
        };
        
        entities.Add(player);
        entities.Add(enemy);
        
        // Place entities on hex grid
        hexGrid.SetTile(player.Position, CellLayer.Entity, 0);
        hexGrid.SetTile(enemy.Position, CellLayer.Entity, 1);
        hexGrid.SetOccupied(player.Position);
        hexGrid.SetOccupied(enemy.Position);
        
        GD.Print("[Battle] Initialized - Player at (0,0), Enemy at (3,0)");
    }
    
    private void ConnectSignals()
    {
        if (menuControls != null)
        {
            menuControls.ButtonActivated += OnButtonActivated;
        }
        
        if (hexGrid != null)
        {
            hexGrid.CellSelected += OnCellSelected;
        }
    }
    
    private void StartTurn()
    {
        // Sort by speed for turn order
        var turnOrder = entities.Where(e => e.HP > 0).OrderByDescending(e => e.Speed).ToList();
        
        if (currentTurnIndex >= turnOrder.Count)
        {
            round++;
            currentTurnIndex = 0;
            GD.Print($"[Battle] === ROUND {round} ===");
        }
        
        currentEntity = turnOrder[currentTurnIndex];
        GD.Print($"[Battle] {currentEntity.Name}'s turn");
        
        if (currentEntity.Type == EntityType.Player)
        {
            ShowPlayerMenu();
        }
        else
        {
            ProcessAITurn();
        }
    }
    
    private void ShowPlayerMenu()
    {
        menuControls?.SetActive(true);
        GD.Print("[Battle] Choose action: Move, Attack, Defend, Skip");
    }
    
    private void OnButtonActivated(int index, BaseButton button)
    {
        menuControls?.SetActive(false);
        
        switch (index)
        {
            case 0: // Move
                GD.Print("[Battle] Select destination");
                hexGrid.Clear();
                ShowMoveRange();
                break;
            case 1: // Attack
                GD.Print("[Battle] Attack selected");
                EndTurn();
                break;
            case 2: // Defend  
                GD.Print("[Battle] Defend selected");
                EndTurn();
                break;
            case 3: // Skip
                GD.Print("[Battle] Turn skipped");
                EndTurn();
                break;
        }
    }
    
    private void ShowMoveRange()
    {
        var moveRange = hexGrid.GetReachable(currentEntity.Position, 2);
        var validMoves = moveRange.Where(pos => !hexGrid.IsOccupied(pos) && pos != currentEntity.Position).ToList();
        hexGrid.Highlight(validMoves, 2); // Highlight valid moves
    }
    
    private void OnCellSelected(Vector2I cell)
    {
        if (currentEntity?.Type != EntityType.Player) return;
        
        if (hexGrid.IsWalkable(cell) && hexGrid.GetDistance(currentEntity.Position, cell) <= 2)
        {
            // Move entity
            hexGrid.SetOccupied(currentEntity.Position, false);
            hexGrid.ClearTile(currentEntity.Position, CellLayer.Entity);
            
            currentEntity.Position = cell;
            
            hexGrid.SetTile(cell, CellLayer.Entity, 0);
            hexGrid.SetOccupied(cell);
            
            GD.Print($"[Battle] {currentEntity.Name} moved to {cell}");
            SaveBattleData();
            EndTurn();
        }
    }
    
    private void ProcessAITurn()
    {
        // Simple AI: move toward player
        var player = entities.FirstOrDefault(e => e.Type == EntityType.Player);
        if (player != null)
        {
            var neighbors = hexGrid.GetNeighbors(currentEntity.Position);
            if (neighbors.Count > 0)
            {
                // Find closest move to player
                var bestMove = neighbors.OrderBy(pos => hexGrid.GetDistance(pos, player.Position)).First();
                
                // Move AI entity
                hexGrid.SetOccupied(currentEntity.Position, false);
                hexGrid.ClearTile(currentEntity.Position, CellLayer.Entity);
                
                currentEntity.Position = bestMove;
                
                hexGrid.SetTile(bestMove, CellLayer.Entity, 1);
                hexGrid.SetOccupied(bestMove);
                
                GD.Print($"[Battle] {currentEntity.Name} moved to {bestMove}");
            }
        }
        
        CallDeferred(nameof(EndTurn));
    }
    
    private void EndTurn()
    {
        hexGrid.Clear();
        currentTurnIndex++;
        CallDeferred(nameof(StartTurn));
    }
    
    private void SaveBattleData()
    {
        var data = new Godot.Collections.Dictionary
        {
            ["round"] = round,
            ["currentTurnIndex"] = currentTurnIndex,
            ["entities"] = new Godot.Collections.Array()
        };
        
        var entitiesArray = data["entities"].AsGodotArray();
        foreach (var entity in entities)
        {
            entitiesArray.Add(new Godot.Collections.Dictionary
            {
                ["name"] = entity.Name,
                ["type"] = (int)entity.Type,
                ["hp"] = entity.HP,
                ["posX"] = entity.Position.X,
                ["posY"] = entity.Position.Y,
                ["speed"] = entity.Speed
            });
        }
        
        var file = FileAccess.Open(saveFilePath, FileAccess.ModeFlags.Write);
        file?.StoreString(Json.Stringify(data));
        file?.Close();
        
        GD.Print("[Battle] Game saved");
    }
    
    private void LoadBattleData()
    {
        if (!FileAccess.FileExists(saveFilePath)) return;
        
        var file = FileAccess.Open(saveFilePath, FileAccess.ModeFlags.Read);
        var json = Json.ParseString(file?.GetAsText() ?? "");
        file?.Close();
        
        if (json.VariantType == Variant.Type.Dictionary)
        {
            var data = json.AsGodotDictionary();
            round = data.GetValueOrDefault("round", 0).AsInt32();
            currentTurnIndex = data.GetValueOrDefault("currentTurnIndex", 0).AsInt32();
            
            GD.Print("[Battle] Game loaded");
        }
    }
}

public enum EntityType { Player, Enemy }

public class BattleEntity
{
    public string Name { get; set; }
    public EntityType Type { get; set; }
    public float HP { get; set; }
    public Vector2I Position { get; set; }
    public float Speed { get; set; }
}