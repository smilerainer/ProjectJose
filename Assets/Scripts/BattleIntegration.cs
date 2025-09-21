// GodotBattleIntegration.cs - Godot wrapper for Enhanced Battle System
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using EnhancedBattleSystem;

namespace GodotBattleIntegration
{
    // Interfaces for your existing systems
    public interface IHexGridProvider
    {
        Vector2I GetSelectedCell();
        void SetCursor(Vector2I position);
        void HighlightCells(List<Vector2I> cells, int tileId);
        void ClearHighlights();
        Vector2 CellToWorld(Vector2I cell);
    }
    
    public interface IMenuProvider
    {
        void ShowMenu(string menuId, List<string> options);
        void HideMenu(string menuId);
        void SetMenuOptions(string menuId, List<string> options);
    }
    
    // Main battle integration that works with your hex grid and menu systems
    public partial class BattleIntegration : Node
    {
        [Export] private NodePath hexGridPath;
        [Export] private NodePath inputManagerPath;
        [Export] private NodePath infoDisplayPath;
        
        private Node hexGrid;  // Your actual HexGrid node
        private Node inputManager;  // Your InputManager node
        private BattleInfoDisplay infoDisplay;
        
        private BattleState battleState;
        private Dictionary<string, BattleAction> actionLibrary;
        private Dictionary<Vector2I, Entity> entityPositions = new();
        
        // Current turn state
        private Entity currentActor;
        private BattleAction pendingAction;
        private List<Entity> pendingTargets = new();
        private bool waitingForInput = false;
        
        public override void _Ready()
        {
            hexGrid = GetNode(hexGridPath);
            inputManager = GetNode(inputManagerPath);
            infoDisplay = GetNode<BattleInfoDisplay>(infoDisplayPath);
            
            InitializeBattle();
            ConnectSignals();
        }
        
        private void InitializeBattle()
        {
            battleState = new BattleState();
            battleState.LogHandler = (msg) => infoDisplay?.LogBattle(msg);
            
            actionLibrary = new Dictionary<string, BattleAction>();
            LoadActions();
        }
        
        private void ConnectSignals()
        {
            // Connect to your HexGrid signals
            if (hexGrid.HasSignal("CellSelected"))
            {
                hexGrid.Connect("CellSelected", new Callable(this, nameof(OnHexSelected)));
            }
            
            // Connect to InputManager for menu callbacks
            if (inputManager.HasSignal("MenuOptionSelected"))
            {
                inputManager.Connect("MenuOptionSelected", new Callable(this, nameof(OnMenuOptionSelected)));
            }
            
            // Connect to BattleInfoDisplay for prompt responses
            if (infoDisplay != null)
            {
                infoDisplay.ResponseGiven += OnPromptResponse;
            }
        }
        
        #region Turn Management
        
        public void StartBattle()
        {
            ProcessNextTurn();
        }
        
        private void ProcessNextTurn()
        {
            if (battleState.IsGameOver())
            {
                EndBattle();
                return;
            }
            
            var turnOrder = battleState.GetTurnOrder();
            if (turnOrder.Count == 0) return;
            
            currentActor = turnOrder.FirstOrDefault();
            if (currentActor == null) return;
            
            infoDisplay?.LogTurn(currentActor.Name, currentActor.Type.ToString());
            
            // Handle based on entity type
            switch (currentActor.Type)
            {
                case EntityType.Player:
                    ShowActionMenu();
                    break;
                    
                case EntityType.Ally:
                    PromptAllyCommand();
                    break;
                    
                case EntityType.NPC:
                    CheckNPCRequest();
                    break;
                    
                case EntityType.Enemy:
                    ProcessAITurn();
                    break;
            }
        }
        
        private void PromptAllyCommand()
        {
            var ally = currentActor;
            int commandCost = CalculateCommandCost(ally);
            
            infoDisplay?.ShowCommandPrompt(ally.Name, commandCost, battleState.PlayerMoney);
            
            // Store current context
            SetMeta("pending_ally", ally.Id);
            SetMeta("command_cost", commandCost);
            
            // Wait for response through signal
            waitingForInput = true;
        }
        
