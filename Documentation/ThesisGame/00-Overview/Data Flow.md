
Detailed sequence diagrams showing how data moves through the system.

---

## Battle Initialization

```
SceneManager
    ↓ LoadNextInSequence()
    ↓ Parameters: battle_config, map
    ↓
GetTree().ChangeSceneToFile("BaseBattle.tscn")
    ↓
BattleManager._Ready()
    ↓
LoadBattleMap()
    ├─ Get map path from SceneManager
    ├─ Load PackedScene
    ├─ Instantiate HexGrid
    └─ Reparent HexControls to HexGrid
    ↓
InitializeComponents()
    ├─ Create all manager instances
    ├─ SetupComponentDependencies()
    │   ├─ actionHandler.Initialize(stateManager, configLoader)
    │   ├─ turnManager.Initialize(stateManager, uiController)
    │   ├─ npcBehaviorManager.Initialize(...)
    │   ├─ stateManager.Initialize(battleManager)
    │   └─ uiController.Initialize(battleManager)
    └─ InitializeSceneManager()
    ↓
SetupBattle()
    ├─ configLoader.LoadConfiguration(configFilePath)
    │   ├─ CustomJsonLoader.LoadBattleConfig()
    │   ├─ Parse JSON to BattleConfigData
    │   └─ Build action lookup dictionaries
    ├─ stateManager.SetupBattleFromConfig(config)
    │   ├─ Create entities from EntityDefinitions
    │   ├─ AddEntity() for each
    │   │   ├─ Add to entity list
    │   │   ├─ Update position map
    │   │   └─ Draw on HexGrid Entity layer
    │   └─ Set phase to PlayerTurn
    ├─ uiController.SetupUI()
    │   └─ HexControls.StartUIOnlyMode()
    └─ turnManager.StartBattle()
        ↓
        StartNextRound()
            ├─ Calculate turn order by Initiative
            └─ ProcessNextTurn()
```

---

## Player Action - Full Sequence

### Phase 1: Action Selection

```
TurnManager detects player turn
    ↓
StartPlayerTurn()
    ↓
BattleUIController.ShowMainMenu()
    ├─ MenuControls.SetActive(true)
    └─ MenuControls.ResetToFirstButton()
    ↓
CentralInputManager detects context
    ├─ DetectActiveControl()
    ├─ currentContext = InputContext.Menu
    └─ currentActiveControl = MenuControls
    ↓
User presses arrow keys
    ↓
CentralInputManager._Input()
    ├─ GetDirectionInput() → Vector2I
    └─ HandleMenuInput()
        ↓
        MenuControls.Navigate(direction)
            ├─ Update currentIndex
            ├─ ApplySelection()
            │   ├─ ClearAllButtonFocus()
            │   └─ currentButton.GrabFocus()
            └─ EmitSignal(ButtonSelected)
    ↓
User presses confirm (Space/Enter)
    ↓
CentralInputManager routes accept input
    ↓
MenuControls.ActivateCurrentButton()
    ├─ button.EmitSignal(Pressed)
    └─ EmitSignal(ButtonActivated, index, button)
    ↓
BattleUIController receives signal
    ↓
BattleManager.OnActionRequested(actionType, "")
```

### Phase 2: Submenu Selection

```
BattleManager.OnActionRequested("skill", "")
    ↓
BattleActionHandler.ProcessActionRequest("skill", "")
    ├─ Store currentActionType = "skill"
    ├─ Clear previous action
    └─ DEBUG: "Action request: skill"
    ↓
BattleManager gets available actions
    ├─ GetAvailableActionsForType("skill")
    └─ configLoader.GetSkillNames()
        ↓ Returns ["Direct Hit", "Mortar Strike", ...]
    ↓
BattleUIController.ShowSubmenu(skillNames)
    ├─ isShowingSubmenu = true
    ├─ Store previousMenu
    ├─ Deactivate previous menu
    ├─ CentralInputManager.SetMenuButtonArray(skillNames)
    │   ↓
    │   FindDynamicMenu()
    │   ├─ Traverse UI tree for DynamicMenuRoot
    │   └─ Find MenuControls inside
    │       ↓
    │       MenuControls.SetButtonsFromArray(skillNames)
    │           ├─ ClearAllButtons()
    │           ├─ AddButton() for each skill
    │           └─ ResetToFirstButton()
    ├─ DynamicMenu.SetActive(true)
    └─ Context switches to DynamicMenu
    ↓
User navigates and selects "Direct Hit"
    ↓
DynamicMenu.ButtonActivated signal
    ↓
BattleManager.OnSubmenuSelection("Direct Hit")
```

