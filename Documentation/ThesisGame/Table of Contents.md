
Complete index of all documentation files in this vault.

---

## 📖 How to Use This Documentation

### For New Developers
1. Start with [[README]] - Overview and quick start
2. Read [[System Architecture]] - Understand the big picture
3. Review [[Component Reference]] - Learn what each class does
4. Check [[Data Flow]] - See how data moves through the system

### For Feature Implementation
1. Check [[TODO & Roadmap]] - See what's planned
2. Review relevant system documentation
3. Check [[Common Patterns]] for code templates
4. Reference [[Configuration Files]] for data structures

### For Debugging
1. Review [[Data Flow]] for the relevant sequence
2. Check [[Component Reference]] for class details
3. Look at configuration examples in [[Configuration Files]]
4. Enable debug mode in relevant systems

### For Understanding Specific Systems
- Actions/Skills? → [[Action System]]
- Targeting? → [[Targeting System]]
- AI? → [[AI Behaviors]]
- UI? → [[Input Management]], [[Menu System]]
- Integration? → [[Dialogic Integration]], [[Scene Management]]

---

## 📁 Documentation Structure

### 🎯 Getting Started
- [[README]] - Start here! System overview and quick links
- [[System Architecture]] - High-level design and component relationships
- [[Scene Hierarchy]] - Scene tree structure for both battle and VN scenes
- [[Data Flow]] - Detailed sequence diagrams showing how data moves

### 🔧 Core Systems
- [[Battle Manager]] - Central coordination hub
- [[Turn System]] - Initiative-based turn management
- [[Entity System]] - Character and enemy data model
- [[Hex Grid System]] - Coordinate math and pathfinding engine

### ⚔️ Combat Systems
- [[Action System]] - Skills, items, moves, talk actions
- [[Targeting System]] - Range calculation and AOE mechanics
- [[Status Effects]] - Buffs, debuffs, and conditions
- [[AI Behaviors]] - NPC decision-making strategies

### 🎮 UI Systems
- [[Input Management]] - CentralInputManager context routing
- [[Menu System]] - MenuControls navigation
- [[HexControls]] - Hex cursor and targeting UI
- [[Inventory System]] - Item management (🚧 in progress)

### 🔗 Integration
- [[Dialogic Integration]] - Visual novel sequences
- [[Scene Management]] - Story progression system
- [[Configuration System]] - JSON data loading

### 📚 Reference
- [[Component Reference]] - Complete class documentation
- [[Signal Reference]] - All events and signals
- [[Configuration Files]] - JSON structure reference
- [[Common Patterns]] - Code templates and examples

### 🚀 Development
- [[TODO & Roadmap]] - Implementation status and future plans
- [[Architecture Decisions]] - Why things are designed this way
- [[Debugging Guide]] - Common issues and solutions

---

## 📋 Quick Reference Tables

### File → Purpose Mapping

| File | Purpose |
|------|---------|
| `BattleManager.cs` | Coordinates all battle subsystems |
| `BattleStateManager.cs` | Tracks entities and battlefield state |
| `BattleActionHandler.cs` | Calculates targets and executes actions |
| `TurnManager.cs` | Manages turn order and battle flow |
| `BattleUIController.cs` | Shows/hides UI elements |
| `BattleConfigurationLoader.cs` | Loads JSON configurations |
| `NPCBehaviorManager.cs` | Routes AI decisions |
| `HexGrid.cs` | Pure hex math engine |
| `HexControls.cs` | Hex targeting UI |
| `CentralInputManager.cs` | Routes input by context |
| `MenuControls.cs` | Generic menu navigation |
| `Entity.cs` | Data model for all combatants |
| `ActionConfig.cs` | Defines skills/items/moves/talk |
| `InventoryManager.cs` | Global inventory singleton |
| `SceneManager.cs` | Story sequence progression |

### System → Files Mapping

| System | Files |
|--------|-------|
| **Battle Core** | BattleManager, BattleStateManager, TurnManager |
| **Action Execution** | BattleActionHandler, ActionConfig, BattleConfigurationLoader |
| **UI** | BattleUIController, CentralInputManager, MenuControls, HexControls |
| **AI** | NPCBehaviorManager, INPCBehavior, [Behavior classes] |
| **Hex Math** | HexGrid, HexControls |
| **Data** | Entity, ActionConfig, CustomJsonLoader |
| **Integration** | SceneManager, DialogueControls |
| **Inventory** | InventoryManager, InventoryControls |

### Class → Responsibility Mapping

| Class | One-Line Responsibility |
|-------|------------------------|
| BattleManager | "Coordinates the battle" |
| BattleStateManager | "Tracks who is where" |
| BattleActionHandler | "Executes actions on targets" |
| TurnManager | "Decides who acts next" |
| BattleUIController | "Shows the right UI" |
| NPCBehaviorManager | "Makes NPC decisions" |
| HexGrid | "Does hex math" |
| HexControls | "Handles targeting UI" |
| CentralInputManager | "Routes input correctly" |
| MenuControls | "Navigates menus" |
| Entity | "Stores combatant data" |
| ActionConfig | "Defines an action" |
| InventoryManager | "Manages items globally" |
| SceneManager | "Progresses story" |

---

## 🔍 Find By Topic