        // Connect this to the ResponseGiven signal from BattleInfoDisplay
        private void OnPromptResponse(bool response)
        {
            var allyId = GetMeta("pending_ally", "").AsString();
            var ally = battleState.GetAllEntities().FirstOrDefault(e => e.Id == allyId);
            
            if (ally != null)
            {
                OnAllyCommandResponse(response);
            }
            
            RemoveMeta("pending_ally");
            RemoveMeta("command_cost");
        }
        
        private void OnAllyCommandResponse(bool command)
        {
            if (command)
            {
                int cost = CalculateCommandCost(currentActor);
                if (battleState.PlayerMoney >= cost)
                {
                    battleState.PlayerMoney -= cost;
                    infoDisplay?.LogBattle($"💰 Paid {cost}P to command {currentActor.Name}");
                    ShowActionMenu();  // Let player control ally
                }
                else
                {
                    infoDisplay?.LogBattle($"⚠️ Not enough money!");
                    ProcessAITurn();
                }
            }
            else
            {
                ProcessAITurn();
            }
        }
        
        #endregion
        
        #region Action Menu
        
        private void ShowActionMenu()
        {
            waitingForInput = true;
            
            var options = new List<string> { "Skill", "Move", "Talk", "Item" };
            
            // Use your menu system
            Call("_show_menu", "action_menu", options);
        }
        
        private void OnMenuOptionSelected(string menuId, int optionIndex)
        {
            if (menuId == "action_menu")
            {
                switch (optionIndex)
                {
                    case 0: ShowSkillMenu(); break;
                    case 1: ShowMoveOptions(); break;
                    case 2: ShowTalkMenu(); break;
                    case 3: ShowItemMenu(); break;
                }
            }
            else if (menuId == "skill_menu")
            {
                SelectSkill(optionIndex);
            }
            else if (menuId == "move_menu")
            {
                SelectMove(optionIndex);
            }
            else if (menuId == "talk_menu")
            {
                SelectTalkOption(optionIndex);
            }
            else if (menuId == "item_menu")
            {
                SelectItem(optionIndex);
            }
        }
        
        private void SelectMove(int index)
        {
            BattleAction action = index switch
            {
                0 => actionLibrary["walk"],
                1 => actionLibrary["sprint"],
                2 => actionLibrary["teleport"],
                _ => null
            };
            
            if (action != null)
            {
                pendingAction = action;
                ShowMovementTargeting(action as MoveAction);
            }
            else
            {
                ShowActionMenu();
            }
        }
        
        private void ShowMovementTargeting(MoveAction moveAction)
        {
            if (moveAction == null) return;
            
            // Get valid move positions
            var currentPos = new Vector2I(currentActor.Position.Q, currentActor.Position.R);
            var validMoves = GetValidMovePositions(currentPos, moveAction.MoveRange);
            
            // Highlight valid moves on hex grid
            hexGrid.Call("highlight_cells", validMoves, 2);  // Use movement highlight tile
            
            infoDisplay?.LogBattle($"Select destination (Range: {moveAction.MoveRange})");
            waitingForInput = true;
        }
        
        private List<Vector2I> GetValidMovePositions(Vector2I from, int range)
        {
            var validPositions = new List<Vector2I>();
            
            // Simple range check - can be improved with pathfinding
            for (int q = -range; q <= range; q++)
            {
                for (int r = Math.Max(-range, -q - range); r <= Math.Min(range, -q + range); r++)
                {
                    var pos = new Vector2I(from.X + q, from.Y + r);
                    
                    // Check if position is empty
                    if (!entityPositions.ContainsKey(pos))
                    {
                        validPositions.Add(pos);
                    }
                }
            }
            
            return validPositions;
        }
        
        private void ShowSkillMenu()
        {
            var skills = new List<string>
            {
                "Basic Attack (0P)",
                "Fireball (10P)",
                "Heal (0P)",
                "Back"
            };
            
            Call("_show_menu", "skill_menu", skills);
        }
        