### Phase 3: Target Calculation

```
BattleManager.OnSubmenuSelection("Direct Hit")
    ↓
BattleActionHandler.ProcessSubmenuSelection("Direct Hit")
    ├─ selectedActionOption = "Direct Hit"
    ├─ currentActionConfig = configLoader.GetActionConfig("Direct Hit")
    │   ↓
    │   Returns ActionConfig object:
    │   {
    │       Name: "Direct Hit",
    │       Damage: 20,
    │       Range: 2,
    │       UseRadiusRange: true,
    │       TargetType: "Area",
    │       ...
    │   }
    └─ currentValidTargets = CalculateValidTargets(config)
        ↓
        CalculateValidTargets() DETAILED:
            ↓
            Get playerPos from stateManager
            Create validTargets HashSet
            ↓
            IF config.UseRadiusRange:
                ├─ Convert playerPos to Cube coordinates
                ├─ Generate all cells within Range using cube math
                │   FOR q in [-Range...Range]:
                │       FOR r in [-Range...Range]:
                │           FOR s in [-Range...Range]:
                │               IF q+r+s == 0 AND distance <= Range:
                │                   ├─ Convert cube to offset
                │                   └─ Add to validTargets
                └─ Result: All cells within radius 2
            ↓
            Apply Whitelist (if any)
            Apply Blacklist (if any)
            ↓
            Filter validTargets:
                FOR each cell in validTargets:
                    ├─ IF !IsValidGridPosition(cell): SKIP
                    ├─ IF RequiresLineOfSight AND !HasLineOfSight(): SKIP
                    ├─ IF !PassesTargetFilters(): SKIP
                    └─ Add to finalTargets
            ↓
            PassesTargetFilters() checks:
                ├─ CanTargetCell(TargetType)?
                ├─ ExcludeSelf?
                ├─ ExcludeOccupied?
                ├─ TargetEmptyCellsOnly?
                └─ TargetSelfOnly?
            ↓
            IF CanTargetSelf(TargetType) AND !ExcludeSelf:
                Add playerPos to finalTargets
            ↓
            Return finalTargets
    ↓
    DEBUG: "Action selected: Direct Hit, X valid targets"
    ↓
BattleManager.OnSubmenuSelection continues
    ↓
Get validTargets from actionHandler
    ↓
BattleUIController.StartTargetSelection(validTargets, config)
```

### Phase 4: Target Selection UI

```
BattleUIController.StartTargetSelection(validTargets, config)
    ├─ targetSelectionActive = true
    ├─ currentActionConfig = config
    ├─ HideMainMenu()
    ├─ HideSubmenu()
    ├─ HexGrid.ShowRangeHighlight(validTargets)
    │   ↓
    │   FOR each cell in validTargets:
    │       SetCursor(cell, CursorType.Range, CellLayer.Marker)
    │       ↓ Draws blue marker on Marker layer
    │
    └─ HexControls.EnterInteractionMode()
        ├─ interactionModeActive = true
        ├─ SetActive(true)
        ├─ cursorPosition = FindPlayerPosition()
        ├─ CalculateAdjacentCells()
        │   ↓ Finds cells next to valid targets
        ├─ DrawCursor()
        │   ↓ Draws green/red cursor on Cursor layer
        └─ SetCameraFollow(true)
    ↓
HexControls.SetActionConfig(config, actionHandler)
    ├─ Store config and handler
    └─ canTargetSelf = HexGrid.CanTargetSelf(config.TargetType)
    ↓
HexControls.SetValidCells(validTargets)
    └─ Store validCells HashSet
    ↓
CentralInputManager detects context change
    ├─ DetectActiveControl()
    ├─ currentContext = InputContext.HexGrid
    └─ currentActiveControl = HexControls
```

### Phase 5: Cursor Movement

