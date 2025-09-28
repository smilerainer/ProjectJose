// TurnManager.cs - Manages turn flow and game state
using Godot;

public class TurnManager
{
    #region Dependencies
    
    private BattleStateManager stateManager;
    private BattleUIController uiController;
    
    #endregion
    
    #region Turn State
    
    private bool battleActive = false;
    private int currentRound = 0;
    
    #endregion
    
    #region Initialization
    
    public void Initialize(BattleStateManager stateManager, BattleUIController uiController)
    {
        this.stateManager = stateManager;
        this.uiController = uiController;
        
        GD.Print("[TurnManager] Turn manager initialized");
    }
    
    #endregion
    
    #region Battle Flow
    
    public void StartBattle()
    {
        battleActive = true;
        currentRound = 1;
        stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.PlayerTurn);
        
        GD.Print("[TurnManager] Battle started");
        
        // Start the first player turn after a brief delay
        StartPlayerTurn();
    }
    
    public void StartPlayerTurn()
    {
        stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.ActionSelection);
        uiController.ShowMainMenu();
        
        GD.Print($"[TurnManager] Player turn {currentRound} started");
    }
    
    public void EndPlayerTurn()
    {
        stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.TurnEnd);
        uiController.HideMainMenu();
        
        GD.Print($"[TurnManager] Player turn {currentRound} ended");
        
        // Process turn end effects
        ProcessPlayerTurnEnd();
        
        // Move to enemy turn
        StartEnemyTurn();
    }
    
    public void StartEnemyTurn()
    {
        stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.EnemyTurn);
        
        GD.Print("[TurnManager] Enemy turn started");
        
        // Process enemy actions
        ProcessEnemyActions();
        
        // End enemy turn
        EndEnemyTurn();
    }
    
    public void EndEnemyTurn()
    {
        GD.Print("[TurnManager] Enemy turn ended");
        
        // Process enemy turn end effects
        ProcessEnemyTurnEnd();
        
        // Check for battle end conditions
        if (CheckBattleEndConditions())
        {
            EndBattle();
            return;
        }
        
        // Start next round
        currentRound++;
        StartPlayerTurn();
    }
    
    public void ReturnToActionSelection()
    {
        stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.ActionSelection);
        uiController.EndTargetSelection();
        uiController.ShowMainMenu();
        
        GD.Print("[TurnManager] Returned to action selection");
    }
    
    #endregion
    
    #region Turn Processing
    
    private void ProcessPlayerTurnEnd()
    {
        GD.Print("[TurnManager] Processing player turn end effects");
        
        // Process status effects for player
        stateManager.ProcessTurnEndEffects();
        
        // TODO: Integrate with BattleLogic Entity.ProcessTurnEnd() for player
        // - Process poison/regen effects
        // - Decrease status effect durations
        // - Apply any end-of-turn triggers
    }
    
    private void ProcessEnemyActions()
    {
        GD.Print("[TurnManager] Processing enemy AI actions");
        
        // TODO: Implement enemy AI system
        // For now, enemies skip their turn
        
        // TODO: Integrate with BattleLogic AI system:
        // - Evaluate available actions
        // - Select optimal targets
        // - Execute enemy abilities
        // - Apply AI decision making
    }
    
    private void ProcessEnemyTurnEnd()
    {
        GD.Print("[TurnManager] Processing enemy turn end effects");
        
        // TODO: Integrate with BattleLogic Entity.ProcessTurnEnd() for enemies
        // - Process status effects for all enemies
        // - Apply regeneration/poison/other effects
        // - Decrease status effect durations
    }
    
    private bool CheckBattleEndConditions()
    {
        // Use state manager to check battle end conditions
        var battleEnded = stateManager.CheckBattleEndConditions();
        
        if (battleEnded)
        {
            GD.Print("[TurnManager] Battle end conditions met");
        }
        
        // TODO: Integrate with BattleLogic BattleState.IsGameOver()
        // - Check if player is defeated
        // - Check if all enemies are defeated
        // - Check for scenario-specific victory conditions
        
        return battleEnded;
    }
    
    private void EndBattle()
    {
        battleActive = false;
        stateManager.SetCurrentPhase(BattleStateManager.BattlePhase.BattleEnd);
        
        GD.Print("[TurnManager] Battle ended");
        
        // Determine battle outcome
        DetermineBattleOutcome();
        
        // TODO: Handle battle results
        // - Award experience/money for victory
        // - Handle defeat scenarios
        // - Transition to post-battle state
    }
    
    private void DetermineBattleOutcome()
    {
        // TODO: Implement proper victory/defeat logic
        var playerPosition = stateManager.GetPlayerPosition();
        var enemyPosition = stateManager.GetEnemyPosition();
        
        // For now, just log the outcome
        GD.Print("[TurnManager] Determining battle outcome...");
        
        // TODO: Integrate with BattleLogic:
        // - Check Entity.IsAlive for all participants
        // - Apply scenario-specific victory conditions
        // - Award appropriate rewards
    }
    
    #endregion
    
    #region State Access
    
    public bool IsBattleActive() => battleActive;
    public int GetCurrentRound() => currentRound;
    
    public bool IsPlayerTurn() => 
        stateManager.GetCurrentPhase() == BattleStateManager.BattlePhase.PlayerTurn ||
        stateManager.GetCurrentPhase() == BattleStateManager.BattlePhase.ActionSelection ||
        stateManager.GetCurrentPhase() == BattleStateManager.BattlePhase.TargetSelection;
    
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
        EndPlayerTurn();
    }
    
    #endregion
}