        private void ShowMoveOptions()
        {
            var moves = new List<string>
            {
                "Walk (0P)",
                "Sprint (3P)",
                "Teleport (15P)",
                "Back"
            };
            
            Call("_show_menu", "move_menu", moves);
        }
        
        private void ShowTalkMenu()
        {
            var talks = new List<string>
            {
                "Small Talk (0P)",
                "Bribe (20P)",
                "Intimidate (0P)",
                "Back"
            };
            
            Call("_show_menu", "talk_menu", talks);
        }
        
        private void ShowItemMenu()
        {
            var items = new List<string>
            {
                "Health Potion (0P)",
                "Poison Vial (5P)",
                "Smoke Bomb (0P)",
                "Back"
            };
            
            Call("_show_menu", "item_menu", items);
        }
        
        private void SelectTalkOption(int index)
        {
            BattleAction action = index switch
            {
                0 => actionLibrary["small_talk"],
                1 => actionLibrary["bribe"],
                2 => actionLibrary["intimidate"],
                _ => null
            };
            
            if (action != null)
            {
                pendingAction = action;
                ShowTargeting(action);
            }
            else
            {
                ShowActionMenu();
            }
        }
        
        private void SelectItem(int index)
        {
            BattleAction action = index switch
            {
                0 => actionLibrary["health_potion"],
                1 => actionLibrary["poison_vial"],
                2 => actionLibrary["smoke_bomb"],
                _ => null
            };
            
            if (action != null)
            {
                pendingAction = action;
                ShowTargeting(action);
            }
            else
            {
                ShowActionMenu();
            }
        }
        
        #endregion
        
        #region Hex Grid Integration
        
        private void ShowTargeting(BattleAction action)
        {
            var validTargets = action.GetValidTargets(currentActor, battleState);
            var validPositions = new List<Vector2I>();
            
            foreach (var target in validTargets)
            {
                var hexPos = new Vector2I(target.Position.Q, target.Position.R);
                validPositions.Add(hexPos);
            }
            
            // Highlight valid targets on hex grid
            hexGrid.Call("highlight_cells", validPositions, 1);  // Use highlight tile ID 1
            
            infoDisplay?.LogBattle("Select target...");
            waitingForInput = true;
        }
        
        private void OnHexSelected(Vector2I cell)
        {
            if (!waitingForInput) return;
            
            // Check if there's an entity at this position
            var entity = GetEntityAt(cell);
            
            if (entity != null && pendingAction != null)
            {
                pendingTargets.Clear();
                pendingTargets.Add(entity);
                ExecuteAction();
            }
        }
        
        private Entity GetEntityAt(Vector2I cell)
        {
            var hexPos = new HexPos(cell.X, cell.Y);
            return battleState.GetAllEntities()
                .FirstOrDefault(e => e.Position.Q == hexPos.Q && e.Position.R == hexPos.R);
        }
        
        public void PlaceEntity(Entity entity, Vector2I hexCell)
        {
            entity.Position = new HexPos(hexCell.X, hexCell.Y);
            entityPositions[hexCell] = entity;
            
            // Update visual position if needed
            if (hexGrid.HasMethod("place_entity"))
            {
                hexGrid.Call("place_entity", entity.Id, hexCell);
            }
        }
        
        #endregion
        
        #region Action Execution
        
        private void ExecuteAction()
        {
            if (pendingAction == null || currentActor == null) return;
            
            var result = pendingAction.Execute(currentActor, pendingTargets, battleState);
            
            // Clear highlights
            hexGrid.Call("clear_highlights");
            
            // Reset state
            pendingAction = null;
            pendingTargets.Clear();
            waitingForInput = false;
            
            // Continue to next turn
            CallDeferred(nameof(ProcessNextTurn));
        }
        
        private void ProcessAITurn()
        {
            // Simple AI - use the correct method name
            var enemies = GetEnemiesOf(currentActor);
            if (enemies.Any())
            {
                var target = enemies.First();
                var action = actionLibrary["basic_attack"];
                
                action.Execute(currentActor, new List<Entity> { target }, battleState);
            }
            
            CallDeferred(nameof(ProcessNextTurn));
        }
        