```
User presses WASD
    ↓
CentralInputManager._Input()
    ├─ Context is HexGrid
    └─ Route to HexControls (HexControls handles own input)
    ↓
HexControls._Input(InputEventKey)
    ├─ Detect direction (W=up, S=down, A=left, D=right)
    └─ MoveCursor(direction)
        ↓
        Calculate newPos = cursorPosition + direction
        ├─ Get neighbors from HexGrid.GetHexNeighbors()
        ├─ Check if newPos in neighbors
        └─ Check if newPos is valid or adjacent
            ↓
            IF valid:
                ├─ cursorPosition = newPos
                ├─ DrawCursor()
                │   ├─ Clear Cursor layer
                │   ├─ Determine if position is valid target
                │   ├─ SetCursor(pos, Valid/Invalid, Cursor layer)
                │   └─ EmitSignal(CursorMoved, pos)
                ├─ ShowAoeIfNeeded()
                │   ↓
                │   IF IsValidTarget(cursorPosition):
                │       ├─ affectedCells = actionHandler.CalculateAffectedCells()
                │       │   ↓
                │       │   CalculateAffectedCells() DETAILED:
                │       │       ├─ Add targetCell to affectedCells
                │       │       ├─ IF AoeType == "line":
                │       │       │   └─ CalculateLineAOE()
                │       │       ├─ ELSE IF AoeRadius > 0:
                │       │       │   └─ Use cube math for radius
                │       │       ├─ ELSE IF AoePattern exists:
                │       │       │   └─ Apply pattern offsets
                │       │       └─ Return affectedCells
                │       │
                │       └─ HexGrid.ShowAoePreviewAbsolute(affectedCells)
                │           ├─ ClearAoePreview()
                │           ├─ FOR each cell in affectedCells:
                │           │   ├─ Store original cursor type
                │           │   └─ SetCursor(cell, AOE, Marker layer)
                │           └─ Redraw main cursor on top
                │   ELSE:
                │       └─ ClearAoePreview() and reset range markers
                │
                └─ UpdateCamera()
```

### Phase 6: Target Confirmation

```
User presses Space/Enter
    ↓
HexControls._Input(InputEventKey)
    ├─ Detect confirm key
    └─ ConfirmSelection()
        ↓
        IF !IsValidTarget(cursorPosition):
            └─ DEBUG: "Invalid selection" → RETURN
        ↓
        IF cursorPosition == playerPos AND TargetType == "movement":
            └─ Special case: Cancel if just entered
        ↓
        EmitSignal(CellActivated, cursorPosition)
    ↓
BattleUIController connected to CellActivated
    ↓
BattleManager.OnTargetSelected(targetCell)
```

### Phase 7: Action Execution

```
BattleManager.OnTargetSelected(targetCell)
    ↓
BattleActionHandler.ProcessTargetSelection(targetCell)
    ↓
ExecuteAction(targetCell, currentActionConfig)
    ├─ Determine action type from currentActionType
    └─ Route to ExecuteSkillAction(targetCell, config)
        ↓
        DEBUG: "Skill 'Direct Hit' targeting (X,Y)"
        ↓
        affectedCells = CalculateAffectedCells(targetCell, config)
        ↓
        FOR each cell in affectedCells:
            ↓
            IF config.Damage > 0:
                ├─ stateManager.ApplyDamageToEntity(cell, damage)
                │   ↓
                │   entity = GetEntityAt(cell)
                │   IF entity exists:
                │       ├─ entity.TakeDamage(damage)
                │       │   └─ CurrentHP -= damage (clamped to 0)
                │       ├─ DEBUG: "Entity takes X damage"
                │       └─ IF !entity.IsAlive:
                │           └─ OnEntityDefeated()
                │
                └─ DEBUG: "Dealt X damage to (X,Y)"
            ↓
            IF config.StatusEffect not empty:
                ├─ stateManager.ApplyStatusEffectToEntity(cell, status)
                └─ DEBUG: "Applied status to (X,Y)"
        ↓
        DEBUG: "Skill cost: X"
    ↓
BattleActionHandler.ClearCurrentAction()
    ├─ Clear currentActionType
    ├─ Clear selectedActionOption
    ├─ Clear currentActionConfig
    └─ Clear currentValidTargets
    ↓
BattleManager.OnTargetSelected continues
    ↓
BattleUIController.EndTargetSelection()
    ├─ targetSelectionActive = false
    ├─ HexGrid.ClearRangeHighlight()
    ├─ HexGrid.ClearAoePreview()
    └─ HexControls.ExitInteractionMode()
        ├─ interactionModeActive = false
        ├─ Clear validCells
        ├─ HexGrid.ClearAllHighlights()
        ├─ SetActive(false)
        └─ SetCameraFollow(false)
    ↓
TurnManager.EndPlayerTurn()
```

