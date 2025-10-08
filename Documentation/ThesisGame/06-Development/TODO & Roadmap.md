# TODO & Roadmap

Current implementation status and future development plan.

---

## Implementation Status Overview

### ✅ Fully Implemented (Working)

#### Core Battle System
- [x] Turn-based combat with initiative
- [x] Hex grid math and pathfinding
- [x] Entity system (Player, Ally, Enemy, NPC, Neutral)
- [x] Action execution (skills, moves, items, talk)
- [x] Status effects (damage over time, buffs, debuffs)
- [x] Battle end detection (victory/defeat)

#### Action System
- [x] Range calculation (radius, pattern, all-tiles)
- [x] AOE calculation (radius, pattern, line)
- [x] Target filtering (type, occupation, self, LOS)
- [x] Whitelist/blacklist targeting
- [x] Inverse AOE
- [x] Line-of-sight checking
- [x] NPC action support (NPCs can use same actions)

#### AI System
- [x] 5 behavior types (aggressive, defensive, support, balanced, cowardly)
- [x] Decision-making framework
- [x] Priority-based action selection
- [x] Target evaluation
- [x] Behavior configuration via JSON

#### UI System
- [x] Context-aware input routing (Menu/HexGrid/Dialogue)
- [x] Menu navigation system
- [x] Hex cursor and targeting
- [x] Dynamic submenu creation
- [x] Virtual cursor display
- [x] Camera following

#### Integration
- [x] Dialogic integration for VN sequences
- [x] Scene sequencing (VN ↔ Battle transitions)
- [x] P variable tracking
- [x] Battle results storage
- [x] JSON configuration loading
- [x] Dynamic map loading

---

### 🚧 Partially Implemented (Needs Testing/Polish)

#### Inventory System
**Status**: Framework exists, needs battle integration testing

**Completed**:
- [x] InventoryManager singleton
- [x] Item data structure
- [x] Add/Remove item methods
- [x] Context-based item filtering
- [x] Item usage framework
- [x] InventoryControls UI scene
- [x] Basic integration with BattleManager

**Needs Work**:
- [ ] Thorough battle context testing
- [ ] Item effect execution in battle
- [ ] Item consumption on use
- [ ] Dialogue context (gift giving)
- [ ] Menu context testing
- [ ] Item definitions in JSON (currently hardcoded)
- [ ] Item icons/sprites
- [ ] Confirmation dialogs

**Files Involved**:
- `InventoryManager.cs` - Core logic
- `InventoryControls.cs` - UI controller
- `BattleManager.cs` - Integration code
- `BattleActionHandler.cs` - Item execution support

#### Battle Results Screen
**Status**: Hooks exist, UI screen needed

**Completed**:
- [x] Result calculation (P earned, turns, enemies defeated)
- [x] TurnManager.OnBattleEnded event
- [x] Result storage in SceneManager
- [x] P value addition

**Needs Work**:
- [ ] Create BattleResultsUI scene
- [ ] Create BattleResultsUI.cs script
- [ ] Display victory/defeat message
- [ ] Display statistics
- [ ] Implement Continue button
- [ ] Implement Retry button (optional)
- [ ] Victory vs Defeat different layouts

**Integration Points**:
- Connect to `TurnManager.OnBattleEnded`
- Read results from `SceneManager.GetBattleResults()`
- Trigger `SceneManager.LoadNextInSequence()` on continue

---

### ⬜ Not Yet Implemented

#### Main Menu System
**Priority**: High (after inventory works)

**Requirements**:
- [ ] Create MainMenuUI scene
- [ ] Create MainMenuUI.cs script
- [ ] "New Game" button → Start sequence from beginning
- [ ] "Continue" button → Load last save / resume sequence
- [ ] "Load Game" button → Show save slots
- [ ] "Settings" button → Open settings menu
- [ ] "Quit" button → Exit application
- [ ] Set as entry scene
- [ ] Integration with SceneManager
- [ ] Save file detection for Continue button state

**Dependencies**: Save/Load system (for Continue/Load buttons)

---

#### Status/Party Screen
**Priority**: Medium

**Requirements**:
- [ ] Define what stats to display
  - Current/Max HP
  - Initiative
  - Speed
  - Active status effects
  - Available skills/items
- [ ] Create StatusScreenUI scene
- [ ] Create StatusScreenUI.cs script
- [ ] Display single character info
- [ ] Navigation between party members (if multiple)
- [ ] Accessible from battle menu (optional)
- [ ] Accessible from main menu

**Integration Points**:
- Read from Entity objects
- Display active StatusEffects
- Show available skills from configuration

---

#### Settings Menu
**Priority**: Medium-Low