        private List<Entity> GetEnemiesOf(Entity entity)
        {
            return battleState.GetAllEntities()
                .Where(e => e.IsAlive && e.Type != entity.Type)
                .ToList();
        }
        
        private List<Entity> GetAlliesOf(Entity entity)
        {
            return battleState.GetAllEntities()
                .Where(e => e.IsAlive && e.Type == entity.Type && e != entity)
                .ToList();
        }
        
        #endregion
        
        #region Helpers
        
        private void ShowYesNoMenu(string menuId, string prompt, Action onYes, Action onNo)
        {
            // This would integrate with your menu system
            infoDisplay?.ShowPrompt(prompt, onYes, onNo);
        }
        
        private void CheckNPCRequest()
        {
            // 30% chance NPC makes a request
            if (GD.Randf() < 0.3f)
            {
                int payment = CalculateNPCPayment();
                infoDisplay?.ShowNPCRequest(currentActor.Name, payment);
            }
            else
            {
                ProcessAITurn();
            }
        }
        
        private int CalculateCommandCost(Entity ally)
        {
            int baseCost = 10;
            float friendshipMod = 2f - (ally.Friendship / 100f);
            return (int)(baseCost * friendshipMod);
        }
        
        private int CalculateNPCPayment()
        {
            var player = battleState.GetPlayer();
            int basePayment = 10;
            if (player != null && player.Reputation > 70) basePayment += 5;
            return basePayment;
        }
        
        private void LoadActions()
        {
            // Load all actions from the game database
            actionLibrary = BattleContent.ActionLoader.LoadAllActions();
        }
        
        private void EndBattle()
        {
            bool victory = battleState.GetPlayer()?.IsAlive ?? false;
            
            if (victory)
            {
                battleState.PlayerMoney += 50;
                infoDisplay?.LogBattle("Victory! +50P");
            }
            else
            {
                infoDisplay?.LogBattle("Defeat!");
            }
            
            infoDisplay?.ShowBattleEnd(victory, battleState.PlayerMoney);
        }
        
        #endregion
    }
    
    // Comprehensive info display system
    public partial class BattleInfoDisplay : RichTextLabel
    {
        [Export] private Color turnColor = new Color(1, 1, 0);
        [Export] private Color damageColor = new Color(1, 0, 0);
        [Export] private Color healColor = new Color(0, 1, 0);
        [Export] private Color moneyColor = new Color(1, 0.8f, 0);
        [Export] private Color systemColor = new Color(0.7f, 0.7f, 1);
        
        private Queue<string> messageQueue = new();
        private const int MaxMessages = 100;
        
        public override void _Ready()
        {
            BbcodeEnabled = true;
            ScrollFollowing = true;
        }
        
        public void LogBattle(string message)
        {
            AddMessage(message, systemColor);
        }
        
        public void LogTurn(string entityName, string entityType)
        {
            AddMessage($"\n[{entityName}'s Turn - {entityType}]", turnColor);
        }
        
        public void LogDamage(string attacker, string target, float damage)
        {
            AddMessage($"{attacker} deals {damage:F0} damage to {target}!", damageColor);
        }
        
        public void LogHeal(string healer, string target, float amount)
        {
            AddMessage($"{healer} heals {target} for {amount:F0} HP!", healColor);
        }
        
        public void LogMoney(string message, int amount)
        {
            AddMessage($"💰 {message}: {amount}P", moneyColor);
        }
        
        public void ShowCommandPrompt(string allyName, int cost, int playerMoney)
        {
            AddMessage($"\n💰 Command {allyName}?", moneyColor);
            AddMessage($"Cost: {cost}P (You have: {playerMoney}P)", moneyColor);
            AddMessage("Press Y to command, N to let AI control", systemColor);
        }
        
        public void ShowNPCRequest(string npcName, int payment)
        {
            AddMessage($"\n💰 {npcName} offers {payment}P for your help!", moneyColor);
            AddMessage("Press Y to accept, N to decline", systemColor);
        }
        