### Phase 8: Turn End

```
TurnManager.EndPlayerTurn()
    ├─ currentPhase = TurnEnd
    ├─ UIController.HideMainMenu()
    ├─ DEBUG: "Player turn ended"
    └─ EndCurrentTurn()
        ↓
        entity.HasActedThisTurn = true
        ↓
        CheckBattleEndConditions()
            ├─ stateManager.CheckBattleEndConditions()
            │   ├─ Check if player alive
            │   └─ Check if any enemies alive
            │
            IF battle ended:
                └─ EndBattle()
            ELSE:
                └─ Continue
        ↓
        currentTurnIndex++
        ↓
        ProcessNextTurn()
            ↓
            IF currentTurnIndex >= turnOrder.Count:
                └─ EndRound()
                    ├─ stateManager.ProcessTurnEndEffects()
                    │   ↓
                    │   FOR each entity:
                    │       └─ entity.ProcessTurnEnd()
                    │           ├─ Process each StatusEffect
                    │           │   ├─ Apply damage/healing
                    │           │   ├─ Duration--
                    │           │   └─ Remove if duration <= 0
                    │           └─ HasActedThisTurn = false
                    │
                    └─ StartNextRound()
            ELSE:
                └─ Get next entity in turn order
                    ↓
                    IF entity.Type == Player:
                        └─ StartPlayerTurn()
                    ELSE:
                        └─ StartNPCTurn(entity)
```

---

## NPC Turn - Full Sequence

```
TurnManager.StartNPCTurn(entity)
    ├─ currentPhase = EnemyTurn
    ├─ DEBUG: "Entity's turn started"
    └─ NPCBehaviorManager.GetDecisionForEntity(entity)
        ↓
        Get behaviorType from entity.BehaviorType
        ↓
        behavior = behaviors[behaviorType]  // e.g., AggressiveBehavior
        ↓
        decision = behavior.DecideAction(entity, stateManager, configLoader, actionHandler)
            ↓
            ═══ AggressiveBehavior.DecideAction() ═══
            ↓
            skills = configLoader.GetSkillsForEntity(entity.Type)
            ↓
            Filter by entity.AvailableSkills
            ↓
            FOR each skill with damage > 0 (highest damage first):
                ↓
                validTargets = actionHandler.CalculateValidTargetsFromPosition(
                    entity.Position,
                    skill
                )
                ↓
                enemyTargets = validTargets.Where(IsEnemyTarget)
                ↓
                IF enemyTargets.Count > 0:
                    ├─ target = nearest enemy
                    └─ RETURN NPCDecision {
                            ActionType: "skill",
                            ActionName: skill.Name,
                            TargetCell: target,
                            IsValid: true,
                            Priority: 10
                        }
            ↓
            IF no valid actions:
                └─ RETURN NPCDecision.Skip()
        ↓
        RETURN decision
    ↓
NPCBehaviorManager.ExecuteDecision(entity, decision)
    ↓
    IF decision.ActionType == "skill":
        ├─ config = configLoader.GetActionConfig(decision.ActionName)
        ├─ affectedCells = actionHandler.CalculateAffectedCellsFromPosition(
        │       entity.Position,
        │       decision.TargetCell,
        │       config
        │   )
        └─ FOR each cell in affectedCells:
                ├─ stateManager.ApplyDamageToEntity(cell, config.Damage)
                └─ stateManager.ApplyHealingToEntity(cell, config.HealAmount)
    ↓
    ELSE IF decision.ActionType == "move":
        └─ stateManager.MoveEntity(entity, decision.TargetCell)
    ↓
    entity.HasActedThisTurn = true
    ↓
TurnManager.EndCurrentTurn()
    └─ [Same as Phase 8 above]
```

---

## Cancel Action Flow

```
User presses Escape during target selection
    ↓
HexControls._Input(InputEventKey.Escape)
    ↓
CancelInteraction()
    ├─ DEBUG: "Cancelled"
    ├─ HexGrid.ClearAllHighlights()
    ├─ cursorPosition = FindPlayerPosition()
    ├─ DrawCursor()
    └─ EmitSignal(InteractionCancelled)
    ↓
BattleManager.OnActionCancelled()
    ↓
BattleActionHandler.CancelCurrentAction()
    ├─ DEBUG: "Current action cancelled"
    └─ ClearCurrentAction()
    ↓
BattleUIController.EndTargetSelection()
    [Same as normal end]
    ↓
TurnManager.ReturnToActionSelection()
    ├─ currentPhase = ActionSelection
    ├─ UIController.EndTargetSelection()
    ├─ UIController.ShowMainMenu()
    └─ DEBUG: "Returned to action selection"
```