**Requirements**:
- [ ] Create SettingsUI scene
- [ ] Create SettingsUI.cs script
- [ ] Create ConfigManager singleton for persistence
- [ ] Audio settings:
  - [ ] Master volume slider
  - [ ] Music volume slider
  - [ ] SFX volume slider
- [ ] Display settings:
  - [ ] Fullscreen toggle
  - [ ] Resolution dropdown
  - [ ] VSync toggle
- [ ] Gameplay settings:
  - [ ] Combat speed
  - [ ] Text speed
  - [ ] Auto-advance dialogue toggle
- [ ] Input settings (optional):
  - [ ] Key rebinding
- [ ] Settings persistence (save to config file)
- [ ] Apply settings immediately

**Dependencies**: ConfigManager.cs (new singleton)

---

#### Save/Load System
**Priority**: High (but complex)

**Requirements**:
- [ ] Create SaveManager singleton
- [ ] Define comprehensive save data structure:
  - [ ] Player progress (current sequence index)
  - [ ] P value
  - [ ] Inventory state
  - [ ] Entity states (if mid-battle save)
  - [ ] Story flags/variables
  - [ ] Dialogic variables
  - [ ] Playtime
- [ ] Implement save functionality
- [ ] Implement load functionality
- [ ] Create SaveLoadUI scene
- [ ] Create SaveLoadUI.cs script
- [ ] Create SaveSlot component
- [ ] Display save slot metadata (date, time, location, playtime)
- [ ] Save file creation
- [ ] Save file overwrite confirmation
- [ ] Save file deletion
- [ ] Auto-save support (optional)
- [ ] Save file corruption handling
- [ ] Save location: `user://saves/`

**Data to Save**:
```csharp
class CompleteSaveData {
    SaveMetadata metadata;
    int currentSequenceIndex;
    int pValue;
    Dictionary<string, int> inventory;
    Dictionary<string, Variant> storyFlags;
    Dictionary<string, Variant> dialogicVariables;
    BattleStateData battleState; // If mid-battle
    float totalPlaytime;
}
```

**Integration Points**:
- SceneManager (sequence progress)
- InventoryManager (item data)
- Dialogic (variable sync)
- BattleStateManager (mid-battle saves)

---

## Development Priority Order

### Phase 1: Complete Core Features ✅ (DONE)
- [x] Battle system
- [x] Turn management
- [x] Action system
- [x] AI behaviors
- [x] UI input routing
- [x] Scene sequencing

### Phase 2: Inventory System 🚧 (CURRENT)
**Goal**: Fully working inventory in all contexts

**Steps**:
1. [ ] Test inventory opening from battle
2. [ ] Test item usage in battle (targeting)
3. [ ] Verify item consumption
4. [ ] Test item effects execution
5. [ ] Test inventory in dialogue context
6. [ ] Test inventory in menu context
7. [ ] Replace hardcoded items with JSON
8. [ ] Add item icons
9. [ ] Polish UI/UX

**Estimated Effort**: 1-2 weeks

---

### Phase 3: Battle Results Screen
**Goal**: Proper end-of-battle feedback

**Steps**:
1. [ ] Design results screen layout
2. [ ] Create scene structure
3. [ ] Create controller script
4. [ ] Display victory/defeat message
5. [ ] Show statistics (turns, P earned, enemies defeated)
6. [ ] Implement Continue button
7. [ ] Test transition to next scene
8. [ ] Polish visuals

**Estimated Effort**: 3-5 days

---

### Phase 4: Main Menu
**Goal**: Professional entry point

**Steps**:
1. [ ] Design menu layout
2. [ ] Create MainMenuUI scene
3. [ ] Create MainMenuUI.cs
4. [ ] Implement New Game
5. [ ] Implement Continue (detect last position)
6. [ ] Implement Load Game
7. [ ] Implement Settings connection
8. [ ] Implement Quit
9. [ ] Set as default scene
10. [ ] Polish visuals/transitions

**Estimated Effort**: 1 week

---

### Phase 5: Status Screen
**Goal**: View character info

**Steps**:
1. [ ] Define display requirements
2. [ ] Create layout mockup
3. [ ] Create StatusScreenUI scene
4. [ ] Create StatusScreenUI.cs
5. [ ] Display entity stats
6. [ ] Display status effects
7. [ ] Display available actions
8. [ ] Add to battle menu
9. [ ] Add to main menu
10. [ ] Test thoroughly

**Estimated Effort**: 1 week

---

### Phase 6: Settings Menu
**Goal**: Player preferences

**Steps**:
1. [ ] Create ConfigManager singleton
2. [ ] Design settings categories
3. [ ] Create SettingsUI scene
4. [ ] Create SettingsUI.cs
5. [ ] Implement audio controls
6. [ ] Implement display controls
7. [ ] Implement gameplay controls
8. [ ] Settings persistence
9. [ ] Apply settings immediately
10. [ ] Polish UI

