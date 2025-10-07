// TurnManager.cs - Manages turn flow with initiative-based turn order
using Godot;
using System.Collections.Generic;
using System.Linq;

public class TurnManager
{
    #region Dependencies

    private BattleStateManager stateManager;
    private BattleUIController uiController;
    private NPCBehaviorManager npcBehaviorManager;

    #endregion
    
    #region Events

    public System.Action<bool> OnBattleEnded; // bool = playerWon
    
    #endregion

    #region Turn State

    private bool battleActive = false;
    private int currentRound = 0;
    private List<Entity> turnOrder = new();
    private int currentTurnIndex = 0;

    #endregion

    #region Initialization

    public void Initialize(BattleStateManager stateManager, BattleUIController uiController)
    {
        this.stateManager = stateManager;
        this.uiController = uiController;

        GD.Print("[TurnManager] Turn manager initialized");
    }

    public void SetNPCBehaviorManager(NPCBehaviorManager manager)
    {
        this.npcBehaviorManager = manager;
        GD.Print("[TurnManager] NPC behavior manager connected");
    }

    #endregion

    #region Battle Flow

    public void StartBattle()
    {
        battleActive = true;
        currentRound = 1;
        stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.PlayerTurn);

        GD.Print("[TurnManager] Battle started");

        StartNextRound();
    }

    private void StartNextRound()
    {
        currentRound++;
        currentTurnIndex = 0;

        // Calculate turn order based on initiative
        turnOrder = CalculateTurnOrder();

        GD.Print($"[TurnManager] === Round {currentRound} Started ===");
        GD.Print($"[TurnManager] Turn order ({turnOrder.Count} entities):");
        for (int i = 0; i < turnOrder.Count; i++)
        {
            GD.Print($"  {i + 1}. {turnOrder[i].Name} (Initiative: {turnOrder[i].Initiative})");
        }

        ProcessNextTurn();
    }

    private List<Entity> CalculateTurnOrder()
    {
        return stateManager.GetAliveEntities()
            .Where(e => e.CanAct)
            .OrderByDescending(e => e.Initiative)
            .ThenByDescending(e => e.Speed)
            .ToList();
    }

    private void ProcessNextTurn()
    {
        if (currentTurnIndex >= turnOrder.Count)
        {
            EndRound();
            return;
        }

        var currentEntity = turnOrder[currentTurnIndex];

        if (!currentEntity.IsAlive || !currentEntity.CanAct)
        {
            GD.Print($"[TurnManager] {currentEntity.Name} skips turn (not alive or cannot act)");
            currentTurnIndex++;
            ProcessNextTurn();
            return;
        }

        GD.Print($"[TurnManager] --- {currentEntity.Name}'s turn ---");

        if (currentEntity.Type == EntityType.Player)
        {
            StartPlayerTurn();
        }
        else
        {
            StartNPCTurn(currentEntity);
        }
    }

    private void StartPlayerTurn()
    {
        stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.ActionSelection);
        uiController.ShowMainMenu();

        GD.Print($"[TurnManager] Player turn {currentRound} started");
    }

    private void StartNPCTurn(Entity entity)
    {
        stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.EnemyTurn);
        
        GD.Print($"[TurnManager] {entity.Name} ({entity.Type}) turn started");
        GD.Print($"[TurnManager DEBUG] npcBehaviorManager is null: {npcBehaviorManager == null}"); // ← Add this
        
        if (npcBehaviorManager != null)
        {
            var decision = npcBehaviorManager.GetDecisionForEntity(entity);
            npcBehaviorManager.ExecuteDecision(entity, decision);
        }
        else
        {
            GD.PrintErr("[TurnManager] NPC behavior manager not initialized!");
        }
        
        EndCurrentTurn();
    }

    public void EndPlayerTurn()
    {
        stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.TurnEnd);
        uiController.HideMainMenu();

        GD.Print($"[TurnManager] Player turn ended");

        EndCurrentTurn();
    }

    private void EndCurrentTurn()
    {
        if (currentTurnIndex < turnOrder.Count)
        {
            var entity = turnOrder[currentTurnIndex];
            entity.HasActedThisTurn = true;
        }

        // Check for battle end
        if (CheckBattleEndConditions())
        {
            EndBattle();
            return;
        }

        // Move to next turn
        currentTurnIndex++;
        ProcessNextTurn();
    }

    private void EndRound()
    {
        GD.Print($"[TurnManager] === Round {currentRound} Ended ===");

        // Process turn end effects for all entities
        stateManager.ProcessTurnEndEffects();

        // Check for battle end after processing effects
        if (CheckBattleEndConditions())
        {
            EndBattle();
            return;
        }

        // Start next round
        StartNextRound();
    }

    public void ReturnToActionSelection()
    {
        stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.ActionSelection);
        uiController.EndTargetSelection();
        uiController.ShowMainMenu();

        GD.Print("[TurnManager] Returned to action selection");
    }

    #endregion

    #region Battle End

    private bool CheckBattleEndConditions()
    {
        var battleEnded = stateManager.CheckBattleEndConditions();

        if (battleEnded)
        {
            GD.Print("[TurnManager] Battle end conditions met");
        }

        return battleEnded;
    }

    private void EndBattle()
    {
        battleActive = false;
        stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.BattleEnd);

        GD.Print("[TurnManager] Battle ended");

        DetermineBattleOutcome();
    }

    private void DetermineBattleOutcome()
    {
        var player = stateManager.GetPlayer();
        bool playerWon = player?.IsAlive == true;

        if (playerWon)
        {
            GD.Print("[TurnManager] === VICTORY ===");
        }
        else
        {
            GD.Print("[TurnManager] === DEFEAT ===");
        }
        
        // Notify BattleManager
        OnBattleEnded?.Invoke(playerWon);
    }

    #endregion

    #region State Access

    public bool IsBattleActive() => battleActive;
    public int GetCurrentRound() => currentRound;
    public Entity GetCurrentActor() => currentTurnIndex < turnOrder.Count ? turnOrder[currentTurnIndex] : null;

    public bool IsPlayerTurn()
    {
        var currentActor = GetCurrentActor();
        return currentActor?.Type == EntityType.Player;
    }

    public bool IsEnemyTurn() =>
        stateManager.GetCurrentPhase() == BattleStateManager.BattlePhase.EnemyTurn;

    #endregion

    #region Debug Methods

    public void ForceEndBattle()
    {
        GD.Print("[TurnManager] Force ending battle (debug)");
        EndBattle();
    }

    public void SkipToNextRound()
    {
        GD.Print("[TurnManager] Skipping to next round (debug)");
        EndRound();
    }

    public void PrintTurnOrder()
    {
        GD.Print("[TurnManager] Current turn order:");
        for (int i = 0; i < turnOrder.Count; i++)
        {
            var marker = i == currentTurnIndex ? ">>> " : "    ";
            GD.Print($"{marker}{i + 1}. {turnOrder[i].Name} (Initiative: {turnOrder[i].Initiative})");
        }
    }

    #endregion
}