---

## Battle End Flow

```
TurnManager detects battle end
    ↓
CheckBattleEndConditions() returns true
    ↓
EndBattle()
    ├─ battleActive = false
    ├─ currentPhase = BattleEnd
    ├─ DEBUG: "Battle ended"
    └─ DetermineBattleOutcome()
        ↓
        player = stateManager.GetPlayer()
        playerWon = player?.IsAlive == true
        ↓
        IF playerWon:
            DEBUG: "=== VICTORY ==="
        ELSE:
            DEBUG: "=== DEFEAT ==="
        ↓
        OnBattleEnded?.Invoke(playerWon)
    ↓
BattleManager.OnBattleEnded(playerWon)
    ├─ Calculate results:
    │   ├─ victory: playerWon
    │   ├─ player_hp: player.CurrentHP
    │   ├─ turns_taken: turnManager.GetCurrentRound()
    │   ├─ enemies_defeated: count
    │   └─ p_earned: CalculatePEarned(playerWon)
    │
    ├─ sceneManager.StoreBattleResults(results)
    ├─ sceneManager.AddP(results["p_earned"])
    ├─ DEBUG: Results logged
    └─ sceneManager.LoadNextInSequence()
        ↓
        [Transitions to next scene in sequence]
```

---

## Dialogic Integration Flow

```
SceneManager loads BaseVN scene
    ↓
DialogueControls._Ready()
    ├─ Check SceneManager for "timeline" parameter
    └─ IF timeline exists:
            dialogic.Call("start", timelinePath)
    ↓
Dialogic autoload starts timeline
    ↓
DialogicAutoload.EmitSignal("timeline_started")
    ↓
CentralInputManager.OnDialogicStarted()
    ├─ Store contextBeforeDialogue
    ├─ Store controlBeforeDialogue
    ├─ Show dialogueContainer
    └─ DEBUG: "Dialogic timeline started"
    ↓
CentralInputManager.DetectActiveControl()
    ├─ IsDialogicActive() → true
    ├─ currentContext = InputContext.Dialogue
    └─ currentActiveControl = null
    ↓
ALL ui_* inputs consumed by Dialogic
    ↓
User progresses through dialogue
    ↓
Timeline ends
    ↓
DialogicAutoload.EmitSignal("timeline_ended")
    ↓
SceneManager.OnTimelineEnded()
    ├─ DEBUG: "Timeline ended"
    ├─ LoadPFromDialogic()
    │   └─ Sync P variable from Dialogic.VAR
    └─ LoadNextInSequence()
    ↓
CentralInputManager.OnDialogicEnded()
    ├─ Hide dialogueContainer
    └─ DEBUG: "Dialogic timeline ended"
```

---

## Configuration Loading Flow

```
BattleManager.SetupBattle()
    ↓
BattleConfigurationLoader.LoadConfiguration(filePath)
    ↓
CustomJsonLoader.LoadBattleConfig(filePath)
    ↓
FileAccess.Open(filePath, Read)
    ↓
file.GetAsText() → JSON string
    ↓
JsonSerializer.Deserialize<BattleConfigData>(json)
    ↓
BattleConfigData object created:
    ├─ List<ActionConfig> Skills
    ├─ List<ActionConfig> Items
    ├─ List<ActionConfig> TalkOptions
    ├─ List<ActionConfig> MoveOptions
    ├─ List<EntityDefinition> Entities
    └─ GameSettings Settings
    ↓
BattleConfigurationLoader.BuildActionLookups()
    ├─ Clear dictionaries
    ├─ FOR each skill/item/talk/move:
    │   ├─ allActions[config.Id] = config
    │   └─ actionsByName[config.Name] = config
    └─ DEBUG: "Built action lookups - X total actions"
    ↓
BattleConfigurationLoader.LogConfigurationSummary()
    └─ Print all loaded actions
    ↓
RETURN true (success)
    ↓
BattleManager continues with setup
```

---

## Related Documentation

- [[System Architecture]] - Component relationships
- [[Component Reference]] - All classes
- [[Action System]] - Action execution details
- [[Targeting System]] - Target calculation algorithms