**Estimated Effort**: 1-2 weeks

---

### Phase 7: Save/Load System
**Goal**: Game persistence

**Steps**:
1. [ ] Design save data structure
2. [ ] Create SaveManager singleton
3. [ ] Implement save functionality
4. [ ] Implement load functionality
5. [ ] Create SaveLoadUI scene
6. [ ] Create SaveLoadUI.cs
7. [ ] Create SaveSlot component
8. [ ] Display save metadata
9. [ ] Save creation/deletion
10. [ ] Auto-save (optional)
11. [ ] Corruption handling
12. [ ] Integration testing
13. [ ] Polish UI

**Estimated Effort**: 2-3 weeks

---

## Future Enhancements (Post-MVP)

### Gameplay Features
- [ ] More status effect types
- [ ] Equipment system
- [ ] Character progression/leveling
- [ ] Multiple party members
- [ ] Enemy variety and behaviors
- [ ] Special battle conditions (weather, terrain effects)
- [ ] Critical hits implementation
- [ ] Skill trees
- [ ] Battle formations

### Polish & Quality of Life
- [ ] Battle animations
- [ ] Particle effects for skills
- [ ] Sound effects
- [ ] Background music
- [ ] UI animations and transitions
- [ ] Tutorial system
- [ ] Tooltips and help text
- [ ] Keyboard shortcuts reference
- [ ] Battle log/history
- [ ] Damage numbers display
- [ ] HP bars on entities

### Technical Improvements
- [ ] Performance optimization
- [ ] Memory usage optimization
- [ ] Better error handling
- [ ] Comprehensive unit tests
- [ ] Automated integration tests
- [ ] Debug console
- [ ] Telemetry/analytics
- [ ] Mod support

### Content Tools
- [ ] Visual battle editor
- [ ] Map editor integration
- [ ] Skill/item database editor
- [ ] Entity template editor
- [ ] Dialogue integration tools

---

## Known Issues & Bugs

### High Priority
- [ ] None currently

### Medium Priority
- [ ] Cursor can sometimes get stuck at invalid position
- [ ] Menu focus can be lost when rapidly switching contexts
- [ ] AOE preview doesn't always clear properly on cancel

### Low Priority
- [ ] Debug logs too verbose in some areas
- [ ] Some edge cases in hex line drawing
- [ ] Menu cursor snap can feel jarring

---

## Technical Debt

### Code Quality
- [ ] Some managers have grown large (consider splitting)
- [ ] Magic numbers in some places (should be constants)
- [ ] Duplicate hex math code (consolidate into HexGrid)
- [ ] Some long methods need refactoring

### Documentation
- [x] Core system documentation (DONE - this vault!)
- [ ] Inline code comments (partial)
- [ ] API documentation generation
- [ ] Example/tutorial code

### Testing
- [ ] Unit tests for hex math
- [ ] Unit tests for action system
- [ ] Unit tests for AI behaviors
- [ ] Integration tests for battle flow
- [ ] UI automation tests

---

## Performance Considerations

### Current Performance
- Battle system runs smoothly
- No noticeable lag with current entity counts
- Hex math is efficient

### Future Scaling Concerns
- Large maps (50+ cells) may need optimization
- Many entities (10+) may slow turn calculation
- Complex AOE patterns could be optimized
- Save file size could grow large

### Optimization Opportunities
- [ ] Cache hex distance calculations
- [ ] Object pooling for UI elements
- [ ] Lazy loading for configurations
- [ ] Compress save files
- [ ] Async loading for maps

---

## Questions to Resolve

### Design Decisions Needed
- [ ] Should mid-battle saving be allowed?
- [ ] Should there be permadeath?
- [ ] Should entities persist between battles?
- [ ] How many save slots should exist?
- [ ] Should battles be replayable?

### Technical Decisions Needed
- [ ] Save format: JSON or binary?
- [ ] Version migration strategy for saves?
- [ ] How to handle backwards compatibility?
- [ ] Should settings be cloud-synced?

---

## Contributing Guidelines

When working on items from this TODO:

1. **Pick an item from the current phase**
2. **Create a feature branch**
3. **Update this document** as you progress
4. **Add tests** if applicable
5. **Update related documentation**
6. **Mark item complete** when done

### Marking Items Complete
```markdown
- [x] Item description
```

### Adding New Items
```markdown
- [ ] Item description (Priority: High/Medium/Low)
  - [ ] Sub-task 1
  - [ ] Sub-task 2
```

---

## Related Documentation
- [[System Architecture]] - Overall structure
- [[Component Reference]] - All classes
- [[Common Patterns]] - Code templates
- [[Configuration Files]] - JSON reference