        public void ShowPrompt(string prompt, Action onYes, Action onNo)
        {
            AddMessage($"\n{prompt}", systemColor);
            AddMessage("Y/N?", systemColor);
            
            // Store callbacks for input handling
            SetMeta("prompt_yes", onYes);
            SetMeta("prompt_no", onNo);
        }
        
        public void ShowBattleEnd(bool victory, int finalMoney)
        {
            var header = victory ? "=== VICTORY ===" : "=== DEFEAT ===";
            AddMessage($"\n{header}", victory ? healColor : damageColor);
            AddMessage($"Final Money: {finalMoney}P", moneyColor);
        }
        
        private void AddMessage(string message, Color color)
        {
            // Add to queue
            messageQueue.Enqueue($"[color=#{color.ToHtml()}]{message}[/color]");
            
            // Keep queue size manageable
            while (messageQueue.Count > MaxMessages)
            {
                messageQueue.Dequeue();
            }
            
            // Update display
            RefreshDisplay();
        }
        
        private void RefreshDisplay()
        {
            Clear();
            foreach (var msg in messageQueue)
            {
                AppendText(msg + "\n");
            }
        }
        
        public void Clear()
        {
            Text = "";
            messageQueue.Clear();
        }
        
        // Input handling for prompts - using signal-based approach instead
        public override void _Input(InputEvent @event)
        {
            if (!GetMeta("waiting_for_response", false).AsBool()) return;
            
            if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("confirm"))
            {
                SetMeta("waiting_for_response", false);
                EmitSignal("response_given", true);
            }
            else if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("cancel"))
            {
                SetMeta("waiting_for_response", false);
                EmitSignal("response_given", false);
            }
        }
    }
    
    // Add this signal to BattleInfoDisplay
    public partial class BattleInfoDisplay : RichTextLabel
    {
        [Signal]
        public delegate void ResponseGivenEventHandler(bool response);
    
    // Example scene setup helper using the game database
    public partial class BattleSceneSetup : Node
    {
        [Export] private PackedScene entityScene;
        [Export] private NodePath battleIntegrationPath;
        [Export] private string battleConfigName = "TestBattle";
        
        private BattleIntegration battle;
        
        public override void _Ready()
        {
            battle = GetNode<BattleIntegration>(battleIntegrationPath);
            SetupBattle();
        }
        
        private void SetupBattle()
        {
            // Load the appropriate battle configuration
            List<(Entity entity, Vector2I position)> setup = null;
            
            switch (battleConfigName)
            {
                case "TestBattle":
                    setup = BattleContent.GameDatabase.BattleConfigs.TestBattle.GetSetup();
                    break;
                case "ArenaBattle":
                    setup = BattleContent.GameDatabase.BattleConfigs.ArenaBattle.GetSetup();
                    break;
                default:
                    SetupDefaultBattle();
                    return;
            }
            
            // Add all entities to the battle
            foreach (var (entity, position) in setup)
            {
                battle.GetNode<BattleState>("%BattleState").AddEntity(entity);
                battle.PlaceEntity(entity, position);
            }
            
            // Start the battle
            battle.StartBattle();
        }
        
        private void SetupDefaultBattle()
        {
            // Fallback to a simple battle setup
            var player = BattleContent.GameDatabase.Characters.CreatePlayer();
            var ally = BattleContent.GameDatabase.Characters.CreateMercenary();
            var enemy = BattleContent.GameDatabase.Characters.CreateGoblin();
            
            battle.GetNode<BattleState>("%BattleState").AddEntity(player);
            battle.GetNode<BattleState>("%BattleState").AddEntity(ally);
            battle.GetNode<BattleState>("%BattleState").AddEntity(enemy);
            
            battle.PlaceEntity(player, new Vector2I(0, 0));
            battle.PlaceEntity(ally, new Vector2I(1, 0));
            battle.PlaceEntity(enemy, new Vector2I(3, 0));
            
            battle.StartBattle();
        }
    }
}