### Hex Coordinate System
- [[Hex Grid System]] - Core concepts
- [[Component Reference#HexGrid]] - API reference
- [[Targeting System#Hex Math]] - Algorithms
- [[Common Patterns#Hex Calculations]] - Code examples

### Action System
- [[Action System]] - Complete guide
- [[Targeting System]] - Target calculation
- [[Configuration Files#ActionConfig]] - JSON schema
- [[Data Flow#Player Action]] - Execution sequence

### AI System
- [[AI Behaviors]] - Strategy guide
- [[Component Reference#NPCBehavior]] - Class reference
- [[Configuration Files#EntityDefinition]] - AI config
- [[Data Flow#NPC Turn]] - Decision-making sequence

### UI System
- [[Input Management]] - Context routing
- [[Menu System]] - Navigation
- [[HexControls]] - Targeting
- [[Component Reference#UI Components]] - All UI classes

### Integration
- [[Dialogic Integration]] - VN sequences
- [[Scene Management]] - Story flow
- [[Configuration Files#story_sequence]] - Sequence JSON
- [[Data Flow#Dialogic Integration]] - Timeline flow

### Configuration
- [[Configuration Files]] - Complete reference
- [[Action System#Configuration]] - Action JSON
- [[Configuration Files#battle_config]] - Battle JSON
- [[Common Patterns#Configuration]] - Examples

---

## 🎯 Common Tasks

### Adding a New Skill
1. Read [[Action System#Example Configurations]]
2. Add to `battle_config.json`
3. Test with debug mode enabled
4. See [[Configuration Files#ActionConfig]]

### Adding a New AI Behavior
1. Read [[AI Behaviors#Creating Behaviors]]
2. Implement `INPCBehavior` interface
3. Register in NPCBehaviorManager
4. See [[Component Reference#NPCBehavior]]

### Adding a New UI Screen
1. Read [[Common Patterns#UI Screens]]
2. Create scene structure
3. Create controller script
4. Connect to CentralInputManager
5. See [[UI Systems]] documentation

### Debugging an Issue
1. Enable `debugMode` in config
2. Check [[Data Flow]] for relevant sequence
3. Review [[Component Reference]] for involved classes
4. See [[Debugging Guide]] for common issues

### Understanding Data Flow
1. Pick a scenario (e.g., "Player uses skill")
2. Read corresponding section in [[Data Flow]]
3. Follow references to component docs
4. Trace through actual code

---

## 📊 System Relationships

### Component Dependencies
```
CentralInputManager
    ↓ (no dependencies)

BattleManager
    ↓ (depends on all below)

BattleConfigurationLoader
    ↓ CustomJsonLoader

BattleStateManager
    ↓ HexGrid, Entity

BattleActionHandler
    ↓ BattleStateManager, BattleConfigurationLoader

TurnManager
    ↓ BattleStateManager, BattleUIController

NPCBehaviorManager
    ↓ All managers

BattleUIController
    ↓ HexGrid, MenuControls, HexControls
```

See [[System Architecture#Component Dependencies]] for detailed graph.

### Data Flow Patterns
```
Input → CentralInputManager → Active UI → BattleManager → Subsystems
Config → JSON → Loader → Manager → Execution
Turn → TurnManager → Entity → Action → Effect → StateManager
AI → Behavior → Decision → ActionHandler → Execution
```

See [[Data Flow]] for complete sequences.

---

## 🆕 Recent Updates

### 2024 Updates
- ✅ Complete documentation vault created
- ✅ All core systems documented
- ✅ Configuration reference completed
- ✅ Data flow diagrams added
- ✅ TODO roadmap established
- 🚧 Inventory system in progress

---

## 🤝 Contributing to Documentation

### When Adding Features
1. Update [[Component Reference]] with new classes
2. Update [[Data Flow]] if adding new sequences
3. Update [[Configuration Files]] if adding JSON fields
4. Update [[TODO & Roadmap]] progress
5. Add examples to [[Common Patterns]]

### Documentation Standards
- Use clear, concise language
- Include code examples
- Link to related documentation
- Update index when adding new files
- Use consistent formatting

### Markdown Conventions
- H1 (#) for page title
- H2 (##) for major sections
- H3 (###) for subsections
- Code blocks with language specified
- Internal links with [[Page Name]]
- External links with [Text](URL)

---

## 🔗 External Resources

### Godot Documentation
- [Godot C# Docs](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/)
- [TileMap](https://docs.godotengine.org/en/stable/classes/class_tilemap.html)
- [Signals](https://docs.godotengine.org/en/stable/getting_started/step_by_step/signals.html)

### Dialogic
- [Dialogic Documentation](https://docs.dialogic.pro/)
- [Dialogic 2 Timeline Syntax](https://docs.dialogic.pro/timeline-syntax/)

### Hex Grid Resources
- [Red Blob Games - Hexagonal Grids](https://www.redblobgames.com/grids/hexagons/)
- [Hex Coordinate Systems](https://www.redblobgames.com/grids/hexagons/implementation.html)

---

## 📝 Notes

### Documentation Philosophy
This documentation follows these principles:

1. **Start with Why** - Explain purpose before implementation
2. **Show, Don't Just Tell** - Include examples and diagrams
3. **Link Everything** - Connect related concepts
4. **Keep It Current** - Update docs with code changes
5. **Write for Humans** - Clear, friendly language

### Obsidian Features Used
- `[[Double Bracket]]` internal links
- Headers for navigation
- Code blocks with syntax highlighting
- Tables for organization
- Checkboxes for TODO items
- Callouts (if supported)

### Maintenance
- Review quarterly for accuracy
- Update with new features
- Archive obsolete information
- Gather feedback from team

---

**Last Updated**: October 8, 2025
**Documentation Version**: 0.1
**Code Version**: Current main branch